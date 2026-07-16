using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StrikeShield.Application.Common;
using StrikeShield.Application.ScanJobs;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Api.Hubs;

public record ScanJobProgressPayload(ScanJobResponse ScanJob, IReadOnlyList<StepRunResponse> Steps);

/// <summary>
/// Polls for ScanJobs the Orchestrator is actively working (Queued/Running/
/// AwaitingApproval) and pushes a fresh snapshot to that job's SignalR
/// group every tick, plus one final snapshot the moment a job leaves that
/// set (Completed/Failed/TimedOut) so the UI's last render is always
/// correct. Deliberately outside the Orchestrator process — it only reads
/// the same Postgres rows the Orchestrator already writes, no coupling to
/// its polling loop or docker.sock access (docs/ARCHITECTURE.md §5/§8).
/// </summary>
public class ScanProgressBroadcastService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private static readonly ScanJobStatus[] ActiveStatuses =
    {
        ScanJobStatus.Queued,
        ScanJobStatus.Running,
        ScanJobStatus.AwaitingApproval
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<ScanProgressHub> _hub;
    private readonly ILogger<ScanProgressBroadcastService> _logger;

    public ScanProgressBroadcastService(
        IServiceScopeFactory scopeFactory,
        IHubContext<ScanProgressHub> hub,
        ILogger<ScanProgressBroadcastService> logger)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var previouslyActiveIds = new HashSet<Guid>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

                var activeJobs = await db.ScanJobs
                    .Include(s => s.Playbook)
                    .Where(s => ActiveStatuses.Contains(s.Status))
                    .ToListAsync(stoppingToken);

                var currentActiveIds = new HashSet<Guid>(activeJobs.Select(j => j.Id));

                foreach (var job in activeJobs)
                {
                    await BroadcastAsync(db, job, stoppingToken);
                }

                var justFinishedIds = previouslyActiveIds.Where(id => !currentActiveIds.Contains(id));
                foreach (var id in justFinishedIds)
                {
                    var job = await db.ScanJobs.Include(s => s.Playbook)
                        .FirstOrDefaultAsync(s => s.Id == id, stoppingToken);
                    if (job is not null)
                    {
                        await BroadcastAsync(db, job, stoppingToken);
                    }
                }

                previouslyActiveIds = currentActiveIds;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "ScanProgressBroadcastService poll iteration failed.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        }
    }

    private async Task BroadcastAsync(IAppDbContext db, ScanJob job, CancellationToken ct)
    {
        var stepRuns = await db.StepRuns
            .Include(s => s.PlaybookStep)
            .Include(s => s.Artifacts)
            .Where(s => s.ScanJobId == job.Id)
            .ToListAsync(ct);

        var payload = new ScanJobProgressPayload(
            ScanJobResponse.FromEntity(job),
            stepRuns
                .OrderBy(s => s.PlaybookStep!.Order)
                .Select(StepRunResponse.FromEntity)
                .ToList());

        await _hub.Clients.Group(ScanProgressHub.GroupName(job.Id)).SendAsync("scanJobUpdated", payload, ct);
    }
}
