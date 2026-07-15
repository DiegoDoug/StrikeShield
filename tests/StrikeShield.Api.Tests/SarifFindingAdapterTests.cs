using StrikeShield.Application.Findings.Adapters;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;
using Xunit;

namespace StrikeShield.Api.Tests;

/// <summary>
/// Phase 4 regression test: the generic SARIF importer is shared across
/// Strix/Semgrep/CodeQL/Trivy (docs/ARCHITECTURE.md §6), so Finding.SourceTool
/// must come from each SARIF run's own tool.driver.name rather than a
/// hardcoded "sarif" literal — otherwise every tool's findings would be
/// indistinguishable in the API.
/// </summary>
public class SarifFindingAdapterTests
{
    private static readonly Target FakeTarget = new() { Id = Guid.NewGuid(), Value = "http://juice-shop:3000" };

    [Fact]
    public void Parse_SetsSourceToolFromDriverName_NotHardcodedSarif()
    {
        const string sarif = """
        {
          "version": "2.1.0",
          "runs": [
            {
              "tool": {
                "driver": {
                  "name": "Strix",
                  "rules": [
                    {
                      "id": "sql-injection",
                      "shortDescription": { "text": "SQL Injection" },
                      "properties": { "security-severity": "9.8", "tags": ["external/cwe/cwe-89"] }
                    }
                  ]
                }
              },
              "results": [
                {
                  "ruleId": "sql-injection",
                  "level": "error",
                  "message": { "text": "Confirmed SQL injection in login endpoint." },
                  "properties": { "poc": "' OR '1'='1" },
                  "locations": [
                    { "physicalLocation": { "artifactLocation": { "uri": "http://juice-shop:3000/rest/user/login" } } }
                  ]
                }
              ]
            }
          ]
        }
        """;

        var adapter = new SarifFindingAdapter();
        var result = adapter.Parse(sarif, Guid.NewGuid(), Guid.NewGuid(), FakeTarget);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("strix", finding.SourceTool);
        Assert.Equal(FindingSeverity.Critical, finding.Severity);
        Assert.Equal(9.8, finding.CvssScore);
        Assert.Contains("CWE-89", finding.CweIds);
        Assert.Equal("' OR '1'='1", finding.PocCode);
    }

    [Fact]
    public void Parse_FallsBackToSarif_WhenDriverNameMissing()
    {
        const string sarif = """
        {
          "version": "2.1.0",
          "runs": [
            {
              "tool": { "driver": { "rules": [] } },
              "results": [
                { "ruleId": "r1", "level": "warning", "message": { "text": "no driver name" } }
              ]
            }
          ]
        }
        """;

        var adapter = new SarifFindingAdapter();
        var result = adapter.Parse(sarif, Guid.NewGuid(), Guid.NewGuid(), FakeTarget);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("sarif", finding.SourceTool);
    }
}
