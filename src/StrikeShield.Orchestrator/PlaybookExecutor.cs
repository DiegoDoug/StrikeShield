using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StrikeShield.Application.Findings;
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
    private readonly IFindingIngestionService _findingIngestionService;
    private readonly ICorrelator _correlator;
    private readonly OrchestratorOptions _options;
    private readonly ILogger<PlaybookExecutor> _logger;

    public PlaybookExecutor(
        IDockerClient dockerClient,
        StrikeShieldDbContext dbContext,
        IFindingIngestionService findingIngestionService,
        ICorrelator correlator,
        IOptions<OrchestratorOptions> options,
        ILogger<PlaybookExecutor> logger)
    {
        _dockerClient = dockerClient;
        _dbContext = dbContext;
        _findingIngestionService = findingIngestionService;
        _correlator = correlator;
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

                // Container stdout/stderr is the only way to diagnose a
                // nonzero exit or timeout after the container's been
                // removed — surface it via the API instead of requiring
                // someone to have been watching `docker compose logs`
                // live when it happened.
                if (stepRun.Status != StepRunStatus.Completed && !string.IsNullOrWhiteSpace(result.ContainerLogs))
                {
                    stepRun.ErrorMessage = Truncate(result.ContainerLogs, 4000);
                }

                if (result.OutputContent is not null)
                {
                    _dbContext.Artifacts.Add(new Artifact
                    {
                        StepRunId = stepRun.Id,
                        FileName = result.OutputFileName,
                        ContentType = "text/plain",
                        Content = result.OutputContent
                    });

                    // Normalize the raw tool output into Findings/Assets
                    // (docs/ARCHITECTURE.md §6) — a no-op if no adapter is
                    // registered for this tool yet.
                    await _findingIngestionService.IngestAsync(
                        scanJob.Id,
                        stepRun.Id,
                        target.Id,
                        step.ToolName,
                        result.OutputContent,
                        cancellationToken);
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

        // Correlate whatever Findings were ingested even on partial failure
        // — an earlier completed step's findings are still worth deduping.
        await _correlator.CorrelateAsync(scanJob.Id, cancellationToken);

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
        var outputFileName = OutputFileNameFor(step.ToolName);
        var relativeOutputPath = $"{scanJobId}/{stepRunId}/{outputFileName}";
        var outputDir = Path.Combine(_options.ScanOutputMountPath, scanJobId.ToString(), stepRunId.ToString());
        var containerOutputPath = $"{outputDir}/{outputFileName}";

        // Same shared volume is mounted at the same path in this
        // (Orchestrator) container and the step container below, so a
        // directory created here is immediately visible to the step.
        Directory.CreateDirectory(outputDir);

        var args = step.ArgsTemplate
            .Replace("{target}", target.Value)
            .Replace("{targetHost}", ExtractHost(target.Value))
            .Replace("{output}", containerOutputPath)
            // A path relative to the shared volume's root — for tools
            // (zap) whose own report writer joins the path it's given
            // against its own working directory rather than honoring an
            // absolute path, so {output} silently lands somewhere we
            // never look. Resolves to the same file as {output} as long
            // as the tool's container also mounts the shared volume at
            // its own working directory (see the zap-specific mount below).
            .Replace("{outputRelative}", relativeOutputPath)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        var mounts = new List<Mount>
        {
            new Mount
            {
                Type = "volume",
                Source = _options.ScanOutputVolumeName,
                Target = _options.ScanOutputMountPath
            }
        };

        // zap-baseline.py hard-validates that /zap/wrk is mounted before
        // accepting ANY file-based option (-J/-r/...). Its default
        // "automation framework" mode also resolves the report path
        // relative to /zap/wrk regardless of whether it's given as
        // absolute (unlike the legacy code path, which does honor an
        // absolute path) — hence {outputRelative} in this step's
        // ArgsTemplate instead of {output}.
        if (step.ToolName.Equals("zap", StringComparison.OrdinalIgnoreCase))
        {
            mounts.Add(new Mount
            {
                Type = "volume",
                Source = _options.ScanOutputVolumeName,
                Target = "/zap/wrk"
            });
        }

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
                    Mounts = mounts
                }
            },
            cancellationToken);

        var containerId = createResponse.ID;
        var timedOut = false;
        long exitCode = -1;
        string? containerLogs = null;

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
            // Must capture logs before removal — they aren't retrievable
            // once the container is gone.
            try
            {
                var logStream = await _dockerClient.Containers.GetContainerLogsAsync(
                    containerId,
                    tty: false,
                    new ContainerLogsParameters { ShowStdout = true, ShowStderr = true },
                    CancellationToken.None);
                var (stdout, stderr) = await logStream.ReadOutputToEndAsync(CancellationToken.None);
                containerLogs = string.Join(
                    Environment.NewLine,
                    new[] { stdout, stderr }.Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to capture logs for container {ContainerId}", containerId);
            }

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

        return new StepExecutionResult(exitCode, timedOut, containerId, outputContent, outputFileName, containerLogs);
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

    private static string OutputFileNameFor(string toolName) => toolName.ToLowerInvariant() switch
    {
        "nuclei" => "output.jsonl",
        "zap" => "output.json",
        "nmap" => "output.xml",
        _ => "output.txt"
    };

    /// <summary>
    /// nmap (and similar host-oriented tools) need a bare host/IP, not a
    /// full URL with scheme/port — extracts one from a Target.Value that
    /// may be either (e.g. "http://juice-shop:3000" or "juice-shop").
    /// </summary>
    private static string ExtractHost(string targetValue) =>
        Uri.TryCreate(targetValue, UriKind.Absolute, out var uri) ? uri.Host : targetValue;

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private sealed record StepExecutionResult(
        long ExitCode,
        bool TimedOut,
        string ContainerId,
        string? OutputContent,
        string OutputFileName,
        string? ContainerLogs);
}
