using StrikeShield.Application.Organizations;

namespace StrikeShield.Api.Endpoints;

public static class OrganizationsEndpoints
{
    public static void MapOrganizationsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/organizations").WithTags("Organizations").RequireAuthorization();

        group.MapGet("/", async (IOrganizationService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(ct)));
    }
}
