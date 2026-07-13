using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StrikeShield.Domain.Enums;
using StrikeShield.Infrastructure.Persistence;

namespace StrikeShield.Orchestrator;

/// <summary>
/// Polls for Queued ScanJobs and runs them one at a time via
/// IPlaybookExecutor. Simple polling (rather than a queue/pub-sub) is
/// deliberate for Phase 2 — there's exactly one Orchestrator instance and
/// one step type (Nuclei); a real work queue arrives if/when this needs to
/// scale beyond a single worker.
/// </summary>
public class ScanJobRunnerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OrchestratorOptions _options;
    private readonly ILogger<ScanJobRunnerService> _logger;

    public ScanJobRunnerService(
        IServiceScopeFactory scopeFactory,
        IOptions<OrchestratorOptions> options,
        ILogger<ScanJobRunnerService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ScanJobRunnerService started, polling every {PollIntervalSeconds}s.",
            _options.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextQueuedJobAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Unhandled error while processing queued scan jobs.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
        }
    }

    private async Task ProcessNextQueuedJobAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StrikeShieldDbContext>();
        var executor = scope.ServiceProvider.GetRequiredService<IPlaybookExecutor>();

        var scanJob = await dbContext.ScanJobs
            .Include(s => s.Playbook)
            .ThenInclude(p => p!.Steps)
            .Include(s => s.Target)
            .Where(s => s.Status == ScanJobStatus.Queued)
            .OrderBy(s => s.CreatedAt)
            .FirstOrDefaultAsync(stoppingToken);

        if (scanJob is null)
        {
            return;
        }

        _logger.LogInformation("Picking up ScanJob {ScanJobId}.", scanJob.Id);

        scanJob.Status = ScanJobStatus.Running;
        await dbContext.SaveChangesAsync(stoppingToken);

        await executor.ExecuteAsync(scanJob, stoppingToken);

        _logger.LogInformation("ScanJob {ScanJobId} finished with status {Status}.", scanJob.Id, scanJob.Status);
    }
}
