using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Scheduling;

/// <summary>
/// PlaybookName is looked up against Playbook.Slug, same convention as
/// CreateScanJobRequest. CronExpression is a standard 5-field cron string
/// (minute hour day-of-month month day-of-week), evaluated in UTC.
/// </summary>
public record CreateScanScheduleRequest(Guid EngagementId, Guid TargetId, string PlaybookName, string CronExpression, string CreatedBy);

public record SetScanScheduleEnabledRequest(bool Enabled);

public record ScanScheduleResponse(
    Guid Id,
    Guid EngagementId,
    Guid TargetId,
    Guid PlaybookId,
    string PlaybookName,
    string CronExpression,
    bool Enabled,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastFiredAt,
    ScanScheduleFireOutcome? LastFireOutcome,
    string? LastSkipReason,
    Guid? LastTriggeredScanJobId)
{
    /// <summary>Requires <paramref name="entity"/>.Playbook to be loaded.</summary>
    public static ScanScheduleResponse FromEntity(ScanSchedule entity) => new(
        entity.Id,
        entity.EngagementId,
        entity.TargetId,
        entity.PlaybookId,
        entity.Playbook!.Slug,
        entity.CronExpression,
        entity.Enabled,
        entity.CreatedBy,
        entity.CreatedAt,
        entity.LastFiredAt,
        entity.LastFireOutcome,
        entity.LastSkipReason,
        entity.LastTriggeredScanJobId);
}
