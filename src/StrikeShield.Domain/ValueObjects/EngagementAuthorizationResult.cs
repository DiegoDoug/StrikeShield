namespace StrikeShield.Domain.ValueObjects;

/// <summary>
/// Outcome of <see cref="Entities.Engagement.CheckAuthorizedForScan"/> — the
/// one non-negotiable gate every active-scan launch path must consult before
/// a ScanJob is created (see docs/ARCHITECTURE.md §4/§8).
/// </summary>
public sealed class EngagementAuthorizationResult
{
    public bool IsAuthorized { get; }
    public string? Reason { get; }

    private EngagementAuthorizationResult(bool isAuthorized, string? reason)
    {
        IsAuthorized = isAuthorized;
        Reason = reason;
    }

    public static EngagementAuthorizationResult Allowed() => new(true, null);

    public static EngagementAuthorizationResult Denied(string reason) => new(false, reason);
}
