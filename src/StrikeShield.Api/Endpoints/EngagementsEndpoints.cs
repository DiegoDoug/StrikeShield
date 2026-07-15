using StrikeShield.Application.Engagements;

namespace StrikeShield.Api.Endpoints;

public static class EngagementsEndpoints
{
    public static void MapEngagementsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/engagements").WithTags("Engagements").RequireAuthorization();

        group.MapPost("/", async (CreateEngagementRequest request, IEngagementService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/api/engagements/{created.Id}", created);
        });

        group.MapGet("/", async (Guid? projectId, IEngagementService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(projectId, ct)));

        group.MapGet("/{id:guid}", async (Guid id, IEngagementService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(id, ct)));

        group.MapPost("/{id:guid}/approve", async (Guid id, ApproveEngagementRequest request, IEngagementService service, CancellationToken ct) =>
            Results.Ok(await service.ApproveAsync(id, request, ct)));
    }
}
