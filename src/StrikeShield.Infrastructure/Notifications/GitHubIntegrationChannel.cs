using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using StrikeShield.Application.Notifications;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Infrastructure.Notifications;

/// <summary>
/// Files one GitHub issue per new critical finding (docs/PHASED_PLAN.md
/// Phase 8: "GitHub issue creation as an optional integration"). Never
/// fires on plain scan completion — an issue with nothing actionable in it
/// isn't useful the way a critical-finding one is, so SendScanCompletedAsync
/// is a deliberate no-op (NotificationDispatcher doesn't call it for
/// GitHub integrations anyway, but the interface contract stays honest).
/// </summary>
public class GitHubIntegrationChannel : IIntegrationChannel
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubIntegrationChannel> _logger;

    public GitHubIntegrationChannel(HttpClient httpClient, ILogger<GitHubIntegrationChannel> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public IntegrationType Type => IntegrationType.GitHub;

    public Task SendScanCompletedAsync(
        Integration integration,
        ScanCompletionNotification notification,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task SendCriticalFindingsAsync(
        Integration integration,
        ScanCompletionNotification notification,
        IReadOnlyList<CriticalFindingNotification> findings,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(integration.GitHubRepository) || string.IsNullOrWhiteSpace(integration.GitHubAccessToken))
        {
            return;
        }

        foreach (var finding in findings)
        {
            await CreateIssueAsync(integration, notification, finding, cancellationToken);
        }
    }

    private async Task CreateIssueAsync(
        Integration integration,
        ScanCompletionNotification notification,
        CriticalFindingNotification finding,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.github.com/repos/{integration.GitHubRepository}/issues");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", integration.GitHubAccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("StrikeShield", "1.0"));

        var body = $"**Severity:** Critical{(finding.CvssScore is not null ? $" (CVSS {finding.CvssScore:0.0})" : string.Empty)}\n" +
            $"**Source tool:** {finding.SourceTool}\n" +
            $"**Affected asset:** {finding.AffectedAsset}\n" +
            $"**Engagement:** {notification.EngagementName}\n" +
            $"**ScanJob:** {notification.ScanJobId}\n\n" +
            (finding.Description ?? "No description provided by the source tool.") +
            "\n\n_Filed automatically by StrikeShield (docs/PHASED_PLAN.md Phase 8)._";

        request.Content = JsonContent.Create(new
        {
            title = $"[StrikeShield][Critical] {finding.Title}",
            body,
            labels = new[] { "security", "strikeshield" }
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "GitHub issue creation for finding {FindingId} returned {StatusCode}.",
                finding.FindingId,
                response.StatusCode);
        }
    }
}
