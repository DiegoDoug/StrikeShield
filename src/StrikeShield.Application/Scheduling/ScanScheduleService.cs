using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StrikeShield.Application.Common;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Scheduling;

/// <summary>
/// CRUD for ScanSchedules plus the fire-time gate every recurring scan
/// goes through (docs/PHASED_PLAN.md Phase 8) — the scheduling analogue of
/// ScanJobService's ad hoc launch path. Registration with the actual
/// recurring-job backend (Hangfire) is delegated to IScanScheduleRegistrar
/// so this class stays testable without a real scheduler.
/// </summary>
public class ScanScheduleService : IScanScheduleService
{
    private readonly IAppDbContext _db;
    private readonly IScanScheduleRegistrar _registrar;
    private readonly ILogger<ScanScheduleService> _logger;

    public ScanScheduleService(IAppDbContext db, IScanScheduleRegistrar registrar, ILogger<ScanScheduleService> logger)
    {
        _db = db;
        _registrar = registrar;
        _logger = logger;
    }

    public async Task<ScanScheduleResponse> CreateAsync(CreateScanScheduleRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PlaybookName))
        {
            throw new AppValidationException("PlaybookName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.CreatedBy))
        {
            throw new AppValidationException("CreatedBy is required.");
        }

        ValidateCronExpressionShape(request.CronExpression);

        var engagement = await _db.Engagements.FirstOrDefaultAsync(e => e.Id == request.EngagementId, cancellationToken)
            ?? throw new NotFoundException($"Engagement '{request.EngagementId}' was not found.");

        var target = await _db.Targets.FirstOrDefaultAsync(t => t.Id == request.TargetId, cancellationToken)
            ?? throw new NotFoundException($"Target '{request.TargetId}' was not found.");

        if (target.ProjectId != engagement.ProjectId)
        {
            throw new AppValidationException("Target does not belong to the same project as the engagement.");
        }

        var playbookSlug = request.PlaybookName.Trim();
        var playbook = await _db.Playbooks.FirstOrDefaultAsync(p => p.Slug == playbookSlug, cancellationToken)
            ?? throw new NotFoundException($"Playbook '{playbookSlug}' was not found.");

        var schedule = new ScanSchedule
        {
            EngagementId = engagement.Id,
            TargetId = target.Id,
            PlaybookId = playbook.Id,
            CronExpression = request.CronExpression.Trim(),
            CreatedBy = request.CreatedBy
        };

        // Registered before anything is persisted: a cron string that
        // passes the shape check above but the scheduler itself still
        // rejects means nothing gets saved.
        _registrar.Register(schedule);

        _db.ScanSchedules.Add(schedule);
        await _db.SaveChangesAsync(cancellationToken);

        schedule.Playbook = playbook;
        return ScanScheduleResponse.FromEntity(schedule);
    }

    public async Task<IReadOnlyList<ScanScheduleResponse>> GetAllAsync(Guid? engagementId, CancellationToken cancellationToken = default)
    {
        var query = _db.ScanSchedules.Include(s => s.Playbook).AsQueryable();
        if (engagementId is not null)
        {
            query = query.Where(s => s.EngagementId == engagementId);
        }

        var schedules = await query.OrderBy(s => s.CreatedAt).ToListAsync(cancellationToken);
        return schedules.Select(ScanScheduleResponse.FromEntity).ToList();
    }

    public async Task<ScanScheduleResponse> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var schedule = await _db.ScanSchedules
            .Include(s => s.Playbook)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException($"ScanSchedule '{id}' was not found.");

        return ScanScheduleResponse.FromEntity(schedule);
    }

    public async Task<ScanScheduleResponse> SetEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default)
    {
        var schedule = await _db.ScanSchedules
            .Include(s => s.Playbook)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException($"ScanSchedule '{id}' was not found.");

        schedule.Enabled = enabled;
        await _db.SaveChangesAsync(cancellationToken);

        return ScanScheduleResponse.FromEntity(schedule);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var schedule = await _db.ScanSchedules.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException($"ScanSchedule '{id}' was not found.");

        _registrar.Remove(schedule.Id);

        _db.ScanSchedules.Remove(schedule);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task FireAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var schedule = await _db.ScanSchedules
            .Include(s => s.Engagement)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (schedule is null)
        {
            // The recurring job outlived its ScanSchedule row (e.g. a
            // delete raced a fire) — Remove() should have unregistered it,
            // but a fire already in flight can still land here.
            _logger.LogWarning("ScanSchedule {ScheduleId} fired but no longer exists; skipping.", id);
            return;
        }

        if (!schedule.Enabled)
        {
            _logger.LogInformation("ScanSchedule {ScheduleId} is disabled; skipping fire.", id);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        schedule.LastFiredAt = now;

        // The one non-negotiable check (docs/ARCHITECTURE.md §4/§8),
        // re-evaluated on every single fire rather than once at schedule-
        // creation time — an Engagement that expires or is never approved
        // makes every subsequent fire skip on its own.
        var authorization = schedule.Engagement!.CheckAuthorizedForScan(now);
        if (!authorization.IsAuthorized)
        {
            schedule.LastFireOutcome = ScanScheduleFireOutcome.Skipped;
            schedule.LastSkipReason = authorization.Reason;

            _logger.LogWarning(
                "ScanSchedule {ScheduleId} skipped: {Reason}",
                id,
                authorization.Reason);

            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var scanJob = new ScanJob
        {
            EngagementId = schedule.EngagementId,
            TargetId = schedule.TargetId,
            PlaybookId = schedule.PlaybookId,
            Status = ScanJobStatus.Queued
        };
        _db.ScanJobs.Add(scanJob);

        schedule.LastFireOutcome = ScanScheduleFireOutcome.Triggered;
        schedule.LastSkipReason = null;
        schedule.LastTriggeredScanJobId = scanJob.Id;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "ScanSchedule {ScheduleId} triggered ScanJob {ScanJobId}.",
            id,
            scanJob.Id);
    }

    /// <summary>
    /// A cheap upfront check (exactly 5 whitespace-separated fields) so an
    /// obviously malformed cron string fails with a clear 400 before ever
    /// reaching IScanScheduleRegistrar — the registrar's own scheduler
    /// still does the authoritative syntax validation.
    /// </summary>
    private static void ValidateCronExpressionShape(string? cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            throw new AppValidationException("CronExpression is required.");
        }

        var fields = cronExpression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5)
        {
            throw new AppValidationException(
                "CronExpression must be a standard 5-field cron expression " +
                "(minute hour day-of-month month day-of-week), e.g. '*/5 * * * *'.");
        }
    }
}
