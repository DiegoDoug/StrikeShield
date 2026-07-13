using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Targets;

public record CreateTargetRequest(Guid ProjectId, TargetType Type, string Value);

public record UpdateTargetRequest(TargetType Type, string Value);

public record TargetResponse(Guid Id, Guid ProjectId, TargetType Type, string Value, DateTimeOffset CreatedAt)
{
    public static TargetResponse FromEntity(Target entity) =>
        new(entity.Id, entity.ProjectId, entity.Type, entity.Value, entity.CreatedAt);
}
