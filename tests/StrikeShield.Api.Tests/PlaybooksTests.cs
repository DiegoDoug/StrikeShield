using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using StrikeShield.Application.Auth;
using StrikeShield.Application.Playbooks;
using Xunit;

namespace StrikeShield.Api.Tests;

/// <summary>
/// Confirms DbInitializer's Phase 2 seed step actually ran: the
/// "nuclei-quick" playbook (and its single Nuclei step) must exist without
/// any manual setup, straight off a fresh `docker compose up`.
/// </summary>
public class PlaybooksTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly WebApplicationFactory<Program> _factory;

    public PlaybooksTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NucleiQuickPlaybook_IsSeededWithOneStep()
    {
        var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin@strikeshield.local", "ChangeMe123!"),
            JsonOptions);
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var playbooks = await client.GetFromJsonAsync<List<PlaybookResponse>>("/api/playbooks", JsonOptions);
        Assert.NotNull(playbooks);

        var nucleiQuick = Assert.Single(playbooks!, p => p.Slug == "nuclei-quick");
        var step = Assert.Single(nucleiQuick.Steps);
        Assert.Equal("nuclei", step.ToolName);
        Assert.Equal("projectdiscovery/nuclei", step.ImageRepository);
    }
}
