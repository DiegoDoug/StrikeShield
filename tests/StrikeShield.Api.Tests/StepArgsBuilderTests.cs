using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;
using StrikeShield.Orchestrator;
using Xunit;

namespace StrikeShield.Api.Tests;

/// <summary>
/// Phase 5's explicit acceptance criterion (docs/PHASED_PLAN.md): "assert
/// [that a downstream step's container args are built from an upstream
/// step's asset output] in an integration test, not just by eyeballing
/// logs." This drives StepArgsBuilder — the same code PlaybookExecutor
/// calls to build a real container's Cmd — with a fixture of upstream
/// Assets and inspects both the returned argv and the file it wrote to
/// the (real, temp-directory) shared scan-output volume.
/// </summary>
public class StepArgsBuilderTests : IDisposable
{
    private readonly string _outputDir = Path.Combine(Path.GetTempPath(), "strikeshield-test-" + Guid.NewGuid());

    public StepArgsBuilderTests()
    {
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        Directory.Delete(_outputDir, recursive: true);
    }

    private static Target Target() => new() { Value = "http://juice-shop:3000" };

    [Fact]
    public void Build_WritesAnAssetsFileFromUpstreamAssets_AndPointsTheArgAtIt()
    {
        var target = Target();
        var step = new PlaybookStep
        {
            ToolName = "nmap",
            ArgsTemplate = "-oX {output} -T4 --script vuln -iL {assetsFile:Subdomain}"
        };

        var upstreamAssets = new List<Asset>
        {
            new() { TargetId = target.Id, Type = AssetType.Subdomain, Value = "api.example.com" },
            new() { TargetId = target.Id, Type = AssetType.Subdomain, Value = "admin.example.com" },
            // A different asset type discovered by the same upstream step
            // must NOT leak into a type-filtered token.
            new() { TargetId = target.Id, Type = AssetType.Url, Value = "http://example.com/login" }
        };

        var args = StepArgsBuilder.Build(
            step,
            target,
            _outputDir,
            Path.Combine(_outputDir, "output.xml"),
            "job/step/output.xml",
            upstreamAssets);

        var assetsFileArg = Assert.Single(args, a => a.EndsWith("assets-subdomain.txt", StringComparison.Ordinal));
        Assert.True(File.Exists(assetsFileArg));

        var writtenLines = File.ReadAllText(assetsFileArg).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(new[] { "api.example.com", "admin.example.com" }, writtenLines);

        // Not hardcoded: change the upstream fixture, the file and arg change with it.
        Assert.DoesNotContain("http://example.com/login", writtenLines);
    }

    [Fact]
    public void Build_WithNoTypeFilter_IncludesEveryUpstreamAssetRegardlessOfType()
    {
        var target = Target();
        var step = new PlaybookStep
        {
            ToolName = "nuclei",
            ArgsTemplate = "-l {assetsFile} -jsonl -o {output}"
        };

        var upstreamAssets = new List<Asset>
        {
            new() { TargetId = target.Id, Type = AssetType.Url, Value = "http://example.com/a" },
            new() { TargetId = target.Id, Type = AssetType.Subdomain, Value = "sub.example.com" }
        };

        var args = StepArgsBuilder.Build(
            step,
            target,
            _outputDir,
            Path.Combine(_outputDir, "output.jsonl"),
            "job/step/output.jsonl",
            upstreamAssets);

        var assetsFileArg = Assert.Single(args, a => a.EndsWith("assets-all.txt", StringComparison.Ordinal));
        var writtenLines = File.ReadAllText(assetsFileArg).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(new[] { "http://example.com/a", "sub.example.com" }, writtenLines);
    }

    [Fact]
    public void Build_WithNoUpstreamAssets_WritesAnEmptyFile_RatherThanFailing()
    {
        var target = Target();
        var step = new PlaybookStep
        {
            ToolName = "nmap",
            ArgsTemplate = "-iL {assetsFile:Subdomain} -oX {output}"
        };

        var args = StepArgsBuilder.Build(
            step,
            target,
            _outputDir,
            Path.Combine(_outputDir, "output.xml"),
            "job/step/output.xml",
            Array.Empty<Asset>());

        var assetsFileArg = Assert.Single(args, a => a.EndsWith("assets-subdomain.txt", StringComparison.Ordinal));
        Assert.Equal(string.Empty, File.ReadAllText(assetsFileArg));
    }

    [Fact]
    public void Build_StillExpandsTargetAndOutputTokens_ForStepsWithNoDependencies()
    {
        var target = Target();
        var step = new PlaybookStep
        {
            ToolName = "nmap",
            ArgsTemplate = "-oX {output} -T4 --script vuln {targetHost}"
        };

        var args = StepArgsBuilder.Build(
            step,
            target,
            _outputDir,
            Path.Combine(_outputDir, "output.xml"),
            "job/step/output.xml",
            Array.Empty<Asset>());

        Assert.Equal(
            new[] { "-oX", Path.Combine(_outputDir, "output.xml"), "-T4", "--script", "vuln", "juice-shop" },
            args);
    }
}
