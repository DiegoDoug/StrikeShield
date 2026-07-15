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
}
