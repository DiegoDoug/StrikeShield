using Microsoft.EntityFrameworkCore;
using StrikeShield.Application.Common;

namespace StrikeShield.Application.Playbooks;

/// <summary>
/// Read-only in Phase 2 — playbooks are seeded at startup (see
/// StrikeShield.Infrastructure.Persistence.DbInitializer). An editor for
/// creating/customizing playbooks arrives with the DAG engine in Phase 5.
/// </summary>
public class PlaybookService : IPlaybookService
{
    private readonly IAppDbContext _db;

    public PlaybookService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PlaybookResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var playbooks = await _db.Playbooks
            .Include(p => p.Steps)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        return playbooks.Select(PlaybookResponse.FromEntity).ToList();
    }

    public async Task<PlaybookResponse> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var playbook = await _db.Playbooks
            .Include(p => p.Steps)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Playbook '{id}' was not found.");

        return PlaybookResponse.FromEntity(playbook);
    }
}
