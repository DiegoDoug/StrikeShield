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
/// step) so the Orchestrator has something to run out of the box.
/// </summary>
public static class DbInitializer
{
    public const string NucleiQuickPlaybookSlug = "nuclei-quick";

    public static async Task MigrateAndSeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var provider = scope.ServiceProvider;

        var dbContext = provider.GetRequiredService<StrikeShieldDbContext>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbInitializer));
        var configuration = provider.GetRequiredService<IConfiguration>();

        logger.LogInformation("Applying pending EF Core migrations...");
        await dbContext.Database.MigrateAsync();

        await SeedOrganizationAndAdminAsync(dbContext, configuration, provider, logger);
        await SeedNucleiQuickPlaybookAsync(dbContext, logger);
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
            MemoryLimitBytes = 512L * 1024 * 1024,
            NanoCpus = 1_000_000_000L
        });

        dbContext.Playbooks.Add(playbook);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Seeded playbook '{Slug}'.", NucleiQuickPlaybookSlug);
    }
}
