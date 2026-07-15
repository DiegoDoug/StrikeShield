namespace StrikeShield.Application.Reporting;

/// <summary>
/// Renders a report's Markdown source into a final PDF (docs/PHASED_PLAN.md
/// Phase 7: "Markdown -> HTML -> PDF render pipeline (Playwright
/// print-to-PDF)"). The Markdown -> HTML step is an implementation detail
/// of whatever renders the PDF — callers only see Markdown in, PDF bytes
/// out.
/// </summary>
public interface IReportRenderer
{
    Task<byte[]> RenderPdfAsync(string markdown, CancellationToken cancellationToken = default);
}
