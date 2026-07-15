using Markdig;
using Microsoft.Playwright;
using StrikeShield.Application.Reporting;

namespace StrikeShield.Infrastructure.Reporting;

/// <summary>
/// Markdown -> HTML -> PDF (docs/PHASED_PLAN.md Phase 7): Markdig renders
/// the Reporting Agent's Markdown into a minimally-styled HTML document,
/// then headless Chromium (via Playwright) prints that to PDF. A fresh
/// Playwright instance/browser is created per call — report generation is
/// a low-frequency, on-demand operation, so the simplicity of not managing
/// a shared long-lived browser process outweighs the ~1-2s startup cost.
/// Requires Chromium to already be installed in the runtime image (see
/// src/StrikeShield.Api/Dockerfile, which uses Playwright's own base image
/// for exactly this).
/// </summary>
public class PlaywrightReportRenderer : IReportRenderer
{
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public async Task<byte[]> RenderPdfAsync(string markdown, CancellationToken cancellationToken = default)
    {
        var html = BuildHtmlDocument(markdown);

        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            // Chromium's own sandbox needs privileges this container
            // doesn't grant when running as root — the standard Docker
            // workaround, same tradeoff every containerized Playwright/
            // Puppeteer setup makes (there's no other untrusted content
            // rendered here; the input is our own generated Markdown).
            Args = new[] { "--no-sandbox" }
        });

        try
        {
            var page = await browser.NewPageAsync();
            await page.SetContentAsync(html, new PageSetContentOptions { WaitUntil = WaitUntilState.NetworkIdle });

            return await page.PdfAsync(new PagePdfOptions
            {
                Format = "A4",
                PrintBackground = true,
                Margin = new Margin { Top = "20mm", Bottom = "20mm", Left = "15mm", Right = "15mm" }
            });
        }
        finally
        {
            await browser.CloseAsync();
        }
    }

    private static string BuildHtmlDocument(string markdown)
    {
        var body = Markdown.ToHtml(markdown, MarkdownPipeline);

        return $$"""
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset="utf-8" />
        <style>
          body { font-family: -apple-system, "Segoe UI", Arial, sans-serif; font-size: 11pt; color: #1a1a1a; line-height: 1.5; }
          h1, h2, h3 { color: #0f172a; }
          table { border-collapse: collapse; width: 100%; margin: 12px 0; }
          th, td { border: 1px solid #cbd5e1; padding: 6px 8px; text-align: left; font-size: 9.5pt; }
          th { background: #f1f5f9; }
          code, pre { background: #f8fafc; border-radius: 4px; font-family: "SFMono-Regular", Consolas, monospace; }
          pre { padding: 8px; overflow-x: auto; }
        </style>
        </head>
        <body>
        {{body}}
        </body>
        </html>
        """;
    }
}
