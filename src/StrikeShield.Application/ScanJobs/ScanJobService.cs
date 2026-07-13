using Microsoft.EntityFrameworkCore;
using StrikeShield.Application.Common;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.ScanJobs;

/// <summary>
/// The one launch path every scan request goes through. The scope/
/// authorization gate below is enforced exactly the same way for every
/// step type, including Strix once it lands — see docs/ARCHITECTURE.md
/// §4/§8. Execution itself is picked up asynchronously by the
/// Orchestrator worker (Phase 2): this service only validates the
/// request, resolves the named Playbook, and records the job as Queued.
/// </summary>
public class ScanJobService : IScanJobService
{
    private readonly IAppDbContext _db;

    public ScanJobService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ScanJobResponse> CreateAsync(CreateScanJobRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PlaybookName))
        {
            throw new AppValidationException("PlaybookName is required.");
        }

        var engagement = await _db.Engagements.FirstOrDefaultAsync(e => e.Id == request.EngagementId, cancellationToken)
            ?? throw new NotFoundException($"Engagement '{request.EngagementId}' was not found.");

        var target = await _db.Targets.FirstOrDefaultAsync(t => t.Id == request.TargetId, cancellationToken)
            ?? throw new NotFoundException($"Target '{request.TargetId}' was not found.");

        if (target.ProjectId != engagement.ProjectId)
        {
            throw new AppValidationException(
                "Target does not belong to the same project as the engagement.");
        }

        var playbookSlug = request.PlaybookName.Trim();
        var playbook = await _db.Playbooks.FirstOrDefaultAsync(p => p.Slug == playbookSlug, cancellationToken)
            ?? throw new NotFoundException($"Playbook '{playbookSlug}' was not found.");

        var authorization = engagement.CheckAuthorizedForScan(DateTimeOffset.UtcNow);
        if (!authorization.IsAuthorized)
        {
            throw new ForbiddenException(authorization.Reason ?? "Engagement is not authorized for scanning.");
        }

        var scanJob = new ScanJob
        {
            EngagementId = engagement.Id,
            TargetId = target.Id,
            PlaybookId = playbook.Id,
            Status = ScanJobStatus.Queued
        };

        _db.ScanJobs.Add(scanJob);
        await _db.SaveChangesAsync(cancellationToken);

        scanJob.Playbook = playbook;
        return ScanJobResponse.FromEntity(scanJob);
    }

    public async Task<ScanJobResponse> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var scanJob = await _db.ScanJobs
            .Include(s => s.Playbook)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException($"ScanJob '{id}' was not found.");

        return ScanJobResponse.FromEntity(scanJob);
    }

    public async Task<IReadOnlyList<ScanJobResponse>> GetAllAsync(Guid? engagementId, CancellationToken cancellationToken = default)
    {
        var query = _db.ScanJobs.Include(s => s.Playbook).AsQueryable();
        if (engagementId is not null)
        {
            query = query.Where(s => s.EngagementId == engagementId);
        }

        var scanJobs = await query.OrderBy(s => s.CreatedAt).ToListAsync(cancellationToken);
        return scanJobs.Select(ScanJobResponse.FromEntity).ToList();
    }

    public async Task<IReadOnlyList<StepRunResponse>> GetStepsAsync(Guid scanJobId, CancellationToken cancellationToken = default)
    {
        var scanJobExists = await _db.ScanJobs.AnyAsync(s => s.Id == scanJobId, cancellationToken);
        if (!scanJobExists)
        {
            throw new NotFoundException($"ScanJob '{scanJobId}' was not found.");
        }

        var stepRuns = await _db.StepRuns
            .Include(s => s.PlaybookStep)
            .Include(s => s.Artifacts)
            .Where(s => s.ScanJobId == scanJobId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return stepRuns.Select(StepRunResponse.FromEntity).ToList();
    }
}
