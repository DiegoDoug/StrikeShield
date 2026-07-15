using StrikeShield.Application.Engagements;
using StrikeShield.Application.Reporting;

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

        // The Reporting Agent (docs/PHASED_PLAN.md Phase 7) — regenerates
        // and returns a fresh PDF on every call rather than caching. "type"
        // is one of executive|technical|dev-remediation|compliance.
        group.MapGet("/{id:guid}/reports/{type}", async Task<IResult> (Guid id, string type, IReportGenerationService service, CancellationToken ct) =>
        {
            if (!ReportTypeParser.TryParse(type, out var reportType))
            {
                return Results.BadRequest(new
                {
                    error = $"Unknown report type '{type}'. Expected one of: executive, technical, dev-remediation, compliance."
                });
            }

            var report = await service.GenerateAsync(id, reportType, ct);
            return Results.File(report.PdfContent, "application/pdf", $"{ReportTypeParser.ToRouteSegment(report.Type)}-report.pdf");
        });
    }
}
