using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Integrations;

public record CreateIntegrationRequest(
    Guid OrganizationId,
    IntegrationType Type,
    bool NotifyOnScanCompletion,
    bool NotifyOnCriticalFinding,
    string? WebhookUrl,
    string? GitHubRepository,
    string? GitHubAccessToken);

public record UpdateIntegrationRequest(
    bool Enabled,
    bool NotifyOnScanCompletion,
    bool NotifyOnCriticalFinding,
    string? WebhookUrl,
    string? GitHubRepository,
    string? GitHubAccessToken);

/// <summary>
/// GitHubAccessToken is deliberately never echoed back — HasGitHubAccessToken
/// tells the caller one is configured without leaking the secret itself.
/// </summary>
public record IntegrationResponse(
    Guid Id,
    Guid OrganizationId,
    IntegrationType Type,
    bool Enabled,
    bool NotifyOnScanCompletion,
    bool NotifyOnCriticalFinding,
    string? WebhookUrl,
    string? GitHubRepository,
    bool HasGitHubAccessToken,
    DateTimeOffset CreatedAt)
{
    public static IntegrationResponse FromEntity(Integration entity) => new(
        entity.Id,
        entity.OrganizationId,
        entity.Type,
        entity.Enabled,
        entity.NotifyOnScanCompletion,
        entity.NotifyOnCriticalFinding,
        entity.WebhookUrl,
        entity.GitHubRepository,
        !string.IsNullOrEmpty(entity.GitHubAccessToken),
        entity.CreatedAt);
}
