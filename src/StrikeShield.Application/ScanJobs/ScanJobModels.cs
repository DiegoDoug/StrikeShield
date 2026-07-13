using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.ScanJobs;

/// <summary>
/// PlaybookName is looked up against Playbook.Slug (e.g. "nuclei-quick") —
/// kept as a friendly string here rather than requiring callers to know a
/// Playbook GUID upfront (see GET /api/playbooks to discover slugs).
/// </summary>
public record CreateScanJobRequest(Guid EngagementId, Guid TargetId, string PlaybookName);

public record ScanJobResponse(
    Guid Id,
    Guid EngagementId,
    Guid TargetId,
    Guid PlaybookId,
    string PlaybookName,
    ScanJobStatus Status,
    DateTimeOffset CreatedAt)
{
    /// <summary>
    /// Requires <paramref name="entity"/>.Playbook to be loaded (e.g. via
    /// <c>.Include(s => s.Playbook)</c>) — throws otherwise, which is
    /// intentional: a ScanJob without its Playbook loaded is a caller bug,
    /// not a case to silently paper over with a null-conditional.
    /// </summary>
    public static ScanJobResponse FromEntity(ScanJob entity) => new(
        entity.Id,
        entity.EngagementId,
        entity.TargetId,
        entity.PlaybookId,
        entity.Playbook!.Slug,
        entity.Status,
        entity.CreatedAt);
}
