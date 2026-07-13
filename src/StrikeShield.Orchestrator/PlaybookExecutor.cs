using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;
using StrikeShield.Infrastructure.Persistence;

namespace StrikeShield.Orchestrator;

/// <summary>
/// Runs each PlaybookStep as its own isolated, resource-bounded, network-
/// scoped Docker container (docs/ARCHITECTURE.md §5). Every container is
/// created, started, waited-on (with a hard timeout), and removed in this
/// one method — nothing here is left running past ExecuteAsync returning,
/// which is the Phase 2 acceptance criterion ("docker ps -a" shows no
/// leaked containers).
/// </summary>
public class PlaybookExecutor : IPlaybookExecutor
{
    private readonly IDockerClient _dockerClient;
    private readonly StrikeShieldDbContext _dbContext;
    private readonly OrchestratorOptions _options;
    private readonly ILogger<PlaybookExecutor> _logger;

    public PlaybookExecutor(
        IDockerClient dockerClient,
        StrikeShieldDbContext dbContext,
        IOptions<OrchestratorOptions> options,
        ILogger<PlaybookExecutor> logger)
    {
        _dockerClient = dockerClient;
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ExecuteAsync(ScanJob scanJob, CancellationToken cancellationToken)
    {
        var playbook = scanJob.Playbook
            ?? throw new InvalidOperationException($"ScanJob {scanJob.Id} has no loaded Playbook.");
        var target = scanJob.Target
            ?? throw new InvalidOperationException($"ScanJob {scanJob.Id} has no loaded Target.");

        var overallSuccess = true;

        foreach (var step in playbook.Steps.OrderBy(s => s.Order))
        {
            var stepRun = new StepRun
            {
                ScanJobId = scanJob.Id,
                PlaybookStepId = step.Id,
                Status = StepRunStatus.Running,
                StartedAt = DateTimeOffset.UtcNow
            };
            _dbContext.StepRuns.Add(stepRun);
            await _dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                var result = await RunStepContainerAsync(step, target, scanJob.Id, stepRun.Id, cancellationToken);

                stepRun.ExitCode = result.ExitCode;
                stepRun.ContainerId = result.ContainerId;
                stepRun.Status = result.TimedOut
                    ? StepRunStatus.TimedOut
                    : result.ExitCode == 0
                        ? StepRunStatus.Completed
                        : StepRunStatus.Failed;
                stepRun.CompletedAt = DateTimeOffset.UtcNow;

                if (result.OutputContent is not null)
                {
                    _dbContext.Artifacts.Add(new Artifact
                    {
                        StepRunId = stepRun.Id,
                        FileName = result.OutputFileName,
                        ContentType = "text/plain",
                        Content = result.OutputContent
                    });
                }

                if (stepRun.Status != StepRunStatus.Completed)
                {
                    overallSuccess = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Step {StepId} for ScanJob {ScanJobId} failed unexpectedly", step.Id, scanJob.Id);
                stepRun.Status = StepRunStatus.Failed;
                stepRun.CompletedAt = DateTimeOffset.UtcNow;
                stepRun.ErrorMessage = ex.Message;
                overallSuccess = false;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            if (!overallSuccess)
            {
                break;
            }
        }

        scanJob.Status = overallSuccess ? ScanJobStatus.Completed : ScanJobStatus.Failed;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<StepExecutionResult> RunStepContainerAsync(
        PlaybookStep step,
        Target target,
        Guid scanJobId,
        Guid stepRunId,
        CancellationToken cancellationToken)
    {
        const string outputFileName = "output.jsonl";
        var outputDir = Path.Combine(_options.ScanOutputMountPath, scanJobId.ToString(), stepRunId.ToString());
        var containerOutputPath = $"{outputDir}/{outputFileName}";

        // Same shared volume is mounted at the same path in this
        // (Orchestrator) container and the step container below, so a
        // directory created here is immediately visible to the step.
        Directory.CreateDirectory(outputDir);

        var args = step.ArgsTemplate
            .Replace("{target}", target.Value)
            .Replace("{output}", containerOutputPath)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        await _dockerClient.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = step.ImageRepository, Tag = step.ImageTag },
            null,
            new Progress<JSONMessage>(),
            cancellationToken);

        var createResponse = await _dockerClient.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Name = $"strikeshield-step-{stepRunId}",
                Image = $"{step.ImageRepository}:{step.ImageTag}",
                Cmd = args,
                Labels = new Dictionary<string, string>
                {
                    ["strikeshield.scan-job-id"] = scanJobId.ToString(),
                    ["strikeshield.step-run-id"] = stepRunId.ToString()
                },
                HostConfig = new HostConfig
                {
                    NetworkMode = _options.NetworkName,
                    Memory = step.MemoryLimitBytes,
                    NanoCPUs = step.NanoCpus,
                    Mounts = new List<Mount>
                    {
                        new Mount
                        {
                            Type = "volume",
                            Source = _options.ScanOutputVolumeName,
                            Target = _options.ScanOutputMountPath
                        }
                    }
                }
            },
            cancellationToken);

        var containerId = createResponse.ID;
        var timedOut = false;
        long exitCode = -1;

        try
        {
            await _dockerClient.Containers.StartContainerAsync(containerId, new ContainerStartParameters(), cancellationToken);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(step.TimeoutSeconds));

            try
            {
                var waitResponse = await _dockerClient.Containers.WaitContainerAsync(containerId, timeoutCts.Token);
                exitCode = waitResponse.StatusCode;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                timedOut = true;
                _logger.LogWarning(
                    "Step {StepId} (container {ContainerId}) timed out after {TimeoutSeconds}s",
                    step.Id,
                    containerId,
                    step.TimeoutSeconds);

                try
                {
                    await _dockerClient.Containers.StopContainerAsync(
                        containerId,
                        new ContainerStopParameters { WaitBeforeKillSeconds = 5 },
                        CancellationToken.None);
                }
                catch (Exception stopEx)
                {
                    _logger.LogWarning(stopEx, "Failed to stop timed-out container {ContainerId}", containerId);
                }
            }
        }
        finally
        {
            try
            {
                await _dockerClient.Containers.RemoveContainerAsync(
                    containerId,
                    new ContainerRemoveParameters { Force = true },
                    CancellationToken.None);
            }
            catch (Exception removeEx)
            {
                _logger.LogWarning(removeEx, "Failed to remove container {ContainerId}", containerId);
            }
        }

        var outputContent = await TryReadOutputFileAsync(outputDir, outputFileName);

        return new StepExecutionResult(exitCode, timedOut, containerId, outputContent, outputFileName);
    }

    private static async Task<string?> TryReadOutputFileAsync(string outputDir, string fileName)
    {
        var path = Path.Combine(outputDir, fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllTextAsync(path);
    }

    private sealed record StepExecutionResult(
        long ExitCode,
        bool TimedOut,
        string ContainerId,
        string? OutputContent,
        string OutputFileName);
}
