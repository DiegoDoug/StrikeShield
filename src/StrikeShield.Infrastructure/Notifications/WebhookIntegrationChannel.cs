using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using StrikeShield.Application.Notifications;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Infrastructure.Notifications;

/// <summary>
/// Posts a generic structured JSON payload to a configured URL
/// (docs/PHASED_PLAN.md Phase 8) — unlike Slack's fixed "text" contract,
/// this carries the full notification so an arbitrary receiver (a local
/// test receiver, an internal automation endpoint) can act on it.
/// </summary>
public class WebhookIntegrationChannel : IIntegrationChannel
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebhookIntegrationChannel> _logger;

    public WebhookIntegrationChannel(HttpClient httpClient, ILogger<WebhookIntegrationChannel> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public IntegrationType Type => IntegrationType.Webhook;

    public Task SendScanCompletedAsync(
        Integration integration,
        ScanCompletionNotification notification,
        CancellationToken cancellationToken = default) =>
        PostAsync(integration.WebhookUrl, new { @event = "scan.completed", notification }, cancellationToken);

    public Task SendCriticalFindingsAsync(
        Integration integration,
        ScanCompletionNotification notification,
        IReadOnlyList<CriticalFindingNotification> findings,
        CancellationToken cancellationToken = default) =>
        PostAsync(integration.WebhookUrl, new { @event = "finding.critical", notification, findings }, cancellationToken);

    private async Task PostAsync(string? webhookUrl, object payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return;
        }

        using var response = await _httpClient.PostAsJsonAsync(webhookUrl, payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Generic webhook returned {StatusCode}.", response.StatusCode);
        }
    }
}
