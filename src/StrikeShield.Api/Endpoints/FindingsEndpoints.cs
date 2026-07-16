using StrikeShield.Application.Findings;

namespace StrikeShield.Api.Endpoints;

/// <summary>
/// Cross-ScanJob finding access for the triage board (docs/PHASED_PLAN.md
/// Phase 9) — GET /api/scan-jobs/{id}/findings (ScanJobsEndpoints) stays the
/// per-job view; this group adds the engagement-wide roll-up plus the
/// status-transition endpoint the board's kanban actions call.
/// </summary>
public static class FindingsEndpoints
{
    public static void MapFindingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/findings").WithTags("Findings").RequireAuthorization();

        group.MapGet("/", async (Guid engagementId, IFindingService service, CancellationToken ct) =>
            Results.Ok(await service.GetForEngagementAsync(engagementId, ct)));

        group.MapPatch("/{id:guid}", async (Guid id, UpdateFindingStatusRequest request, IFindingService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateStatusAsync(id, request, ct)));
    }
}
