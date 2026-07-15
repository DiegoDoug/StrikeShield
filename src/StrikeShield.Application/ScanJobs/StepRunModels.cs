using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.ScanJobs;

public record ArtifactResponse(Guid Id, string FileName, string ContentType, string Content, DateTimeOffset CreatedAt)
{
    public static ArtifactResponse FromEntity(Artifact entity) =>
        new(entity.Id, entity.FileName, entity.ContentType, entity.Content, entity.CreatedAt);
}

public record StepRunResponse(
    Guid Id,
    Guid ScanJobId,
    Guid PlaybookStepId,
    string ToolName,
    StepRunStatus Status,
    long? ExitCode,
    string? ErrorMessage,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<ArtifactResponse> Artifacts)
{
    /// <summary>Requires PlaybookStep and Artifacts to be loaded.</summary>
    public static StepRunResponse FromEntity(StepRun entity) => new(
        entity.Id,
        entity.ScanJobId,
        entity.PlaybookStepId,
        entity.PlaybookStep!.ToolName,
        entity.Status,
        entity.ExitCode,
        entity.ErrorMessage,
        entity.StartedAt,
        entity.CompletedAt,
        entity.Artifacts.Select(ArtifactResponse.FromEntity).ToList());
}
