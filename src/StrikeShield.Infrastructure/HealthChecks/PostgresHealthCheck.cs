using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StrikeShield.Infrastructure.Persistence;

namespace StrikeShield.Infrastructure.HealthChecks;

/// <summary>
/// Verifies the API can actually reach Postgres, rather than reporting a
/// hardcoded "Healthy". Used by the Phase 0 acceptance test: a broken DB
/// connection string must surface here, not just at first query time.
/// </summary>
public class PostgresHealthCheck : IHealthCheck
{
    private readonly StrikeShieldDbContext _dbContext;

    public PostgresHealthCheck(StrikeShieldDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Postgres connection succeeded.")
                : HealthCheckResult.Unhealthy("Postgres connection failed.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Postgres connection threw an exception.", ex);
        }
    }
}
