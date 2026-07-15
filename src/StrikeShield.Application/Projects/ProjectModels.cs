using StrikeShield.Domain.Entities;

namespace StrikeShield.Application.Projects;

public record CreateProjectRequest(Guid ClientId, string Name);

public record UpdateProjectRequest(string Name);

public record ProjectResponse(Guid Id, Guid ClientId, string Name, DateTimeOffset CreatedAt)
{
    public static ProjectResponse FromEntity(Project entity) =>
        new(entity.Id, entity.ClientId, entity.Name, entity.CreatedAt);
}
