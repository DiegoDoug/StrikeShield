using Microsoft.EntityFrameworkCore;
using StrikeShield.Application.Common;

namespace StrikeShield.Application.Findings;

public class FindingService : IFindingService
{
    private readonly IAppDbContext _db;

    public FindingService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<FindingResponse>> GetForScanJobAsync(Guid scanJobId, CancellationToken cancellationToken = default)
    {
        var scanJobExists = await _db.ScanJobs.AnyAsync(s => s.Id == scanJobId, cancellationToken);
        if (!scanJobExists)
        {
            throw new NotFoundException($"ScanJob '{scanJobId}' was not found.");
        }

        var findings = await _db.Findings
            .Where(f => f.ScanJobId == scanJobId)
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.FirstSeenAt)
            .ToListAsync(cancellationToken);

        return findings.Select(FindingResponse.FromEntity).ToList();
    }

    public async Task<IReadOnlyList<FindingResponse>> GetForEngagementAsync(Guid engagementId, CancellationToken cancellationToken = default)
    {
        var engagementExists = await _db.Engagements.AnyAsync(e => e.Id == engagementId, cancellationToken);
        if (!engagementExists)
        {
            throw new NotFoundException($"Engagement '{engagementId}' was not found.");
        }

        var findings = await _db.Findings
            .Where(f => f.ScanJob!.EngagementId == engagementId)
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.FirstSeenAt)
            .ToListAsync(cancellationToken);

        return findings.Select(FindingResponse.FromEntity).ToList();
    }

    public async Task<FindingResponse> UpdateStatusAsync(Guid id, UpdateFindingStatusRequest request, CancellationToken cancellationToken = default)
    {
        var finding = await _db.Findings.FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Finding '{id}' was not found.");

        finding.Status = request.Status;

        await _db.SaveChangesAsync(cancellationToken);

        return FindingResponse.FromEntity(finding);
    }
}
