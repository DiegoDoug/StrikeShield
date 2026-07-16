using StrikeShield.Application.Scheduling;

namespace StrikeShield.Api.Endpoints;

/// <summary>
/// Cron-based recurring ScanJobs per Engagement (docs/PHASED_PLAN.md
/// Phase 8). The scope/window gate is re-checked on every fire, not just
/// at creation time — see IScanScheduleService.FireAsync.
/// </summary>
public static class ScanSchedulesEndpoints
{
    public static void MapScanSchedulesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/scan-schedules").WithTags("ScanSchedules").RequireAuthorization();

        group.MapPost("/", async (CreateScanScheduleRequest request, IScanScheduleService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/api/scan-schedules/{created.Id}", created);
        });

        group.MapGet("/", async (Guid? engagementId, IScanScheduleService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(engagementId, ct)));

        group.MapGet("/{id:guid}", async (Guid id, IScanScheduleService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(id, ct)));

        group.MapPatch("/{id:guid}", async (Guid id, SetScanScheduleEnabledRequest request, IScanScheduleService service, CancellationToken ct) =>
            Results.Ok(await service.SetEnabledAsync(id, request.Enabled, ct)));

        group.MapDelete("/{id:guid}", async (Guid id, IScanScheduleService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }
}
