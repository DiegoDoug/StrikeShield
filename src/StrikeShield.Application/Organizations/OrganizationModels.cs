using StrikeShield.Domain.Entities;

namespace StrikeShield.Application.Organizations;

public record OrganizationResponse(Guid Id, string Name, DateTimeOffset CreatedAt)
{
    public static OrganizationResponse FromEntity(Organization entity) =>
        new(entity.Id, entity.Name, entity.CreatedAt);
}
