using Microsoft.EntityFrameworkCore;

namespace StrikeShield.Infrastructure.Persistence;

/// <summary>
/// Application database context. No entities yet in Phase 0 — the domain
/// model (Client/Project/Target/Engagement/...) lands in Phase 1. This
/// exists now so Postgres connectivity is provable end-to-end in Phase 0.
/// </summary>
public class StrikeShieldDbContext : DbContext
{
    public StrikeShieldDbContext(DbContextOptions<StrikeShieldDbContext> options)
        : base(options)
    {
    }
}
