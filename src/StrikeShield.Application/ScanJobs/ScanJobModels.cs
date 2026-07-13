using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.ScanJobs;

public record CreateScanJobRequest(Guid EngagementId, Guid TargetId, string PlaybookName);

public record ScanJobResponse(
    Guid Id,
    Guid EngagementId,
    Guid TargetId,
    string PlaybookName,
    ScanJobStatus Status,
    DateTimeOffset CreatedAt)
{
    public static ScanJobResponse FromEntity(ScanJob entity) => new(
        entity.Id,
        entity.EngagementId,
        entity.TargetId,
        entity.PlaybookName,
        entity.Status,
        entity.CreatedAt);
}
