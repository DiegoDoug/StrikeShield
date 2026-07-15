using StrikeShield.Application.Targets;

namespace StrikeShield.Api.Endpoints;

public static class TargetsEndpoints
{
    public static void MapTargetsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/targets").WithTags("Targets").RequireAuthorization();

        group.MapPost("/", async (CreateTargetRequest request, ITargetService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/api/targets/{created.Id}", created);
        });

        group.MapGet("/", async (Guid? projectId, ITargetService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(projectId, ct)));

        group.MapGet("/{id:guid}", async (Guid id, ITargetService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(id, ct)));

        group.MapPut("/{id:guid}", async (Guid id, UpdateTargetRequest request, ITargetService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(id, request, ct)));

        group.MapDelete("/{id:guid}", async (Guid id, ITargetService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }
}
