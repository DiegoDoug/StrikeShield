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
/// </summary>
public static class DbInitializer
{
    public static async Task MigrateAndSeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var provider = scope.ServiceProvider;

        var dbContext = provider.GetRequiredService<StrikeShieldDbContext>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbInitializer));

        logger.LogInformation("Applying pending EF Core migrations...");
        await dbContext.Database.MigrateAsync();

        if (await dbContext.Organizations.AnyAsync())
        {
            logger.LogInformation("Seed data already present, skipping.");
            return;
        }

        var configuration = provider.GetRequiredService<IConfiguration>();
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
}
