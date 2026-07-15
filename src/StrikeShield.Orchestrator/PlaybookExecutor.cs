using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StrikeShield.Application.Findings;
using StrikeShield.Application.Playbooks;
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
    // Strix's own documented behavior: "-n" exits non-zero when it finds
    // vulnerabilities — that's a result, not a failure (same idea as
    // zap-baseline's WARN/FAIL exit codes, but zap's is already fully
    // handled by the -I flag in its seeded ArgsTemplate). A genuine crash
    // still gets caught below since it wouldn't have produced findings.sarif.
    private static readonly HashSet<string> ToolsWhereNonZeroExitCanMeanFindingsFound =
        new(StringComparer.OrdinalIgnoreCase) { "strix" };

    private readonly IDockerClient _dockerClient;
    private readonly StrikeShieldDbContext _dbContext;
    private readonly IFindingIngestionService _findingIngestionService;
    private readonly ICorrelator _correlator;
    private readonly IAdaptivePlanner _adaptivePlanner;
    private readonly OrchestratorOptions _options;
    private readonly ILogger<PlaybookExecutor> _logger;

    public PlaybookExecutor(
        IDockerClient dockerClient,
        StrikeShieldDbContext dbContext,
        IFindingIngestionService findingIngestionService,
        ICorrelator correlator,
        IAdaptivePlanner adaptivePlanner,
        IOptions<OrchestratorOptions> options,
        ILogger<PlaybookExecutor> logger)
    {
        _dockerClient = dockerClient;
        _dbContext = dbContext;
        _findingIngestionService = findingIngestionService;
        _correlator = correlator;
        _adaptivePlanner = adaptivePlanner;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ExecuteAsync(ScanJob scanJob, CancellationToken cancellationToken)
    {
        var playbook = scanJob.Playbook
            ?? throw new InvalidOperationException($"ScanJob {scanJob.Id} has no loaded Playbook.");
        var target = scanJob.Target
            ?? throw new InvalidOperationException($"ScanJob {scanJob.Id} has no loaded Target.");

        var orderedSteps = PlaybookDagPlanner.TopologicalOrder(playbook.Steps.ToList());
        var stepRunsByStepKey = new Dictionary<string, StepRun>(StringComparer.OrdinalIgnoreCase);

        // Loaded once per invocation so a resumed job (see below) picks up
        // whatever an operator decided since the last run.
        var amendments = await _dbContext.PlaybookAmendments
            .Where(a => a.ScanJobId == scanJob.Id)
            .ToListAsync(cancellationToken);

        // Unlike Phase 2-4's single linear chain, a DAG's branches are
        // independent: an unrelated step failing shouldn't stop a step
        // with no dependency on it. Only a step's own DependsOn (evaluated
        // per-step below via Condition) skips it; the ScanJob as a whole
        // is Failed if *any* step that actually ran ended up Failed/TimedOut.
        var anyStepFailed = false;

        foreach (var step in orderedSteps)
        {
            // Resume support (docs/PHASED_PLAN.md Phase 6): a job paused
            // AwaitingApproval gets re-queued once its amendment is
            // approved/rejected, and ExecuteAsync runs again from the top —
            // steps that already have a StepRun from a prior invocation
            // must not be re-created/re-launched.
            var existingStepRun = await _dbContext.StepRuns
                .FirstOrDefaultAsync(sr => sr.ScanJobId == scanJob.Id && sr.PlaybookStepId == step.Id, cancellationToken);
            if (existingStepRun is not null)
            {
                stepRunsByStepKey[step.StepKey] = existingStepRun;
                if (existingStepRun.Status is StepRunStatus.Failed or StepRunStatus.TimedOut)
                {
                    anyStepFailed = true;
                }

                continue;
            }

            // The Adaptive Planner's pause point (docs/PHASED_PLAN.md
            // Phase 6): a Pending amendment targeting this not-yet-run step
            // means a human hasn't decided yet — stop here rather than
            // running it either with or without the proposed change.
            if (amendments.Any(a => a.TargetPlaybookStepId == step.Id && a.Status == PlaybookAmendmentStatus.Pending))
            {
                scanJob.Status = ScanJobStatus.AwaitingApproval;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            var dependencyStepRuns = step.DependsOn
                .Select(depKey => stepRunsByStepKey.TryGetValue(depKey, out var sr) ? sr : null)
                .Where(sr => sr is not null)
                .Select(sr => sr!)
                .ToList();

            var shouldSkip = step.Condition == StepCondition.OnSuccess
                && dependencyStepRuns.Any(sr => sr.Status != StepRunStatus.Completed);

            var stepRun = new StepRun
            {
                ScanJobId = scanJob.Id,
                PlaybookStepId = step.Id,
                Status = shouldSkip ? StepRunStatus.Skipped : StepRunStatus.Running,
                StartedAt = shouldSkip ? null : DateTimeOffset.UtcNow
            };
            _dbContext.StepRuns.Add(stepRun);
            await _dbContext.SaveChangesAsync(cancellationToken);
            stepRunsByStepKey[step.StepKey] = stepRun;

            if (shouldSkip)
            {
                stepRun.CompletedAt = DateTimeOffset.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                continue;
            }

            try
            {
                // Step N+1 consumes step N's discovered Assets as its own
                // target/wordlist input via "{assetsFile}" tokens in its
                // ArgsTemplate (docs/PHASED_PLAN.md Phase 5) — e.g.
                // subfinder's subdomains become nmap's host list, katana's
                // URLs become nuclei's/ffuf's input list.
                var dependencyStepRunIds = dependencyStepRuns.Select(sr => sr.Id).ToList();
                var upstreamAssets = dependencyStepRunIds.Count > 0
                    ? await _dbContext.Assets
                        .Where(a => a.DiscoveredByStepRunId != null && dependencyStepRunIds.Contains(a.DiscoveredByStepRunId!.Value))
                        .ToListAsync(cancellationToken)
                    : new List<Asset>();

                // An Approved amendment (docs/PHASED_PLAN.md Phase 6)
                // substitutes its ProposedArgsTemplate for this run only —
                // the shared PlaybookStep template row is never mutated.
                var approvedArgsTemplate = amendments
                    .Where(a => a.TargetPlaybookStepId == step.Id && a.Status == PlaybookAmendmentStatus.Approved)
                    .OrderByDescending(a => a.DecidedAt)
                    .Select(a => a.ProposedArgsTemplate)
                    .FirstOrDefault();

                var result = await RunStepContainerAsync(
                    step,
                    approvedArgsTemplate ?? step.ArgsTemplate,
                    target,
                    scanJob.Id,
                    stepRun.Id,
                    upstreamAssets,
                    cancellationToken);

                stepRun.ExitCode = result.ExitCode;
                stepRun.ContainerId = result.ContainerId;

                var succeededDespiteExitCode = result.ExitCode != 0
                    && ToolsWhereNonZeroExitCanMeanFindingsFound.Contains(step.ToolName)
                    && result.OutputContent is not null;
                stepRun.Status = result.TimedOut
                    ? StepRunStatus.TimedOut
                    : result.ExitCode == 0 || succeededDespiteExitCode
                        ? StepRunStatus.Completed
                        : StepRunStatus.Failed;
                stepRun.CompletedAt = DateTimeOffset.UtcNow;

                // Container stdout/stderr is the only way to diagnose a
                // nonzero exit/timeout, or a "succeeded" tool that still
                // didn't produce its expected output file (e.g. a report
                // writer failing silently past its own exit-code check),
                // after the container's been removed — surface it via the
                // API instead of requiring someone to have been watching
                // `docker compose logs` live when it happened.
                if ((stepRun.Status != StepRunStatus.Completed || result.OutputContent is null)
                    && !string.IsNullOrWhiteSpace(result.ContainerLogs))
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

                // Secondary, non-ingested files (Strix's run.json cost/run
                // metadata) — stored for visibility only, no adapter parses
                // these.
                if (result.AdditionalArtifacts is not null)
                {
                    foreach (var (fileName, content) in result.AdditionalArtifacts)
                    {
                        _dbContext.Artifacts.Add(new Artifact
                        {
                            StepRunId = stepRun.Id,
                            FileName = fileName,
                            ContentType = "text/plain",
                            Content = content
                        });
                    }
                }

                if (stepRun.Status != StepRunStatus.Completed)
                {
                    anyStepFailed = true;
                }
                else
                {
                    // The Adaptive Planner (docs/ARCHITECTURE.md §3, Phase
                    // 6): after a successful step, it may propose an
                    // amendment to a not-yet-run downstream step — never
                    // applied automatically, just recorded Pending. A no-op
                    // (no LLM call) if no BYOK key is configured.
                    var stepIndex = orderedSteps.IndexOf(step);
                    var remainingSteps = orderedSteps
                        .Skip(stepIndex + 1)
                        .Where(s => !stepRunsByStepKey.ContainsKey(s.StepKey))
                        .ToList();

                    if (remainingSteps.Count > 0)
                    {
                        await _adaptivePlanner.ProposeAmendmentAsync(scanJob.Id, stepRun.Id, remainingSteps, cancellationToken);

                        // Refresh the in-memory snapshot: if that call just
                        // created a Pending amendment targeting a step later
                        // in *this same* loop, the pause-check above must
                        // see it — it was fetched before this iteration ran.
                        amendments = await _dbContext.PlaybookAmendments
                            .Where(a => a.ScanJobId == scanJob.Id)
                            .ToListAsync(cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Step {StepId} for ScanJob {ScanJobId} failed unexpectedly", step.Id, scanJob.Id);
                stepRun.Status = StepRunStatus.Failed;
                stepRun.CompletedAt = DateTimeOffset.UtcNow;
                stepRun.ErrorMessage = ex.Message;
                anyStepFailed = true;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Correlate whatever Findings were ingested even on partial failure
        // — an earlier completed step's findings are still worth deduping.
        await _correlator.CorrelateAsync(scanJob.Id, cancellationToken);

        scanJob.Status = anyStepFailed ? ScanJobStatus.Failed : ScanJobStatus.Completed;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<StepExecutionResult> RunStepContainerAsync(
        PlaybookStep step,
        string effectiveArgsTemplate,
        Target target,
        Guid scanJobId,
        Guid stepRunId,
        IReadOnlyList<Asset> upstreamAssets,
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

        // This container (running as root) owns the directory it just
        // created, but several tool images (ZAP's, notably) run as a
        // fixed non-root UID and need to write their output into it —
        // world-writable is fine here since it only ever holds ephemeral
        // per-job scan output, nothing sensitive.
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                outputDir,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
        }

        // "{outputRelative}" (used by zap): a path relative to the shared
        // volume's root — its own report writer joins the path it's given
        // against its own working directory rather than honoring an
        // absolute path, so "{output}" alone would silently land somewhere
        // we never look. Resolves to the same file as "{output}" as long as
        // the tool's container also mounts the shared volume at its own
        // working directory (see the zap-specific mount below).
        // "{assetsFile}"/"{assetsFile:<AssetType>}": this step's upstream
        // dependencies' discovered Assets, written to a file in this same
        // shared volume (docs/PHASED_PLAN.md Phase 5) — see StepArgsBuilder.
        var args = StepArgsBuilder.Build(effectiveArgsTemplate, target, outputDir, containerOutputPath, relativeOutputPath, upstreamAssets);

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

        var isStrix = step.ToolName.Equals("strix", StringComparison.OrdinalIgnoreCase);
        var env = new List<string>();

        if (isStrix)
        {
            // Strix's own runtime talks to the Docker daemon directly via
            // the Python docker SDK (confirmed in strix-agent's own
            // pyproject.toml — docker>=7.1.0, no CLI shell-out) to spawn its
            // own Kali-based sandbox for dynamic testing. This is the one
            // deliberate, documented exception to "only the Orchestrator
            // touches docker.sock" (docs/ARCHITECTURE.md §5/§8) — Strix
            // fundamentally needs it to do its job, the same way it needs
            // it when run outside a container at all.
            mounts.Add(new Mount { Type = "bind", Source = "/var/run/docker.sock", Target = "/var/run/docker.sock" });
            env.Add($"STRIX_LLM={_options.StrixLlmModel}");
            env.Add($"LLM_API_KEY={_options.StrixLlmApiKey}");
        }

        // Docker.DotNet's CreateImageAsync always attempts a registry pull —
        // fine for every tool image we reference (all public), but
        // strikeshield/-prefixed images are our own, built locally only by
        // `docker compose build` (see docker-compose.yml's strix-runner
        // service); pulling them would just fail against no registry.
        if (!step.ImageRepository.StartsWith("strikeshield/", StringComparison.OrdinalIgnoreCase))
        {
            await _dockerClient.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = step.ImageRepository, Tag = step.ImageTag },
                null,
                new Progress<JSONMessage>(),
                cancellationToken);
        }

        var createResponse = await _dockerClient.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Name = $"strikeshield-step-{stepRunId}",
                Image = $"{step.ImageRepository}:{step.ImageTag}",
                Cmd = args,
                Env = env,
                // Strix has no flag to control its output path (it always
                // writes to "./strix_runs/<auto-generated-run-name>/"), so
                // the only way to keep its output inside our tracked,
                // per-step directory is to make that directory its cwd.
                WorkingDir = isStrix ? outputDir : null,
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

        if (isStrix)
        {
            var (sarifContent, additionalArtifacts) = await TryReadStrixOutputAsync(outputDir);
            return new StepExecutionResult(exitCode, timedOut, containerId, sarifContent, "findings.sarif", containerLogs, additionalArtifacts);
        }

        var outputContent = await TryReadOutputFileAsync(outputDir, outputFileName);

        return new StepExecutionResult(exitCode, timedOut, containerId, outputContent, outputFileName, containerLogs);
    }

    /// <summary>
    /// Strix has no flag to fix its output filename/path (see the
    /// WorkingDir comment above) — it always writes
    /// "./strix_runs/&lt;auto-generated-run-name&gt;/", so this globs for the
    /// run directory and the .sarif file inside it rather than assuming an
    /// exact path.
    /// </summary>
    private static async Task<(string? SarifContent, List<(string FileName, string Content)>? AdditionalArtifacts)> TryReadStrixOutputAsync(string outputDir)
    {
        var runsDir = Path.Combine(outputDir, "strix_runs");
        if (!Directory.Exists(runsDir))
        {
            return (null, null);
        }

        // One target per ScanJob step means exactly one run directory is
        // expected; take the first if Strix ever surprises us with more.
        var runDir = Directory.GetDirectories(runsDir).FirstOrDefault();
        if (runDir is null)
        {
            return (null, null);
        }

        var sarifPath = Directory.GetFiles(runDir, "*.sarif", SearchOption.AllDirectories).FirstOrDefault();
        var sarifContent = sarifPath is not null ? await File.ReadAllTextAsync(sarifPath) : null;

        var additionalArtifacts = new List<(string, string)>();
        var runJsonPath = Path.Combine(runDir, "run.json");
        if (File.Exists(runJsonPath))
        {
            additionalArtifacts.Add(("run.json", await File.ReadAllTextAsync(runJsonPath)));
        }

        return (sarifContent, additionalArtifacts.Count > 0 ? additionalArtifacts : null);
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
        "katana" => "output.jsonl",
        "ffuf" => "output.json",
        "nikto" => "output.json",
        _ => "output.txt"
    };

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private sealed record StepExecutionResult(
        long ExitCode,
        bool TimedOut,
        string ContainerId,
        string? OutputContent,
        string OutputFileName,
        string? ContainerLogs,
        IReadOnlyList<(string FileName, string Content)>? AdditionalArtifacts = null);
}
