using StrikeShield.Domain.Enums;

namespace StrikeShield.Domain.Entities;

/// <summary>
/// One generated audience-specific document for an Engagement
/// (docs/PHASED_PLAN.md Phase 7) — the Reporting Agent's output. Stores
/// both the Markdown source (the Reporting Agent's structured, validated
/// content, rendered deterministically by ReportMarkdownBuilder — no
/// per-report-type schema surprises) and the final rendered PDF bytes
/// (Markdown -> HTML -> PDF via Playwright print-to-PDF). Every GET
/// request regenerates and stores a fresh row rather than caching — see
/// IReportGenerationService.
/// </summary>
public class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EngagementId { get; set; }
    public Engagement? Engagement { get; set; }

    public ReportType Type { get; set; }

    public string MarkdownContent { get; set; } = string.Empty;
    public byte[] PdfContent { get; set; } = Array.Empty<byte>();

    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}
