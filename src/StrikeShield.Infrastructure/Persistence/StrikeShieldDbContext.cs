using Microsoft.EntityFrameworkCore;
using StrikeShield.Application.Common;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Infrastructure.Persistence;

public class StrikeShieldDbContext : DbContext, IAppDbContext
{
    public StrikeShieldDbContext(DbContextOptions<StrikeShieldDbContext> options)
        : base(options)
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Target> Targets => Set<Target>();
    public DbSet<Engagement> Engagements => Set<Engagement>();
    public DbSet<ScanJob> ScanJobs => Set<ScanJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StrikeShieldDbContext).Assembly);
    }
}
