using Hangfire;
using StrikeShield.Application.Scheduling;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Infrastructure.Scheduling;

/// <summary>
/// The real IScanScheduleRegistrar (docs/PHASED_PLAN.md Phase 8) — backs
/// every ScanSchedule with a Hangfire recurring job whose id is derived
/// from the ScanSchedule's own Id, firing IScanScheduleService.FireAsync
/// on the configured cadence. AddOrUpdate parses/validates the cron
/// expression synchronously and throws on malformed input — that's what
/// lets ScanScheduleService.CreateAsync call this before persisting the
/// schedule, so an invalid cron string is never saved.
/// </summary>
public class HangfireScanScheduleRegistrar : IScanScheduleRegistrar
{
    private readonly IRecurringJobManager _recurringJobManager;

    public HangfireScanScheduleRegistrar(IRecurringJobManager recurringJobManager)
    {
        _recurringJobManager = recurringJobManager;
    }

    public void Register(ScanSchedule schedule)
    {
        var scheduleId = schedule.Id;

        _recurringJobManager.AddOrUpdate<IScanScheduleService>(
            RecurringJobId(scheduleId),
            service => service.FireAsync(scheduleId, CancellationToken.None),
            schedule.CronExpression);
    }

    public void Remove(Guid scheduleId)
    {
        _recurringJobManager.RemoveIfExists(RecurringJobId(scheduleId));
    }

    private static string RecurringJobId(Guid scheduleId) => $"scan-schedule-{scheduleId}";
}
