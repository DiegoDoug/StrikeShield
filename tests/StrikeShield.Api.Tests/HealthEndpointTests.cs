using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StrikeShield.Api.Tests;

/// <summary>
/// Phase 0 acceptance test: proves the API can reach Postgres, not just that
/// it boots. Requires a reachable Postgres at ConnectionStrings:Postgres
/// (default: docker compose's "postgres" service, or override via the
/// ConnectionStrings__Postgres env var — see docs/PHASED_PLAN.md Phase 0).
/// </summary>
public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_ReportsHealthyPostgresCheck()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"name\":\"postgres\"", body);
        Assert.Contains("\"status\":\"Healthy\"", body);
    }
}
