using Microsoft.EntityFrameworkCore;
using StrikeShield.Application.Findings;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;
using StrikeShield.Infrastructure.Persistence;
using Xunit;

namespace StrikeShield.Api.Tests;

/// <summary>
/// Phase 3 acceptance test (docs/PHASED_PLAN.md): a fixture of known,
/// hand-crafted overlapping tool output — not a live scan, whose grouping
/// would be nondeterministic — proving the Correlator's exact-fingerprint
/// grouping deterministically collapses cross-tool duplicates.
/// </summary>
public class CorrelatorTests
{
    private static StrikeShieldDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<StrikeShieldDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new StrikeShieldDbContext(options);
    }

    [Fact]
    public async Task CorrelateAsync_GroupsFindingsSharingExactFingerprint_AcrossTools()
    {
        await using var db = CreateInMemoryDbContext();
        var scanJobId = Guid.NewGuid();
        var otherStepRunId = Guid.NewGuid();

        // Nuclei and ZAP both flag the same CWE at the same endpoint — same
        // dedupe fingerprint, different sourceTool/title/description.
        var sharedFingerprint = FindingFingerprint.Compute("http://juice-shop:3000", "CWE-693", "http://juice-shop:3000/");

        var nucleiFinding = new Finding
        {
            ScanJobId = scanJobId,
            StepRunId = otherStepRunId,
            SourceTool = "nuclei",
            Title = "Missing X-Content-Type-Options header",
            Severity = FindingSeverity.Low,
            CweIds = new List<string> { "CWE-693" },
            AffectedAsset = "http://juice-shop:3000/",
            DedupeFingerprint = sharedFingerprint
        };

        var zapFinding = new Finding
        {
            ScanJobId = scanJobId,
            StepRunId = otherStepRunId,
            SourceTool = "zap",
            Title = "X-Content-Type-Options Header Missing",
            Severity = FindingSeverity.Low,
            CweIds = new List<string> { "CWE-693" },
            AffectedAsset = "http://juice-shop:3000/",
            DedupeFingerprint = sharedFingerprint
        };

        var unrelatedFinding = new Finding
        {
            ScanJobId = scanJobId,
            StepRunId = otherStepRunId,
            SourceTool = "nmap",
            Title = "nmap vuln script flagged CVE-2021-1234",
            Severity = FindingSeverity.Medium,
            CveIds = new List<string> { "CVE-2021-1234" },
            AffectedAsset = "juice-shop:3000",
            DedupeFingerprint = FindingFingerprint.Compute("http://juice-shop:3000", "CVE-2021-1234", "juice-shop:3000")
        };

        db.Findings.AddRange(nucleiFinding, zapFinding, unrelatedFinding);
        await db.SaveChangesAsync();

        var correlator = new Correlator(db);
        var groupsCreated = await correlator.CorrelateAsync(scanJobId);

        Assert.Equal(1, groupsCreated);

        var refreshedNuclei = await db.Findings.SingleAsync(f => f.Id == nucleiFinding.Id);
        var refreshedZap = await db.Findings.SingleAsync(f => f.Id == zapFinding.Id);
        var refreshedUnrelated = await db.Findings.SingleAsync(f => f.Id == unrelatedFinding.Id);

        Assert.NotNull(refreshedNuclei.CorrelationGroupId);
        Assert.Equal(refreshedNuclei.CorrelationGroupId, refreshedZap.CorrelationGroupId);
        Assert.Null(refreshedUnrelated.CorrelationGroupId);
    }

    [Fact]
    public async Task CorrelateAsync_ReusesExistingGroup_WhenRunTwice()
    {
        await using var db = CreateInMemoryDbContext();
        var scanJobId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var fingerprint = FindingFingerprint.Compute("http://juice-shop:3000", "CWE-79", "http://juice-shop:3000/rest/products/search");

        var findingA = new Finding
        {
            ScanJobId = scanJobId,
            StepRunId = stepRunId,
            SourceTool = "nuclei",
            Title = "Reflected XSS",
            Severity = FindingSeverity.High,
            CweIds = new List<string> { "CWE-79" },
            AffectedAsset = "http://juice-shop:3000/rest/products/search",
            DedupeFingerprint = fingerprint
        };
        var findingB = new Finding
        {
            ScanJobId = scanJobId,
            StepRunId = stepRunId,
            SourceTool = "zap",
            Title = "Cross Site Scripting (Reflected)",
            Severity = FindingSeverity.High,
            CweIds = new List<string> { "CWE-79" },
            AffectedAsset = "http://juice-shop:3000/rest/products/search",
            DedupeFingerprint = fingerprint
        };
        db.Findings.AddRange(findingA, findingB);
        await db.SaveChangesAsync();

        var correlator = new Correlator(db);
        var firstRunGroupsCreated = await correlator.CorrelateAsync(scanJobId);
        var groupIdAfterFirstRun = (await db.Findings.SingleAsync(f => f.Id == findingA.Id)).CorrelationGroupId;

        var secondRunGroupsCreated = await correlator.CorrelateAsync(scanJobId);
        var groupIdAfterSecondRun = (await db.Findings.SingleAsync(f => f.Id == findingA.Id)).CorrelationGroupId;

        Assert.Equal(1, firstRunGroupsCreated);
        Assert.Equal(0, secondRunGroupsCreated);
        Assert.Equal(groupIdAfterFirstRun, groupIdAfterSecondRun);
    }
}
