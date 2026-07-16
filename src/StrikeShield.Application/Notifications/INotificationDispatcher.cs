namespace StrikeShield.Application.Notifications;

public interface INotificationDispatcher
{
    /// <summary>
    /// Fires scan-completion and (if any exist) new-critical-finding
    /// notifications to every enabled Integration on the ScanJob's
    /// Organization (docs/PHASED_PLAN.md Phase 8). A no-op if the
    /// Organization has no Integrations configured. Delivery failures are
    /// logged and swallowed per-integration — this must never throw back
    /// into the caller (the Orchestrator's ScanJob execution loop).
    /// </summary>
    Task DispatchForCompletedScanJobAsync(Guid scanJobId, CancellationToken cancellationToken = default);
}
