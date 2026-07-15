using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Playbooks;

public record PlaybookAmendmentResponse(
    Guid Id,
    Guid ScanJobId,
    Guid ProposedByStepRunId,
    Guid TargetPlaybookStepId,
    string TargetStepKey,
    string Rationale,
    string ProposedArgsTemplate,
    PlaybookAmendmentStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DecidedAt,
    string? DecidedBy)
{
    /// <summary>Requires TargetPlaybookStep to be loaded.</summary>
    public static PlaybookAmendmentResponse FromEntity(PlaybookAmendment entity) => new(
        entity.Id,
        entity.ScanJobId,
        entity.ProposedByStepRunId,
        entity.TargetPlaybookStepId,
        entity.TargetPlaybookStep!.StepKey,
        entity.Rationale,
        entity.ProposedArgsTemplate,
        entity.Status,
        entity.CreatedAt,
        entity.DecidedAt,
        entity.DecidedBy);
}

public record DecideAmendmentRequest(string DecidedBy);
