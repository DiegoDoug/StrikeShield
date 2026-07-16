using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StrikeShield.Application.Common;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Notifications;

/// <summary>
/// Fans a completed ScanJob out to every enabled Integration configured on
/// its Organization (docs/ARCHITECTURE.md §4: "Integration ── Organization",
/// docs/PHASED_PLAN.md Phase 8: "Slack + generic webhook notifications on
/// scan completion / new critical finding; GitHub issue creation as an
/// optional integration"). GitHub issue creation only fires for critical
/// findings — a per-scan-completion issue with nothing actionable in it
/// isn't useful the way a Slack/webhook heads-up is.
/// </summary>
public class NotificationDispatcher : INotificationDispatcher
{
    private readonly IAppDbContext _db;
    private readonly IEnumerable<IIntegrationChannel> _channels;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(IAppDbContext db, IEnumerable<IIntegrationChannel> channels, ILogger<NotificationDispatcher> logger)
    {
        _db = db;
        _channels = channels;
        _logger = logger;
    }

    public async Task DispatchForCompletedScanJobAsync(Guid scanJobId, CancellationToken cancellationToken = default)
    {
        var scanJob = await _db.ScanJobs
            .Include(s => s.Playbook)
            .Include(s => s.Target)
            .Include(s => s.Engagement)
                .ThenInclude(e => e!.Project)
                    .ThenInclude(p => p!.Client)
            .FirstOrDefaultAsync(s => s.Id == scanJobId, cancellationToken);

        var organizationId = scanJob?.Engagement?.Project?.Client?.OrganizationId;
        if (scanJob is null || organizationId is null)
        {
            return;
        }

        var integrations = await _db.Integrations
            .Where(i => i.OrganizationId == organizationId.Value && i.Enabled)
            .ToListAsync(cancellationToken);

        if (integrations.Count == 0)
        {
            return;
        }

        var findings = await _db.Findings
            .Where(f => f.ScanJobId == scanJobId)
            .ToListAsync(cancellationToken);

        var criticalFindings = findings.Where(f => f.Severity == FindingSeverity.Critical).ToList();

        var notification = new ScanCompletionNotification(
            scanJob.Id,
            scanJob.EngagementId,
            scanJob.Engagement!.Name,
            scanJob.Target!.Value,
            scanJob.Playbook!.Name,
            scanJob.Status,
            findings.Count,
            criticalFindings.Count);

        var criticalNotifications = criticalFindings
            .Select(f => new CriticalFindingNotification(f.Id, f.Title, f.Description, f.AffectedAsset, f.SourceTool, f.CvssScore))
            .ToList();

        foreach (var integration in integrations)
        {
            var channel = _channels.FirstOrDefault(c => c.Type == integration.Type);
            if (channel is null)
            {
                continue;
            }

            if (integration.NotifyOnScanCompletion && integration.Type != IntegrationType.GitHub)
            {
                await TrySendAsync(
                    () => channel.SendScanCompletedAsync(integration, notification, cancellationToken),
                    integration,
                    "scan completion");
            }

            if (integration.NotifyOnCriticalFinding && criticalNotifications.Count > 0)
            {
                await TrySendAsync(
                    () => channel.SendCriticalFindingsAsync(integration, notification, criticalNotifications, cancellationToken),
                    integration,
                    "critical findings");
            }
        }
    }

    private async Task TrySendAsync(Func<Task> send, Integration integration, string eventName)
    {
        try
        {
            await send();
        }
        catch (Exception ex)
        {
            // Best-effort delivery: a failed Slack/webhook/GitHub call must
            // never fail the ScanJob whose completion triggered it.
            _logger.LogWarning(
                ex,
                "Failed to deliver {EventName} notification via {IntegrationType} integration {IntegrationId}.",
                eventName,
                integration.Type,
                integration.Id);
        }
    }
}
