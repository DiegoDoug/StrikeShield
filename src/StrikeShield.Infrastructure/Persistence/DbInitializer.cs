using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StrikeShield.Application.Auth;
using StrikeShield.Application.Scheduling;
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
/// normalization/correlation with genuinely different native formats. Phase
/// 4 adds "strix-quick" (the AI pentesting agent, BYOK — see
/// Orchestrator's StrixLlmApiKey option). Phase 5 adds
/// "full-external-recon", a multi-tool DAG proving recon output (Assets)
/// feeds downstream steps' args — see PlaybookDagPlanner/StepArgsBuilder.
/// </summary>
public static class DbInitializer
{
    public const string NucleiQuickPlaybookSlug = "nuclei-quick";
    public const string FullBaselinePlaybookSlug = "full-baseline";
    public const string StrixQuickPlaybookSlug = "strix-quick";
    public const string FullExternalReconPlaybookSlug = "full-external-recon";

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
            await SeedStrixQuickPlaybookAsync(dbContext, logger);
            await SeedFullExternalReconPlaybookAsync(dbContext, logger);
            await SyncScanSchedulesAsync(dbContext, provider, logger);
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
            StepKey = "nuclei",
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
            StepKey = "nuclei",
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
            StepKey = "zap",
            ToolName = "zap",
            ImageRepository = "ghcr.io/zaproxy/zaproxy",
            ImageTag = "stable",
            // -I: don't fail the container on WARN-level alerts — findings
            // are the expected, desired output of a baseline scan, not an
            // execution error. {outputRelative} (not {output}): the
            // default automation-framework mode resolves -J relative to
            // /zap/wrk regardless of an absolute path (see
            // PlaybookExecutor.RunStepContainerAsync).
            ArgsTemplate = "zap-baseline.py -t {target} -J {outputRelative} -I",
            TimeoutSeconds = 900,
            MemoryLimitBytes = 1024L * 1024 * 1024,
            NanoCpus = 1_000_000_000L
        });

        playbook.Steps.Add(new PlaybookStep
        {
            PlaybookId = playbook.Id,
            Order = 3,
            StepKey = "nmap",
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

    private static async Task SeedStrixQuickPlaybookAsync(StrikeShieldDbContext dbContext, ILogger logger)
    {
        if (await dbContext.Playbooks.AnyAsync(p => p.Slug == StrixQuickPlaybookSlug))
        {
            return;
        }

        var playbook = new Playbook
        {
            Slug = StrixQuickPlaybookSlug,
            Name = "Strix Quick Scan",
            Description = "Runs the Strix AI pentesting agent (docs/ARCHITECTURE.md §3) against the target in " +
                "quick mode, budget-capped at $1.00. Requires the Orchestrator's StrixLlmApiKey to be configured " +
                "(BYOK — see .env.example) or this step will fail immediately with an auth error from the LLM provider."
        };

        playbook.Steps.Add(new PlaybookStep
        {
            PlaybookId = playbook.Id,
            Order = 1,
            StepKey = "strix",
            ToolName = "strix",
            // Built locally by `docker compose build` (docker-compose.yml's
            // strix-runner service) — Docker.DotNet skips the registry pull
            // for strikeshield/-prefixed images (see PlaybookExecutor).
            ImageRepository = "strikeshield/strix-runner",
            ImageTag = "latest",
            // No {output}/{outputRelative} placeholder: Strix has no flag
            // for its output path (always writes "./strix_runs/<run-name>/"
            // — see PlaybookExecutor.RunStepContainerAsync, which points
            // its cwd at our tracked per-step directory instead).
            ArgsTemplate = "-n --target {target} --scan-mode quick --max-budget-usd 1.00",
            // Generous timeout: this is an LLM-driven agent run (plus its
            // own nested sandbox container spinning up), not a fixed-time
            // scanner — "quick" mode still means several minutes at least.
            TimeoutSeconds = 1800,
            MemoryLimitBytes = 1024L * 1024 * 1024,
            NanoCpus = 1_000_000_000L
        });

        dbContext.Playbooks.Add(playbook);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Seeded playbook '{Slug}'.", StrixQuickPlaybookSlug);
    }

    /// <summary>
    /// Phase 5's DAG + asset hand-off showcase (docs/PHASED_PLAN.md): two
    /// independent recon steps (subfinder/katana) feed three downstream
    /// steps via "{assetsFile:&lt;AssetType&gt;}" — subfinder's discovered
    /// subdomains become nmap's host list, katana's discovered URLs become
    /// ffuf's/nuclei's input list. nikto runs standalone, same as
    /// full-baseline's steps. Best exercised against a real, authorized,
    /// multi-subdomain target — subfinder/katana won't discover anything
    /// interesting about an internal-only compose hostname like
    /// "juice-shop" that has no public DNS footprint.
    /// </summary>
    private static async Task SeedFullExternalReconPlaybookAsync(StrikeShieldDbContext dbContext, ILogger logger)
    {
        if (await dbContext.Playbooks.AnyAsync(p => p.Slug == FullExternalReconPlaybookSlug))
        {
            return;
        }

        var playbook = new Playbook
        {
            Slug = FullExternalReconPlaybookSlug,
            Name = "Full External Recon + Scan",
            Description = "Subfinder + Katana recon feeds nmap/ffuf/nuclei as a DAG (docs/PHASED_PLAN.md Phase 5): " +
                "subfinder's subdomains become nmap's target list, katana's crawled URLs become ffuf's and nuclei's " +
                "input list. Nikto runs standalone. Point this at a real, authorized, multi-subdomain domain — " +
                "subfinder/katana have nothing to find against an internal-only compose hostname."
        };

        playbook.Steps.Add(new PlaybookStep
        {
            PlaybookId = playbook.Id,
            Order = 1,
            StepKey = "subfinder",
            ToolName = "subfinder",
            ImageRepository = "projectdiscovery/subfinder",
            ImageTag = "latest",
            ArgsTemplate = "-d {targetHost} -silent -o {output}",
            TimeoutSeconds = 300,
            MemoryLimitBytes = 512L * 1024 * 1024,
            NanoCpus = 1_000_000_000L
        });

        playbook.Steps.Add(new PlaybookStep
        {
            PlaybookId = playbook.Id,
            Order = 2,
            StepKey = "katana",
            ToolName = "katana",
            ImageRepository = "projectdiscovery/katana",
            ImageTag = "latest",
            ArgsTemplate = "-u {target} -jsonl -silent -o {output}",
            TimeoutSeconds = 300,
            MemoryLimitBytes = 512L * 1024 * 1024,
            NanoCpus = 1_000_000_000L
        });

        playbook.Steps.Add(new PlaybookStep
        {
            PlaybookId = playbook.Id,
            Order = 3,
            StepKey = "nmap-recon",
            ToolName = "nmap",
            ImageRepository = "instrumentisto/nmap",
            ImageTag = "latest",
            DependsOn = new List<string> { "subfinder" },
            // {assetsFile:Subdomain}: nmap's -iL host list, built from
            // subfinder's discovered Subdomain Assets rather than a single
            // hardcoded {targetHost} (see StepArgsBuilder).
            ArgsTemplate = "-oX {output} -T4 --script vuln -iL {assetsFile:Subdomain}",
            TimeoutSeconds = 900,
            MemoryLimitBytes = 512L * 1024 * 1024,
            NanoCpus = 1_000_000_000L
        });

        playbook.Steps.Add(new PlaybookStep
        {
            PlaybookId = playbook.Id,
            Order = 4,
            StepKey = "ffuf",
            ToolName = "ffuf",
            ImageRepository = "ffuf/ffuf",
            ImageTag = "latest",
            DependsOn = new List<string> { "katana" },
            // {assetsFile:Url}: ffuf's wordlist, seeded from katana's
            // discovered URLs instead of a static wordlist file.
            ArgsTemplate = "-u {target}/FUZZ -w {assetsFile:Url} -of json -o {output}",
            TimeoutSeconds = 600,
            MemoryLimitBytes = 512L * 1024 * 1024,
            NanoCpus = 1_000_000_000L
        });

        playbook.Steps.Add(new PlaybookStep
        {
            PlaybookId = playbook.Id,
            Order = 5,
            StepKey = "nuclei-targeted",
            ToolName = "nuclei",
            ImageRepository = "projectdiscovery/nuclei",
            ImageTag = "latest",
            DependsOn = new List<string> { "katana" },
            // {assetsFile:Url}: nuclei's -l input list, built from katana's
            // discovered URLs instead of a single {target}.
            ArgsTemplate = "-l {assetsFile:Url} -jsonl -o {output} -severity critical,high,medium",
            TimeoutSeconds = 600,
            MemoryLimitBytes = 1024L * 1024 * 1024,
            NanoCpus = 1_000_000_000L
        });

        playbook.Steps.Add(new PlaybookStep
        {
            PlaybookId = playbook.Id,
            Order = 6,
            StepKey = "nikto",
            ToolName = "nikto",
            ImageRepository = "securecodebox/nikto",
            ImageTag = "latest",
            ArgsTemplate = "-h {target} -Format json -o {output}",
            TimeoutSeconds = 600,
            MemoryLimitBytes = 512L * 1024 * 1024,
            NanoCpus = 1_000_000_000L
        });

        dbContext.Playbooks.Add(playbook);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Seeded playbook '{Slug}'.", FullExternalReconPlaybookSlug);
    }

    /// <summary>
    /// Re-registers every enabled ScanSchedule's recurring job on startup
    /// (docs/PHASED_PLAN.md Phase 8). Hangfire recurring jobs are
    /// themselves persisted in its own Postgres storage, so this is
    /// normally a no-op restoring the same state — it only matters if that
    /// storage was ever reset independently of the ScanSchedules table
    /// (e.g. a restore that didn't cover both). AddOrUpdate is idempotent,
    /// so re-running this on every boot is cheap and safe.
    /// </summary>
    private static async Task SyncScanSchedulesAsync(StrikeShieldDbContext dbContext, IServiceProvider provider, ILogger logger)
    {
        var registrar = provider.GetRequiredService<IScanScheduleRegistrar>();

        var schedules = await dbContext.ScanSchedules.Where(s => s.Enabled).ToListAsync();
        foreach (var schedule in schedules)
        {
            registrar.Register(schedule);
        }

        if (schedules.Count > 0)
        {
            logger.LogInformation("Re-registered {Count} enabled ScanSchedule recurring job(s).", schedules.Count);
        }
    }
}
