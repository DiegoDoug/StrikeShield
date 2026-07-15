using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.ScanJobs;

/// <summary>
/// One Asset discovered by a step of this ScanJob (docs/PHASED_PLAN.md
/// Phase 5's "hand-off" acceptance test: subdomains/URLs a recon step
/// found, surfaced here regardless of which downstream step later
/// consumed them as input).
/// </summary>
public record AssetResponse(
    Guid Id,
    AssetType Type,
    string Value,
    string? Metadata,
    Guid? DiscoveredByStepRunId,
    DateTimeOffset CreatedAt)
{
    public static AssetResponse FromEntity(Asset entity) => new(
        entity.Id,
        entity.Type,
        entity.Value,
        entity.Metadata,
        entity.DiscoveredByStepRunId,
        entity.CreatedAt);
}
