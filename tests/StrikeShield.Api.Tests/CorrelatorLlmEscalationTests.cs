using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StrikeShield.Application.Ai;
using StrikeShield.Application.Findings;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;
using StrikeShield.Infrastructure.Persistence;
using Xunit;

namespace StrikeShield.Api.Tests;

/// <summary>
/// Phase 6 acceptance test (docs/PHASED_PLAN.md): "ambiguous cross-tool
/// matches (same target/severity, different everything else) get one
/// batched LLM call for a merge/no-merge + confidence judgment;
/// deterministic matches never touch the LLM." Uses a fake ILlmClient
/// returning a known, hand-crafted judgment — not a live LLM call, same
/// "fixture, not nondeterminism" approach as CorrelatorTests.cs.
/// </summary>
public class CorrelatorLlmEscalationTests
{
    private static StrikeShieldDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<StrikeShieldDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new StrikeShieldDbContext(options);
    }

    private static Finding AmbiguousFinding(Guid scanJobId, Guid stepRunId, string sourceTool, string identifier) => new()
    {
        ScanJobId = scanJobId,
        StepRunId = stepRunId,
        SourceTool = sourceTool,
        Title = $"{sourceTool} finding",
        Severity = FindingSeverity.High,
        AffectedAsset = "http://juice-shop:3000/",
        DedupeFingerprint = FindingFingerprint.Compute("http://juice-shop:3000", identifier, "http://juice-shop:3000/")
    };

    [Fact]
    public async Task CorrelateAsync_MergesAmbiguousCluster_WhenLlmSaysMergeAboveConfidenceFloor()
    {
        await using var db = CreateInMemoryDbContext();
        var scanJobId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();

        // Same target + severity, but deliberately different identifiers
        // (=> different DedupeFingerprint) — the deterministic pass alone
        // would never group these.
        var nucleiFinding = AmbiguousFinding(scanJobId, stepRunId, "nuclei", "outdated-jquery");
        var strixFinding = AmbiguousFinding(scanJobId, stepRunId, "strix", "CVE-2020-11023");
        db.Findings.AddRange(nucleiFinding, strixFinding);
        await db.SaveChangesAsync();

        var fakeResponse = $$"""
        {"shouldMerge": true, "confidence": 0.92, "findingIds": ["{{nucleiFinding.Id}}", "{{strixFinding.Id}}"]}
        """;
        var llmClient = new FakeLlmClient(fakeResponse);
        var correlator = new Correlator(db, llmClient, NullLogger<Correlator>.Instance);

        var groupsCreated = await correlator.CorrelateAsync(scanJobId);

        Assert.Equal(1, groupsCreated);
        Assert.Equal(1, llmClient.CallCount);

        var refreshedNuclei = await db.Findings.SingleAsync(f => f.Id == nucleiFinding.Id);
        var refreshedStrix = await db.Findings.SingleAsync(f => f.Id == strixFinding.Id);
        Assert.NotNull(refreshedNuclei.CorrelationGroupId);
        Assert.Equal(refreshedNuclei.CorrelationGroupId, refreshedStrix.CorrelationGroupId);
    }

    [Fact]
    public async Task CorrelateAsync_DoesNotMerge_WhenLlmConfidenceIsBelowFloor()
    {
        await using var db = CreateInMemoryDbContext();
        var scanJobId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();

        var nucleiFinding = AmbiguousFinding(scanJobId, stepRunId, "nuclei", "identifier-a");
        var strixFinding = AmbiguousFinding(scanJobId, stepRunId, "strix", "identifier-b");
        db.Findings.AddRange(nucleiFinding, strixFinding);
        await db.SaveChangesAsync();

        var fakeResponse = $$"""
        {"shouldMerge": true, "confidence": 0.4, "findingIds": ["{{nucleiFinding.Id}}", "{{strixFinding.Id}}"]}
        """;
        var correlator = new Correlator(db, new FakeLlmClient(fakeResponse), NullLogger<Correlator>.Instance);

        var groupsCreated = await correlator.CorrelateAsync(scanJobId);

        Assert.Equal(0, groupsCreated);
        var refreshedNuclei = await db.Findings.SingleAsync(f => f.Id == nucleiFinding.Id);
        Assert.Null(refreshedNuclei.CorrelationGroupId);
    }

    [Fact]
    public async Task CorrelateAsync_NeverCallsTheLlm_WhenNoByokKeyIsConfigured()
    {
        await using var db = CreateInMemoryDbContext();
        var scanJobId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();

        var nucleiFinding = AmbiguousFinding(scanJobId, stepRunId, "nuclei", "identifier-a");
        var strixFinding = AmbiguousFinding(scanJobId, stepRunId, "strix", "identifier-b");
        db.Findings.AddRange(nucleiFinding, strixFinding);
        await db.SaveChangesAsync();

        var llmClient = new FakeLlmClient(response: null, isConfigured: false);
        var correlator = new Correlator(db, llmClient, NullLogger<Correlator>.Instance);

        var groupsCreated = await correlator.CorrelateAsync(scanJobId);

        Assert.Equal(0, groupsCreated);
        Assert.Equal(0, llmClient.CallCount);
    }

    [Fact]
    public async Task CorrelateAsync_NeverCallsTheLlm_ForFindingsAlreadyGroupedDeterministically()
    {
        await using var db = CreateInMemoryDbContext();
        var scanJobId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var sharedFingerprint = FindingFingerprint.Compute("http://juice-shop:3000", "CWE-79", "http://juice-shop:3000/");

        var nucleiFinding = new Finding
        {
            ScanJobId = scanJobId,
            StepRunId = stepRunId,
            SourceTool = "nuclei",
            Title = "Reflected XSS",
            Severity = FindingSeverity.High,
            AffectedAsset = "http://juice-shop:3000/",
            DedupeFingerprint = sharedFingerprint
        };
        var zapFinding = new Finding
        {
            ScanJobId = scanJobId,
            StepRunId = stepRunId,
            SourceTool = "zap",
            Title = "Cross Site Scripting (Reflected)",
            Severity = FindingSeverity.High,
            AffectedAsset = "http://juice-shop:3000/",
            DedupeFingerprint = sharedFingerprint
        };
        db.Findings.AddRange(nucleiFinding, zapFinding);
        await db.SaveChangesAsync();

        var llmClient = new FakeLlmClient(response: "{\"shouldMerge\": false, \"confidence\": 0, \"findingIds\": []}");
        var correlator = new Correlator(db, llmClient, NullLogger<Correlator>.Instance);

        var groupsCreated = await correlator.CorrelateAsync(scanJobId);

        // The deterministic pass alone already grouped these (identical
        // fingerprint) — nothing ambiguous is left for the LLM to see.
        Assert.Equal(1, groupsCreated);
        Assert.Equal(0, llmClient.CallCount);
    }
}
