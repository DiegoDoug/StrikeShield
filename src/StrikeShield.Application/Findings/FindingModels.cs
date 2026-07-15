using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Findings;

public record FindingResponse(
    Guid Id,
    Guid ScanJobId,
    Guid StepRunId,
    Guid? CorrelationGroupId,
    string SourceTool,
    string Title,
    string? Description,
    FindingSeverity Severity,
    List<string> CweIds,
    List<string> CveIds,
    string AffectedAsset,
    FindingStatus Status,
    string DedupeFingerprint,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt)
{
    public static FindingResponse FromEntity(Finding entity) => new(
        entity.Id,
        entity.ScanJobId,
        entity.StepRunId,
        entity.CorrelationGroupId,
        entity.SourceTool,
        entity.Title,
        entity.Description,
        entity.Severity,
        entity.CweIds,
        entity.CveIds,
        entity.AffectedAsset,
        entity.Status,
        entity.DedupeFingerprint,
        entity.FirstSeenAt,
        entity.LastSeenAt);
}
