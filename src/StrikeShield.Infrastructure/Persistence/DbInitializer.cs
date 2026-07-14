using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StrikeShield.Application.Auth;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Infrastructure.Persistence;

/// <summary>
/// Applies pending EF Core migrations and seeds exactly one Organization +
/// one Owner AppUser on first boot, so Phase 1's acceptance test can log in
/// and create a Client/Project/Target/Engagement immediately after
/// `docker compose up` with no manual DB setup. Full multi-tenant
/// organization management arrives in Phase 10 — see docs/PHASED_PLAN.md.
///
/// Phase 2 additionally seeds the "nuclei-quick" Playbook (a single Nuclei
/// step) so the Orchestrator has something to run out of the box. Phase 3
/// adds "full-baseline" (Nuclei + ZAP + nmap) to prove cross-tool finding
/// normalization/correlation with genuinely different native formats.
/// </summary>
public static class DbInitializer
{
    public const string NucleiQuickPlaybookSlug = "nuclei-quick";
    public const string FullBaselinePlaybookSlug = "full-baseline";

    // Arbitrary fixed key for a Postgres session-level advisory lock. Any
    // process calling MigrateAndSeedAsync concurrently against the same
    // database — multiple WebApplicationFactory-built test hosts in one
    // xUnit run (different collections run in parallel), or multiple
    // replicas of this API starting up together in a real deployment —
    // serializes on this lock instead of racing to CREATE TABLE
    // "__EFMigrationsHistory" (which fails for all but one racer with a
    // Postgres catalog-level duplicate-key error).
    private const long MigrationLockKey = 958301001;

    public static async Task MigrateAndSeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var provider = scope.ServiceProvider;

        var dbContext = provider.GetRequiredService<StrikeShieldDbContext>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbInitializer));
        var configuration = provider.GetRequiredService<IConfiguration>();

        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            await ExecuteNonQueryAsync(connection, $"SELECT pg_advisory_lock({MigrationLockKey})");

            logger.LogInformation("Applying pending EF Core migrations...");
            await dbContext.Database.MigrateAsync();

            await SeedOrganizationAndAdminAsync(dbContext, configuration, provider, logger);
            await SeedNucleiQuickPlaybookAsync(dbContext, logger);
            await SeedFullBaselinePlaybookAsync(dbContext, logger);
        }
        finally
        {
            await ExecuteNonQueryAsync(connection, $"SELECT pg_advisory_unlock({MigrationLockKey})");
            await connection.CloseAsync();
        }
    }

    private static async Task ExecuteNonQueryAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SeedOrganizationAndAdminAsync(
        StrikeShieldDbContext dbContext,
        IConfiguration configuration,
        IServiceProvider provider,
        ILogger logger)
    {
        if (await dbContext.Organizations.AnyAsync())
        {
            return;
        }

        var passwordHasher = provider.GetRequiredService<IPasswordHasherService>();

        var seedOrgName = configuration["SeedAdmin:OrganizationName"] ?? "StrikeShield";
        var seedEmail = (configuration["SeedAdmin:Email"] ?? "admin@strikeshield.local").Trim().ToLowerInvariant();
        var seedPassword = configuration["SeedAdmin:Password"] ?? "ChangeMe123!";

        var organization = new Organization
        {
            Name = seedOrgName
        };
        dbContext.Organizations.Add(organization);

        var adminUser = new AppUser
        {
            OrganizationId = organization.Id,
            Email = seedEmail,
            Role = UserRole.Owner
        };
        adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, seedPassword);
        dbContext.Users.Add(adminUser);

        await dbContext.SaveChangesAsync();

        logger.LogWarning(
            "Seeded organization {OrganizationName} and admin user {Email}. " +
            "If this is not a throwaway dev instance, set SeedAdmin:Password (or the " +
            "SeedAdmin__Password env var) before first boot and rotate it afterwards.",
            seedOrgName,
            seedEmail);
    }

    private static async Task SeedNucleiQuickPlaybookAsync(StrikeShieldDbContext dbContext, ILogger logger)
    {
        if (await dbContext.Playbooks.AnyAsync(p => p.Slug == NucleiQuickPlaybookSlug))
        {
            return;
        }

        var playbook = new Playbook
        {
            Slug = NucleiQuickPlaybookSlug,
            Name = "Nuclei Quick Scan",
            Description = "A single-step playbook that runs a Nuclei template scan against the target."
        };

        playbook.Steps.Add(new PlaybookStep
        {
            PlaybookId = playbook.Id,
            Order = 1,
            ToolName = "nuclei",
            ImageRepository = "projectdiscovery/nuclei",
            ImageTag = "latest",
            ArgsTemplate = "-u {target} -jsonl -o {output} -severity critical,high,medium",
            // Generous timeout: Nuclei's first-ever run in a fresh
            // container also downloads its template set, which can take a
            // while depending on connection speed.
            TimeoutSeconds = 600,
            // The (unpinned) nuclei-templates set has been observed at
            // 6000+ templates and growing every release; 512MB was
            // observed OOM-killing the container (exit 137) mid-scan.
            MemoryLimitBytes = 1024L * 1024 * 1024,
            NanoCpus = 1_000_000_000L
        });

        dbContext.Playbooks.Add(playbook);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Seeded playbook '{Slug}'.", NucleiQuickPlaybookSlug);
    }

    private static async Task SeedFullBaselinePlaybookAsync(StrikeShieldDbContext dbContext, ILogger logger)
    {
        if (await dbContext.Playbooks.AnyAsync(p => p.Slug == FullBaselinePlaybookSlug))
        {
            return;
        }

        var playbook = new Playbook
        {
            Slug = FullBaselinePlaybookSlug,
            Name = "Full Baseline Scan",
            Description = "Runs Nuclei, an OWASP ZAP baseline scan, and an nmap vuln-script scan " +
                "against the target, normalizing all three into one Finding table (docs/PHASED_PLAN.md Phase 3)."
        };

        playbook.Steps.Add(new PlaybookStep
        {
            PlaybookId = playbook.Id,
            Order = 1,
            ToolName = "nuclei",
            ImageRepository = "projectdiscovery/nuclei",
            ImageTag = "latest",
            ArgsTemplate = "-u {target} -jsonl -o {output} -severity critical,high,medium",
            TimeoutSeconds = 600,
            MemoryLimitBytes = 1024L * 1024 * 1024,
            NanoCpus = 1_000_000_000L
        });

        playbook.Steps.Add(new PlaybookStep
        {
            PlaybookId = playbook.Id,
            Order = 2,
            ToolName = "zap",
            ImageRepository = "ghcr.io/zaproxy/zaproxy",
            ImageTag = "stable",
            // -I: don't fail the container on WARN-level alerts — findings
            // are the expected, desired output of a baseline scan, not an
            // execution error. -J's path wins over ZAP's own /zap/wrk/
            // base dir because it's given as an absolute path here.
            ArgsTemplate = "zap-baseline.py -t {target} -J {output} -I",
            TimeoutSeconds = 900,
            MemoryLimitBytes = 1024L * 1024 * 1024,
            NanoCpus = 1_000_000_000L
        });

        playbook.Steps.Add(new PlaybookStep
        {
            PlaybookId = playbook.Id,
            Order = 3,
            ToolName = "nmap",
            ImageRepository = "instrumentisto/nmap",
            ImageTag = "latest",
            // {targetHost}: nmap needs a bare host/IP, not a scheme+port URL.
            ArgsTemplate = "-oX {output} -T4 --script vuln {targetHost}",
            TimeoutSeconds = 600,
            MemoryLimitBytes = 512L * 1024 * 1024,
            NanoCpus = 1_000_000_000L
        });

        dbContext.Playbooks.Add(playbook);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Seeded playbook '{Slug}'.", FullBaselinePlaybookSlug);
    }
}
