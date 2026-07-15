using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Playbooks;

public record PlaybookStepResponse(
    Guid Id,
    int Order,
    string StepKey,
    string ToolName,
    string ImageRepository,
    string ImageTag,
    int TimeoutSeconds,
    IReadOnlyList<string> DependsOn,
    StepCondition Condition)
{
    public static PlaybookStepResponse FromEntity(PlaybookStep entity) => new(
        entity.Id,
        entity.Order,
        entity.StepKey,
        entity.ToolName,
        entity.ImageRepository,
        entity.ImageTag,
        entity.TimeoutSeconds,
        entity.DependsOn,
        entity.Condition);
}

public record PlaybookResponse(
    Guid Id,
    string Slug,
    string Name,
    string? Description,
    IReadOnlyList<PlaybookStepResponse> Steps)
{
    /// <summary>Requires Steps to be loaded.</summary>
    public static PlaybookResponse FromEntity(Playbook entity) => new(
        entity.Id,
        entity.Slug,
        entity.Name,
        entity.Description,
        entity.Steps.OrderBy(s => s.Order).Select(PlaybookStepResponse.FromEntity).ToList());
}
