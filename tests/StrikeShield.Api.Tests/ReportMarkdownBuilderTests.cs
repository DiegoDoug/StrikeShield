using StrikeShield.Application.Reporting;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;
using Xunit;

namespace StrikeShield.Api.Tests;

/// <summary>
/// Phase 7 acceptance test (docs/PHASED_PLAN.md): "an automated
/// schema-validation test asserts every finding in the technical report
/// contains all required fields." ReportMarkdownBuilder is the
/// deterministic (no LLM, no Playwright) half of report generation, so
/// this asserts every required per-finding field from ARCHITECTURE.md §7
/// actually appears in the rendered Markdown for each of the 4 report
/// types, given a fully-populated fixture finding.
/// </summary>
public class ReportMarkdownBuilderTests
{
    private static Engagement Engagement() => new() { Name = "Acme Corp Q3 Pentest" };

    private static ReportFindingPayload FullyPopulatedFinding() => new(
        FindingId: Guid.NewGuid(),
        Title: "Reflected XSS in search endpoint",
        Description: "The search endpoint reflects unsanitized user input.",
        SourceTool: "nuclei",
        Severity: FindingSeverity.High,
        CvssVector: "CVSS:3.1/AV:N/AC:L/PR:N/UI:R/S:C/C:L/I:L/A:N",
        CvssScore: 7.4,
        CweIds: new[] { "CWE-79" },
        CveIds: new[] { "CVE-2024-12345" },
        OwaspCategory: "A03:2021-Injection",
        MitreAttackTechniques: new[] { "T1059" },
        AffectedAsset: "http://juice-shop:3000/rest/products/search",
        ReproSteps: new[] { "Send a GET request with a script payload in the q parameter.", "Observe the payload reflected unescaped." },
        EvidenceRefs: new[] { "GET /rest/products/search?q=<script>alert(1)</script>" },
        PocCode: "curl 'http://juice-shop:3000/rest/products/search?q=<script>alert(1)</script>'",
        RecommendedFix: "HTML-encode the q parameter before rendering it back to the client.",
        VerificationSteps: new[] { "Re-run the PoC and confirm the payload is now encoded, not executed." },
        BusinessImpact: "An attacker could hijack a customer's session.",
        RiskRating: "High");

    [Fact]
    public void BuildExecutive_IncludesTitleBusinessImpactAndRiskRating_ButNoTechnicalDetail()
    {
        var finding = FullyPopulatedFinding();
        var markdown = ReportMarkdownBuilder.Build(ReportType.Executive, Engagement(), "Overall risk is moderate.", new[] { finding });

        Assert.Contains(finding.Title, markdown);
        Assert.Contains(finding.BusinessImpact, markdown);
        Assert.Contains(finding.RiskRating, markdown);
        Assert.Contains("Overall risk is moderate.", markdown);
        // Executive summaries deliberately omit jargon-heavy technical detail.
        Assert.DoesNotContain(finding.CvssVector!, markdown);
        Assert.DoesNotContain(finding.PocCode!, markdown);
    }

    [Fact]
    public void BuildTechnical_IncludesEveryRequiredField()
    {
        var finding = FullyPopulatedFinding();
        var markdown = ReportMarkdownBuilder.Build(ReportType.Technical, Engagement(), "Technical overview.", new[] { finding });

        Assert.Contains(finding.Title, markdown);
        Assert.Contains(finding.SourceTool, markdown);
        Assert.Contains(finding.CvssVector!, markdown);
        Assert.Contains(finding.CvssScore!.Value.ToString("0.0"), markdown);
        Assert.Contains(finding.CweIds[0], markdown);
        Assert.Contains(finding.CveIds[0], markdown);
        Assert.Contains(finding.OwaspCategory!, markdown);
        Assert.Contains(finding.MitreAttackTechniques[0], markdown);
        Assert.Contains(finding.AffectedAsset, markdown);
        Assert.Contains(finding.BusinessImpact, markdown);
        Assert.Contains(finding.RiskRating, markdown);
        Assert.Contains(finding.Description!, markdown);
        Assert.Contains(finding.ReproSteps[0], markdown);
        Assert.Contains(finding.EvidenceRefs[0], markdown);
        Assert.Contains(finding.PocCode!, markdown);
        Assert.Contains(finding.RecommendedFix!, markdown);
        Assert.Contains(finding.VerificationSteps[0], markdown);
    }

    [Fact]
    public void BuildDevRemediation_GroupsByAffectedAsset_AndIncludesFixAndVerification()
    {
        var findingA = FullyPopulatedFinding() with { AffectedAsset = "src/routes/search.ts" };
        var findingB = FullyPopulatedFinding() with { FindingId = Guid.NewGuid(), Title = "SQL injection in login", AffectedAsset = "src/routes/login.ts" };

        var markdown = ReportMarkdownBuilder.Build(ReportType.DevRemediation, Engagement(), "Fix these before shipping.", new[] { findingA, findingB });

        Assert.Contains("src/routes/search.ts", markdown);
        Assert.Contains("src/routes/login.ts", markdown);
        Assert.Contains(findingA.PocCode!, markdown);
        Assert.Contains(findingA.RecommendedFix!, markdown);
        Assert.Contains(findingA.VerificationSteps[0], markdown);
        Assert.Contains(findingB.Title, markdown);
    }

    [Fact]
    public void BuildCompliance_ProducesAnOwaspCweMatrix_AndMitreAttackCoverageTable()
    {
        var finding = FullyPopulatedFinding();
        var markdown = ReportMarkdownBuilder.Build(ReportType.Compliance, Engagement(), "Scope and coverage.", new[] { finding });

        Assert.Contains("OWASP / CWE Matrix", markdown);
        Assert.Contains(finding.OwaspCategory!, markdown);
        Assert.Contains(finding.CweIds[0], markdown);
        Assert.Contains(finding.Title, markdown);
        Assert.Contains("MITRE ATT&CK Coverage", markdown);
        Assert.Contains(finding.MitreAttackTechniques[0], markdown);
    }
}
