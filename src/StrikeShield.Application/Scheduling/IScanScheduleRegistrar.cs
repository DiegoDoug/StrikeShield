using StrikeShield.Domain.Entities;

namespace StrikeShield.Application.Scheduling;

/// <summary>
/// Registers/removes the recurring background-job trigger backing a
/// ScanSchedule (docs/PHASED_PLAN.md Phase 8 — Hangfire recurring jobs).
/// Kept as a seam so the Application layer never references Hangfire
/// directly — StrikeShield.Infrastructure's HangfireScanScheduleRegistrar
/// is the only real implementation, the same "seam in Application, real
/// implementation in Infrastructure" pattern as ILlmClient/IReportRenderer.
/// </summary>
public interface IScanScheduleRegistrar
{
    /// <summary>
    /// Idempotent add-or-update. Throws if <paramref name="schedule"/>.CronExpression
    /// isn't valid cron syntax — called before the schedule is persisted so
    /// nothing is saved on failure.
    /// </summary>
    void Register(ScanSchedule schedule);

    void Remove(Guid scheduleId);
}
