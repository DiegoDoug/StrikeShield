using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using StrikeShield.Application.Notifications;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Infrastructure.Notifications;

/// <summary>
/// Posts to a Slack incoming-webhook URL (docs/PHASED_PLAN.md Phase 8).
/// Slack's incoming-webhook contract is a single JSON body with a "text"
/// field — no auth header, the URL itself is the secret.
/// </summary>
public class SlackIntegrationChannel : IIntegrationChannel
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SlackIntegrationChannel> _logger;

    public SlackIntegrationChannel(HttpClient httpClient, ILogger<SlackIntegrationChannel> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public IntegrationType Type => IntegrationType.Slack;

    public Task SendScanCompletedAsync(
        Integration integration,
        ScanCompletionNotification notification,
        CancellationToken cancellationToken = default)
    {
        var text = $":mag: *ScanJob {notification.Status}* — playbook `{notification.PlaybookName}` against " +
            $"`{notification.TargetValue}` (engagement \"{notification.EngagementName}\"). " +
            $"{notification.FindingsCount} finding(s), {notification.CriticalFindingsCount} critical.";

        return PostAsync(integration.WebhookUrl, text, cancellationToken);
    }

    public Task SendCriticalFindingsAsync(
        Integration integration,
        ScanCompletionNotification notification,
        IReadOnlyList<CriticalFindingNotification> findings,
        CancellationToken cancellationToken = default)
    {
        var lines = findings.Select(f =>
            $"• *{f.Title}* on `{f.AffectedAsset}` ({f.SourceTool}{(f.CvssScore is not null ? $", CVSS {f.CvssScore:0.0}" : string.Empty)})");

        var text = $":rotating_light: *{findings.Count} critical finding(s)* in playbook `{notification.PlaybookName}` " +
            $"against `{notification.TargetValue}` (engagement \"{notification.EngagementName}\"):\n{string.Join("\n", lines)}";

        return PostAsync(integration.WebhookUrl, text, cancellationToken);
    }

    private async Task PostAsync(string? webhookUrl, string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return;
        }

        using var response = await _httpClient.PostAsJsonAsync(webhookUrl, new { text }, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Slack webhook returned {StatusCode}.", response.StatusCode);
        }
    }
}
