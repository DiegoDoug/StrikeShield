using StrikeShield.Domain.Enums;

namespace StrikeShield.Domain.Entities;

/// <summary>
/// A notification channel configured per-Organization (docs/ARCHITECTURE.md
/// §4 data model: "Integration (Slack/Jira/GitHub/Webhook) ── Organization").
/// NotificationDispatcher fires these on scan completion and on new
/// critical findings (docs/PHASED_PLAN.md Phase 8) — delivery failures are
/// logged and swallowed, never allowed to fail the ScanJob that triggered
/// them.
/// </summary>
public class Integration
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public IntegrationType Type { get; set; }
    public bool Enabled { get; set; } = true;

    public bool NotifyOnScanCompletion { get; set; } = true;
    public bool NotifyOnCriticalFinding { get; set; } = true;

    /// <summary>Slack incoming-webhook URL (Type == Slack) or the generic Webhook target URL (Type == Webhook).</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>"owner/repo" — required when Type == GitHub.</summary>
    public string? GitHubRepository { get; set; }
    public string? GitHubAccessToken { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
