using StrikeShield.Application.Integrations;

namespace StrikeShield.Api.Endpoints;

/// <summary>
/// Per-Organization notification channels — Slack, generic webhook, GitHub
/// issue creation (docs/PHASED_PLAN.md Phase 8, docs/ARCHITECTURE.md §4:
/// "Integration ── Organization"). NotificationDispatcher fires these on
/// scan completion / new critical findings.
/// </summary>
public static class IntegrationsEndpoints
{
    public static void MapIntegrationsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/integrations").WithTags("Integrations").RequireAuthorization();

        group.MapPost("/", async (CreateIntegrationRequest request, IIntegrationService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/api/integrations/{created.Id}", created);
        });

        group.MapGet("/", async (Guid organizationId, IIntegrationService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(organizationId, ct)));

        group.MapPut("/{id:guid}", async (Guid id, UpdateIntegrationRequest request, IIntegrationService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(id, request, ct)));

        group.MapDelete("/{id:guid}", async (Guid id, IIntegrationService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }
}
