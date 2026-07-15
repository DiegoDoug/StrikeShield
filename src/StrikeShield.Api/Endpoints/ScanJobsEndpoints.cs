using StrikeShield.Application.Findings;
using StrikeShield.Application.Playbooks;
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

        // Raw Nuclei (or any future tool's) output is retrievable via the
        // artifacts nested in each step here — see docs/PHASED_PLAN.md
        // Phase 2 acceptance criteria.
        group.MapGet("/{id:guid}/steps", async (Guid id, IScanJobService service, CancellationToken ct) =>
            Results.Ok(await service.GetStepsAsync(id, ct)));

        // Normalized findings across every tool that ran in this ScanJob —
        // see docs/PHASED_PLAN.md Phase 3 acceptance criteria.
        group.MapGet("/{id:guid}/findings", async (Guid id, IFindingService service, CancellationToken ct) =>
            Results.Ok(await service.GetForScanJobAsync(id, ct)));

        // Everything a recon step discovered (subdomains/URLs/hosts/ports) —
        // see docs/PHASED_PLAN.md Phase 5 acceptance criteria.
        group.MapGet("/{id:guid}/assets", async (Guid id, IScanJobService service, CancellationToken ct) =>
            Results.Ok(await service.GetAssetsAsync(id, ct)));

        // The Adaptive Planner's pending/decided proposals for this
        // ScanJob (docs/PHASED_PLAN.md Phase 6) — never auto-applied; see
        // the approve/reject endpoints below.
        group.MapGet("/{id:guid}/amendments", async (Guid id, IPlaybookAmendmentService service, CancellationToken ct) =>
            Results.Ok(await service.GetForScanJobAsync(id, ct)));

        // Approving substitutes the amendment's proposed ArgsTemplate for
        // the target step only for this ScanJob (the shared Playbook
        // template is never mutated) and, if the job was paused awaiting
        // this decision, re-queues it so the Orchestrator resumes.
        group.MapPost(
            "/{id:guid}/amendments/{amendmentId:guid}/approve",
            async (Guid id, Guid amendmentId, DecideAmendmentRequest request, IPlaybookAmendmentService service, CancellationToken ct) =>
                Results.Ok(await service.ApproveAsync(id, amendmentId, request, ct)));

        // Rejecting also un-pauses the job — the target step just runs
        // with its original, unmodified ArgsTemplate.
        group.MapPost(
            "/{id:guid}/amendments/{amendmentId:guid}/reject",
            async (Guid id, Guid amendmentId, DecideAmendmentRequest request, IPlaybookAmendmentService service, CancellationToken ct) =>
                Results.Ok(await service.RejectAsync(id, amendmentId, request, ct)));
    }
}
