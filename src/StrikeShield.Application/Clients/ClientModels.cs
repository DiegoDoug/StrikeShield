using StrikeShield.Domain.Entities;

namespace StrikeShield.Application.Clients;

public record CreateClientRequest(Guid OrganizationId, string Name);

public record UpdateClientRequest(string Name);

public record ClientResponse(Guid Id, Guid OrganizationId, string Name, DateTimeOffset CreatedAt)
{
    public static ClientResponse FromEntity(Client entity) =>
        new(entity.Id, entity.OrganizationId, entity.Name, entity.CreatedAt);
}
