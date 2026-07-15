using StrikeShield.Domain.Entities;

namespace StrikeShield.Application.Engagements;

public record CreateEngagementRequest(
    Guid ProjectId,
    string Name,
    string? RulesOfEngagement,
    string? AuthorizationEvidenceUri,
    DateTimeOffset ScopeStart,
    DateTimeOffset ScopeEnd,
    List<string>? AllowedScopeRules);

public record ApproveEngagementRequest(string ApprovedBy);

public record EngagementResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? RulesOfEngagement,
    string? AuthorizationEvidenceUri,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset ScopeStart,
    DateTimeOffset ScopeEnd,
    List<string> AllowedScopeRules,
    bool IsApproved,
    DateTimeOffset CreatedAt)
{
    public static EngagementResponse FromEntity(Engagement entity) => new(
        entity.Id,
        entity.ProjectId,
        entity.Name,
        entity.RulesOfEngagement,
        entity.AuthorizationEvidenceUri,
        entity.ApprovedBy,
        entity.ApprovedAt,
        entity.ScopeStart,
        entity.ScopeEnd,
        entity.AllowedScopeRules,
        entity.IsApproved,
        entity.CreatedAt);
}
