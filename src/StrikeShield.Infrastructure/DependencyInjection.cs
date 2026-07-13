using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StrikeShield.Infrastructure.HealthChecks;
using StrikeShield.Infrastructure.Persistence;

namespace StrikeShield.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Missing required connection string 'ConnectionStrings:Postgres'.");

        services.AddDbContext<StrikeShieldDbContext>(options =>
            options.UseNpgsql(connectionString));

        services
            .AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres");

        return services;
    }
}
