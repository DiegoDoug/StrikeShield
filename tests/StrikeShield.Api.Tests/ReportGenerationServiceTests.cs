using Microsoft.EntityFrameworkCore;
using StrikeShield.Application.Common;
using StrikeShield.Application.Reporting;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;
using StrikeShield.Infrastructure.Persistence;
using Xunit;

namespace StrikeShield.Api.Tests;

/// <summary>
/// Phase 7 acceptance test (docs/PHASED_PLAN.md): "an automated
/// schema-validation test asserts every finding in the technical report
/// contains all required fields (fails the build if the LLM response was
/// incomplete, rather than shipping a partial report)." Uses a fake
/// ILlmClient with a known, hand-crafted response — not a live call — the
/// same approach as the Phase 6 fixture tests.
/// </summary>
public class ReportGenerationServiceTests
{
    private static StrikeShieldDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<StrikeShieldDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new StrikeShieldDbContext(options);
    }

    private static async Task<(Engagement Engagement, Finding Finding)> SeedEngagementWithOneFindingAsync(StrikeShieldDbContext db)
    {
        var engagement = new Engagement { Name = "Test Engagement", ScopeStart = DateTimeOffset.UtcNow, ScopeEnd = DateTimeOffset.UtcNow.AddDays(7) };
        var scanJob = new ScanJob { EngagementId = engagement.Id, TargetId = Guid.NewGuid(), PlaybookId = Guid.NewGuid() };
        var finding = new Finding
        {
            ScanJobId = scanJob.Id,
            StepRunId = Guid.NewGuid(),
            SourceTool = "nuclei",
            Title = "Reflected XSS",
            Severity = FindingSeverity.High,
            AffectedAsset = "http://juice-shop:3000/",
            DedupeFingerprint = "fp-1"
        };

        db.Engagements.Add(engagement);
        db.ScanJobs.Add(scanJob);
        db.Findings.Add(finding);
        await db.SaveChangesAsync();

        return (engagement, finding);
    }

    [Fact]
    public async Task GenerateAsync_ProducesAReport_WhenLlmCoversEveryFinding()
    {
        await using var db = CreateInMemoryDbContext();
        var (engagement, finding) = await SeedEngagementWithOneFindingAsync(db);

        var fakeResponse = $$"""
        {"summary": "Overall risk is manageable.", "findingNarratives": [{"findingId": "{{finding.Id}}", "businessImpact": "Session hijacking risk.", "riskRating": "High"}]}
        """;
        var renderer = new FakeReportRenderer();
        var service = new ReportGenerationService(db, new FakeLlmClient(fakeResponse), renderer);

        var report = await service.GenerateAsync(engagement.Id, ReportType.Technical, CancellationToken.None);

        Assert.Equal(ReportType.Technical, report.Type);
        Assert.Contains("Session hijacking risk.", report.MarkdownContent);
        Assert.NotEmpty(report.PdfContent);
        Assert.NotNull(renderer.LastMarkdown);

        var persisted = await db.Reports.SingleAsync();
        Assert.Equal(report.Id, persisted.Id);
    }

    [Fact]
    public async Task GenerateAsync_Throws_WhenLlmResponseOmitsAFinding()
    {
        await using var db = CreateInMemoryDbContext();
        var (engagement, _) = await SeedEngagementWithOneFindingAsync(db);

        // No findingNarratives at all — the one seeded finding is left uncovered.
        var fakeResponse = "{\"summary\": \"Overall risk is manageable.\", \"findingNarratives\": []}";
        var service = new ReportGenerationService(db, new FakeLlmClient(fakeResponse), new FakeReportRenderer());

        var ex = await Assert.ThrowsAsync<AppValidationException>(
            () => service.GenerateAsync(engagement.Id, ReportType.Technical, CancellationToken.None));
        Assert.Contains("missing businessImpact/riskRating", ex.Message);

        Assert.Empty(db.Reports);
    }

    [Fact]
    public async Task GenerateAsync_Throws_WhenNarrativeFieldsAreBlank()
    {
        await using var db = CreateInMemoryDbContext();
        var (engagement, finding) = await SeedEngagementWithOneFindingAsync(db);

        var fakeResponse = $$"""
        {"summary": "Overall risk is manageable.", "findingNarratives": [{"findingId": "{{finding.Id}}", "businessImpact": "", "riskRating": "High"}]}
        """;
        var service = new ReportGenerationService(db, new FakeLlmClient(fakeResponse), new FakeReportRenderer());

        await Assert.ThrowsAsync<AppValidationException>(
            () => service.GenerateAsync(engagement.Id, ReportType.Technical, CancellationToken.None));
    }

    [Fact]
    public async Task GenerateAsync_Throws_WhenNoLlmKeyIsConfigured()
    {
        await using var db = CreateInMemoryDbContext();
        var (engagement, _) = await SeedEngagementWithOneFindingAsync(db);

        var service = new ReportGenerationService(db, new FakeLlmClient(response: null, isConfigured: false), new FakeReportRenderer());

        var ex = await Assert.ThrowsAsync<AppValidationException>(
            () => service.GenerateAsync(engagement.Id, ReportType.Executive, CancellationToken.None));
        Assert.Contains("AiOrchestration:LlmApiKey", ex.Message);
    }

    [Fact]
    public async Task GenerateAsync_Throws_WhenEngagementHasNoFindingsYet()
    {
        await using var db = CreateInMemoryDbContext();
        var engagement = new Engagement { Name = "Empty Engagement", ScopeStart = DateTimeOffset.UtcNow, ScopeEnd = DateTimeOffset.UtcNow.AddDays(7) };
        db.Engagements.Add(engagement);
        await db.SaveChangesAsync();

        var service = new ReportGenerationService(db, new FakeLlmClient("irrelevant"), new FakeReportRenderer());

        await Assert.ThrowsAsync<AppValidationException>(
            () => service.GenerateAsync(engagement.Id, ReportType.Executive, CancellationToken.None));
    }

    [Fact]
    public async Task GenerateAsync_Throws_WhenEngagementDoesNotExist()
    {
        await using var db = CreateInMemoryDbContext();
        var service = new ReportGenerationService(db, new FakeLlmClient("irrelevant"), new FakeReportRenderer());

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GenerateAsync(Guid.NewGuid(), ReportType.Executive, CancellationToken.None));
    }
}
