using Microsoft.EntityFrameworkCore;
using StrikeShield.Application.Common;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.ScanJobs;

/// <summary>
/// The one launch path every scan request goes through. Phase 1 only
/// stubs execution (a ScanJob is created as Queued and nothing runs it
/// yet — that's Phase 2's Docker orchestrator), but the scope/authorization
/// gate below is enforced exactly as it will be for every future step type,
/// including Strix. See docs/ARCHITECTURE.md §4/§8.
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

        var authorization = engagement.CheckAuthorizedForScan(DateTimeOffset.UtcNow);
        if (!authorization.IsAuthorized)
        {
            throw new ForbiddenException(authorization.Reason ?? "Engagement is not authorized for scanning.");
        }

        var scanJob = new ScanJob
        {
            EngagementId = engagement.Id,
            TargetId = target.Id,
            PlaybookName = request.PlaybookName.Trim(),
            Status = ScanJobStatus.Queued
        };

        _db.ScanJobs.Add(scanJob);
        await _db.SaveChangesAsync(cancellationToken);

        return ScanJobResponse.FromEntity(scanJob);
    }

    public async Task<ScanJobResponse> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var scanJob = await _db.ScanJobs.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException($"ScanJob '{id}' was not found.");

        return ScanJobResponse.FromEntity(scanJob);
    }

    public async Task<IReadOnlyList<ScanJobResponse>> GetAllAsync(Guid? engagementId, CancellationToken cancellationToken = default)
    {
        var query = _db.ScanJobs.AsQueryable();
        if (engagementId is not null)
        {
            query = query.Where(s => s.EngagementId == engagementId);
        }

        var scanJobs = await query.OrderBy(s => s.CreatedAt).ToListAsync(cancellationToken);
        return scanJobs.Select(ScanJobResponse.FromEntity).ToList();
    }
}
