using StrikeShield.Application.Projects;

namespace StrikeShield.Api.Endpoints;

public static class ProjectsEndpoints
{
    public static void MapProjectsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/projects").WithTags("Projects").RequireAuthorization();

        group.MapPost("/", async (CreateProjectRequest request, IProjectService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/api/projects/{created.Id}", created);
        });

        group.MapGet("/", async (Guid? clientId, IProjectService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(clientId, ct)));

        group.MapGet("/{id:guid}", async (Guid id, IProjectService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(id, ct)));

        group.MapPut("/{id:guid}", async (Guid id, UpdateProjectRequest request, IProjectService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(id, request, ct)));

        group.MapDelete("/{id:guid}", async (Guid id, IProjectService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }
}
