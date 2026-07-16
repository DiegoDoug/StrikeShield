using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Notifications;

/// <summary>
/// One delivery mechanism for an Integration (docs/PHASED_PLAN.md Phase 8 —
/// Slack, generic Webhook, GitHub issue creation). NotificationDispatcher
/// picks the channel matching an Integration's Type; real implementations
/// (HTTP calls out) live in StrikeShield.Infrastructure, same seam as
/// ILlmClient/IReportRenderer.
/// </summary>
public interface IIntegrationChannel
{
    IntegrationType Type { get; }

    Task SendScanCompletedAsync(
        Integration integration,
        ScanCompletionNotification notification,
        CancellationToken cancellationToken = default);

    Task SendCriticalFindingsAsync(
        Integration integration,
        ScanCompletionNotification notification,
        IReadOnlyList<CriticalFindingNotification> findings,
        CancellationToken cancellationToken = default);
}
