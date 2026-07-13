using StrikeShield.Application.ScanJobs;

namespace StrikeShield.Api.Endpoints;

public static class ScanJobsEndpoints
{
    public static void MapScanJobsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/scan-jobs").WithTags("ScanJobs").RequireAuthorization();

        // 202 Accepted on success: the scope/authorization gate in
        // ScanJobService throws ForbiddenException (-> 403) before this ever
        // returns if the Engagement isn't approved and in-window.
        group.MapPost("/", async (CreateScanJobRequest request, IScanJobService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return Results.Accepted($"/api/scan-jobs/{created.Id}", created);
        });

        group.MapGet("/", async (Guid? engagementId, IScanJobService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(engagementId, ct)));

        group.MapGet("/{id:guid}", async (Guid id, IScanJobService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(id, ct)));
    }
}
