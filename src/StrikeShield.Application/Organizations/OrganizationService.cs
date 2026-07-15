using Microsoft.EntityFrameworkCore;
using StrikeShield.Application.Common;

namespace StrikeShield.Application.Organizations;

/// <summary>
/// Read-only in Phase 1 — organizations are seeded at startup (see
/// StrikeShield.Infrastructure.Persistence.DbInitializer). Full
/// organization management arrives with multi-tenancy in Phase 10.
/// </summary>
public class OrganizationService : IOrganizationService
{
    private readonly IAppDbContext _db;

    public OrganizationService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<OrganizationResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var organizations = await _db.Organizations
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return organizations.Select(OrganizationResponse.FromEntity).ToList();
    }
}
