using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using StrikeShield.Application.Auth;
using StrikeShield.Application.Clients;
using StrikeShield.Application.Engagements;
using StrikeShield.Application.Organizations;
using StrikeShield.Application.Projects;
using StrikeShield.Application.ScanJobs;
using StrikeShield.Application.Targets;
using StrikeShield.Domain.Enums;
using Xunit;

namespace StrikeShield.Api.Tests;

/// <summary>
/// Phase 1 acceptance test (docs/PHASED_PLAN.md): create client -> project
/// -> target -> engagement (unapproved); a scan-job request against it must
/// be rejected with 403; approving the engagement must then let the same
/// request through with 202. This is the one non-negotiable behavior from
/// docs/ARCHITECTURE.md §4/§8 — no active scan without an approved,
/// in-window engagement.
/// </summary>
public class ScopeGateTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly WebApplicationFactory<Program> _factory;

    public ScopeGateTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ScanJob_IsRejectedUntilEngagementIsApproved_ThenAccepted()
    {
        var client = _factory.CreateClient();
        await AuthenticateAsync(client);

        var organizations = await client.GetFromJsonAsync<List<OrganizationResponse>>("/api/organizations", JsonOptions);
        Assert.NotNull(organizations);
        Assert.NotEmpty(organizations!);
        var organizationId = organizations![0].Id;

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];

        var createdClient = await PostAsync<ClientResponse>(
            client,
            "/api/clients",
            new CreateClientRequest(organizationId, $"Acme Corp {uniqueSuffix}"));

        var project = await PostAsync<ProjectResponse>(
            client,
            "/api/projects",
            new CreateProjectRequest(createdClient.Id, $"Q3 External Pentest {uniqueSuffix}"));

        var target = await PostAsync<TargetResponse>(
            client,
            "/api/targets",
            new CreateTargetRequest(project.Id, TargetType.Url, "https://juice-shop.example.test"));

        var now = DateTimeOffset.UtcNow;
        var engagement = await PostAsync<EngagementResponse>(
            client,
            "/api/engagements",
            new CreateEngagementRequest(
                project.Id,
                $"July Engagement {uniqueSuffix}",
                "Standard web app rules of engagement.",
                "https://example.test/authorization-letter.pdf",
                now.AddMinutes(-5),
                now.AddDays(7),
                new List<string> { "*.example.test" }));

        Assert.False(engagement.IsApproved);

        // 1. Unapproved engagement -> scan-job request must be rejected.
        var rejected = await client.PostAsJsonAsync(
            "/api/scan-jobs",
            new CreateScanJobRequest(engagement.Id, target.Id, "nuclei-quick"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);

        // 2. Approve the engagement.
        var approved = await PostAsync<EngagementResponse>(
            client,
            $"/api/engagements/{engagement.Id}/approve",
            new ApproveEngagementRequest("qa-lead@strikeshield.local"));
        Assert.True(approved.IsApproved);

        // 3. The same scan-job request must now be accepted.
        var accepted = await client.PostAsJsonAsync(
            "/api/scan-jobs",
            new CreateScanJobRequest(engagement.Id, target.Id, "nuclei-quick"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);

        var scanJob = await accepted.Content.ReadFromJsonAsync<ScanJobResponse>(JsonOptions);
        Assert.NotNull(scanJob);
        Assert.Equal(ScanJobStatus.Queued, scanJob!.Status);
    }

    private static async Task AuthenticateAsync(HttpClient client)
    {
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin@strikeshield.local", "ChangeMe123!"),
            JsonOptions);

        loginResponse.EnsureSuccessStatusCode();

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(auth);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body, JsonOptions);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        Assert.NotNull(result);
        return result!;
    }
}
