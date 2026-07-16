using Microsoft.EntityFrameworkCore;
using StrikeShield.Application.Common;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Integrations;

/// <summary>
/// CRUD for the notification channels NotificationDispatcher fans a
/// completed ScanJob out to (docs/PHASED_PLAN.md Phase 8,
/// docs/ARCHITECTURE.md §4: "Integration ── Organization").
/// </summary>
public class IntegrationService : IIntegrationService
{
    private readonly IAppDbContext _db;

    public IntegrationService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IntegrationResponse> CreateAsync(CreateIntegrationRequest request, CancellationToken cancellationToken = default)
    {
        var organizationExists = await _db.Organizations.AnyAsync(o => o.Id == request.OrganizationId, cancellationToken);
        if (!organizationExists)
        {
            throw new NotFoundException($"Organization '{request.OrganizationId}' was not found.");
        }

        ValidateChannelConfig(request.Type, request.WebhookUrl, request.GitHubRepository, request.GitHubAccessToken);

        var integration = new Integration
        {
            OrganizationId = request.OrganizationId,
            Type = request.Type,
            NotifyOnScanCompletion = request.NotifyOnScanCompletion,
            NotifyOnCriticalFinding = request.NotifyOnCriticalFinding,
            WebhookUrl = request.WebhookUrl,
            GitHubRepository = request.GitHubRepository,
            GitHubAccessToken = request.GitHubAccessToken
        };

        _db.Integrations.Add(integration);
        await _db.SaveChangesAsync(cancellationToken);

        return IntegrationResponse.FromEntity(integration);
    }

    public async Task<IReadOnlyList<IntegrationResponse>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var integrations = await _db.Integrations
            .Where(i => i.OrganizationId == organizationId)
            .OrderBy(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        return integrations.Select(IntegrationResponse.FromEntity).ToList();
    }

    public async Task<IntegrationResponse> UpdateAsync(Guid id, UpdateIntegrationRequest request, CancellationToken cancellationToken = default)
    {
        var integration = await _db.Integrations.FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Integration '{id}' was not found.");

        // A blank token in the request means "leave it as-is" — updating
        // notification toggles shouldn't force re-entering a secret.
        var effectiveGitHubToken = string.IsNullOrWhiteSpace(request.GitHubAccessToken)
            ? integration.GitHubAccessToken
            : request.GitHubAccessToken;

        ValidateChannelConfig(integration.Type, request.WebhookUrl, request.GitHubRepository, effectiveGitHubToken);

        integration.Enabled = request.Enabled;
        integration.NotifyOnScanCompletion = request.NotifyOnScanCompletion;
        integration.NotifyOnCriticalFinding = request.NotifyOnCriticalFinding;
        integration.WebhookUrl = request.WebhookUrl;
        integration.GitHubRepository = request.GitHubRepository;
        integration.GitHubAccessToken = effectiveGitHubToken;

        await _db.SaveChangesAsync(cancellationToken);

        return IntegrationResponse.FromEntity(integration);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var integration = await _db.Integrations.FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Integration '{id}' was not found.");

        _db.Integrations.Remove(integration);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateChannelConfig(
        IntegrationType type,
        string? webhookUrl,
        string? gitHubRepository,
        string? gitHubAccessToken)
    {
        switch (type)
        {
            case IntegrationType.Slack:
            case IntegrationType.Webhook:
                if (string.IsNullOrWhiteSpace(webhookUrl))
                {
                    throw new AppValidationException($"WebhookUrl is required for a {type} integration.");
                }

                break;
            case IntegrationType.GitHub:
                if (string.IsNullOrWhiteSpace(gitHubRepository) || string.IsNullOrWhiteSpace(gitHubAccessToken))
                {
                    throw new AppValidationException("GitHubRepository and GitHubAccessToken are required for a GitHub integration.");
                }

                break;
        }
    }
}
