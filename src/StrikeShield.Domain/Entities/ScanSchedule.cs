using StrikeShield.Domain.Enums;

namespace StrikeShield.Domain.Entities;

/// <summary>
/// A cron-scheduled recurring ScanJob launch against one Engagement/Target/
/// Playbook combination (docs/PHASED_PLAN.md Phase 8 — "cron-based
/// recurring ScanJobs per Engagement"). Hangfire fires
/// IScanScheduleService.FireAsync(Id) on this cadence; the Engagement's
/// scope/approval gate (Engagement.CheckAuthorizedForScan) is re-checked at
/// every fire, not just at schedule-creation time, so an expired or
/// not-yet-approved Engagement makes every subsequent fire skip (and record
/// why) instead of silently re-running forever or needing the schedule to
/// be manually disabled.
/// </summary>
public class ScanSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EngagementId { get; set; }
    public Engagement? Engagement { get; set; }

    public Guid TargetId { get; set; }
    public Target? Target { get; set; }

    public Guid PlaybookId { get; set; }
    public Playbook? Playbook { get; set; }

    /// <summary>Standard 5-field cron expression, evaluated in UTC.</summary>
    public string CronExpression { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastFiredAt { get; set; }
    public ScanScheduleFireOutcome? LastFireOutcome { get; set; }
    public string? LastSkipReason { get; set; }
    public Guid? LastTriggeredScanJobId { get; set; }
}
