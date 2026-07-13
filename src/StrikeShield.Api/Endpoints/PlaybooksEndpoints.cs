using StrikeShield.Application.Playbooks;

namespace StrikeShield.Api.Endpoints;

public static class PlaybooksEndpoints
{
    public static void MapPlaybooksEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/playbooks").WithTags("Playbooks").RequireAuthorization();

        group.MapGet("/", async (IPlaybookService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(ct)));

        group.MapGet("/{id:guid}", async (Guid id, IPlaybookService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(id, ct)));
    }
}
