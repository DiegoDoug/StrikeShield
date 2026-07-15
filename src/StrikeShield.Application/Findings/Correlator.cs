using Microsoft.EntityFrameworkCore;
using StrikeShield.Application.Common;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Application.Findings;

public class Correlator : ICorrelator
{
    private readonly IAppDbContext _db;

    public Correlator(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<int> CorrelateAsync(Guid scanJobId, CancellationToken cancellationToken = default)
    {
        var findings = await _db.Findings
            .Where(f => f.ScanJobId == scanJobId)
            .ToListAsync(cancellationToken);

        var groupsCreated = 0;

        foreach (var group in findings.GroupBy(f => f.DedupeFingerprint))
        {
            var members = group.ToList();
            if (members.Count < 2)
            {
                continue;
            }

            // Some members may already belong to a group from an earlier
            // correlation pass (e.g. a re-run) — reuse the first one found
            // instead of creating duplicate groups for the same fingerprint.
            var existingGroupId = members.Select(f => f.CorrelationGroupId).FirstOrDefault(id => id is not null);

            Guid groupId;
            if (existingGroupId is { } id)
            {
                groupId = id;
            }
            else
            {
                var correlationGroup = new CorrelationGroup();
                _db.CorrelationGroups.Add(correlationGroup);
                groupId = correlationGroup.Id;
                groupsCreated++;
            }

            foreach (var finding in members)
            {
                finding.CorrelationGroupId = groupId;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return groupsCreated;
    }
}
