using StrikeShield.Domain.Entities;

namespace StrikeShield.Application.Playbooks;

public record PlaybookStepResponse(
    Guid Id,
    int Order,
    string ToolName,
    string ImageRepository,
    string ImageTag,
    int TimeoutSeconds)
{
    public static PlaybookStepResponse FromEntity(PlaybookStep entity) => new(
        entity.Id,
        entity.Order,
        entity.ToolName,
        entity.ImageRepository,
        entity.ImageTag,
        entity.TimeoutSeconds);
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
