namespace StrikeShield.Application.Scheduling;

public interface IScanScheduleService
{
    Task<ScanScheduleResponse> CreateAsync(CreateScanScheduleRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScanScheduleResponse>> GetAllAsync(Guid? engagementId, CancellationToken cancellationToken = default);

    Task<ScanScheduleResponse> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ScanScheduleResponse> SetEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invoked by the recurring background job on this schedule's cadence
    /// (docs/PHASED_PLAN.md Phase 8). Re-checks the Engagement's scope/
    /// approval gate at fire time (not just at schedule-creation time) and
    /// either enqueues a Queued ScanJob or records why the fire was
    /// skipped — an expired or not-yet-approved Engagement skips instead
    /// of throwing, so one bad fire doesn't fail the whole recurring job.
    /// </summary>
    Task FireAsync(Guid id, CancellationToken cancellationToken = default);
}
