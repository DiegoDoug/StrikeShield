using Microsoft.EntityFrameworkCore;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Application.Common;

/// <summary>
/// Persistence seam: Application depends on this abstraction, not on EF
/// Core's DbContext or Npgsql directly. StrikeShield.Infrastructure's
/// StrikeShieldDbContext implements it.
/// </summary>
public interface IAppDbContext
{
    DbSet<Organization> Organizations { get; }
    DbSet<AppUser> Users { get; }
    DbSet<Client> Clients { get; }
    DbSet<Project> Projects { get; }
    DbSet<Target> Targets { get; }
    DbSet<Engagement> Engagements { get; }
    DbSet<Playbook> Playbooks { get; }
    DbSet<PlaybookStep> PlaybookSteps { get; }
    DbSet<ScanJob> ScanJobs { get; }
    DbSet<StepRun> StepRuns { get; }
    DbSet<Artifact> Artifacts { get; }
    DbSet<Finding> Findings { get; }
    DbSet<FindingEvidence> FindingEvidence { get; }
    DbSet<Asset> Assets { get; }
    DbSet<CorrelationGroup> CorrelationGroups { get; }
    DbSet<PlaybookAmendment> PlaybookAmendments { get; }
    DbSet<Report> Reports { get; }
    DbSet<ScanSchedule> ScanSchedules { get; }
    DbSet<Integration> Integrations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
