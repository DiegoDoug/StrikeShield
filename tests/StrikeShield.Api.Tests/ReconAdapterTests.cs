using StrikeShield.Application.Findings.Adapters;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;
using Xunit;

namespace StrikeShield.Api.Tests;

/// <summary>
/// Fixture-based parsing tests for Phase 5's new adapters
/// (docs/PHASED_PLAN.md) — known, hand-crafted native tool output, not a
/// live scan, same approach as SarifFindingAdapterTests.cs.
/// </summary>
public class ReconAdapterTests
{
    private static Target Target() => new() { Value = "http://example.com" };

    [Fact]
    public void SubdomainReconFindingAdapter_ParsesOneSubdomainPerLine_IntoAssets()
    {
        var adapter = new SubdomainReconFindingAdapter();
        var raw = "api.example.com\nadmin.example.com\n\napi.example.com\n";

        var result = adapter.Parse(raw, Guid.NewGuid(), Guid.NewGuid(), Target());

        Assert.Empty(result.Findings);
        Assert.Equal(2, result.Assets.Count);
        Assert.All(result.Assets, a => Assert.Equal(AssetType.Subdomain, a.Type));
        Assert.Contains(result.Assets, a => a.Value == "api.example.com");
        Assert.Contains(result.Assets, a => a.Value == "admin.example.com");
    }

    [Fact]
    public void KatanaFindingAdapter_ParsesJsonlEndpoints_IntoUrlAssets()
    {
        var adapter = new KatanaFindingAdapter();
        var raw = string.Join('\n', new[]
        {
            "{\"request\":{\"endpoint\":\"http://example.com/\",\"method\":\"GET\"}}",
            "{\"request\":{\"endpoint\":\"http://example.com/login\",\"method\":\"GET\"}}",
            "{\"request\":{\"endpoint\":\"http://example.com/\",\"method\":\"GET\"}}"
        });

        var result = adapter.Parse(raw, Guid.NewGuid(), Guid.NewGuid(), Target());

        Assert.Empty(result.Findings);
        Assert.Equal(2, result.Assets.Count);
        Assert.All(result.Assets, a => Assert.Equal(AssetType.Url, a.Type));
        Assert.Contains(result.Assets, a => a.Value == "http://example.com/login");
    }

    [Fact]
    public void FfufFindingAdapter_ProducesAssetsForEveryResult_AndFindingOnlyForSensitivePaths()
    {
        var adapter = new FfufFindingAdapter();
        var raw = """
        {
          "results": [
            { "url": "http://example.com/admin", "status": 200 },
            { "url": "http://example.com/.git/config", "status": 200 }
          ]
        }
        """;

        var result = adapter.Parse(raw, Guid.NewGuid(), Guid.NewGuid(), Target());

        Assert.Equal(2, result.Assets.Count);
        var finding = Assert.Single(result.Findings);
        Assert.Equal(FindingSeverity.Info, finding.Severity);
        Assert.Equal("http://example.com/.git/config", finding.AffectedAsset);
    }

    [Fact]
    public void NiktoFindingAdapter_ParsesVulnerabilities_AsLowSeverityFindings()
    {
        var adapter = new NiktoFindingAdapter();
        var raw = """
        {
          "host": "example.com",
          "vulnerabilities": [
            { "id": "999100", "method": "GET", "url": "/", "msg": "Retrieved x-powered-by header" }
          ]
        }
        """;

        var result = adapter.Parse(raw, Guid.NewGuid(), Guid.NewGuid(), Target());

        Assert.Empty(result.Assets);
        var finding = Assert.Single(result.Findings);
        Assert.Equal(FindingSeverity.Low, finding.Severity);
        Assert.Equal("example.com/", finding.AffectedAsset);
    }
}
