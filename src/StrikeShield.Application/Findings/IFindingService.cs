namespace StrikeShield.Application.Findings;

public interface IFindingService
{
    Task<IReadOnlyList<FindingResponse>> GetForScanJobAsync(Guid scanJobId, CancellationToken cancellationToken = default);

    /// <summary>Every finding across every ScanJob that belongs to this Engagement — backs the triage board.</summary>
    Task<IReadOnlyList<FindingResponse>> GetForEngagementAsync(Guid engagementId, CancellationToken cancellationToken = default);

    /// <summary>Human triage decision (docs/PHASED_PLAN.md Phase 9) — never touches severity/fingerprint, status only.</summary>
    Task<FindingResponse> UpdateStatusAsync(Guid id, UpdateFindingStatusRequest request, CancellationToken cancellationToken = default);
}
