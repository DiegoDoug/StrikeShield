using System.Text;
using StrikeShield.Application.Reporting;

namespace StrikeShield.Api.Tests;

/// <summary>
/// Test double for IReportRenderer — no real Playwright/Chromium
/// dependency in unit tests (docs/PHASED_PLAN.md Phase 7). Returns the
/// UTF-8 bytes of the Markdown it was given so tests can assert on
/// rendered content without needing a real PDF parser.
/// </summary>
public class FakeReportRenderer : IReportRenderer
{
    public string? LastMarkdown { get; private set; }

    public Task<byte[]> RenderPdfAsync(string markdown, CancellationToken cancellationToken = default)
    {
        LastMarkdown = markdown;
        return Task.FromResult(Encoding.UTF8.GetBytes(markdown));
    }
}
