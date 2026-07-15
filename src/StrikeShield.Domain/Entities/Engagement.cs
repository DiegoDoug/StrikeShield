using StrikeShield.Domain.ValueObjects;

namespace StrikeShield.Domain.Entities;

/// <summary>
/// A scoped, time-boxed body of authorized pentesting work against a
/// Project. This is the entity the scan-launch scope gate checks — see
/// docs/ARCHITECTURE.md §4/§8. No ScanJob may be created against an
/// Engagement that isn't approved and currently inside its scope window.
/// </summary>
public class Engagement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? RulesOfEngagement { get; set; }
    public string? AuthorizationEvidenceUri { get; set; }

    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }

    public DateTimeOffset ScopeStart { get; set; }
    public DateTimeOffset ScopeEnd { get; set; }
    public List<string> AllowedScopeRules { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ScanJob> ScanJobs { get; set; } = new List<ScanJob>();

    public bool IsApproved => ApprovedAt is not null && !string.IsNullOrWhiteSpace(ApprovedBy);

    public bool IsWithinScopeWindow(DateTimeOffset at) => at >= ScopeStart && at <= ScopeEnd;

    public void Approve(string approvedBy, DateTimeOffset at)
    {
        ApprovedBy = approvedBy;
        ApprovedAt = at;
    }

    public EngagementAuthorizationResult CheckAuthorizedForScan(DateTimeOffset at)
    {
        if (!IsApproved)
        {
            return EngagementAuthorizationResult.Denied(
                "Engagement has not been approved. Call POST /api/engagements/{id}/approve first.");
        }

        if (!IsWithinScopeWindow(at))
        {
            return EngagementAuthorizationResult.Denied(
                $"Engagement scope window ({ScopeStart:O} to {ScopeEnd:O}) does not include the current time ({at:O}).");
        }

        return EngagementAuthorizationResult.Allowed();
    }
}
