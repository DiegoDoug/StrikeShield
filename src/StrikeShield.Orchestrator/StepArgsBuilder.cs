using System.Text.RegularExpressions;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Orchestrator;

/// <summary>
/// Expands a PlaybookStep's ArgsTemplate into the argv passed to its
/// container. Split out of PlaybookExecutor so it's directly unit-testable
/// without a Docker daemon (docs/PHASED_PLAN.md Phase 5's acceptance
/// criterion: "assert [that a downstream step's container args are built
/// from an upstream step's asset output] in an integration test, not just
/// by eyeballing logs" — see StepArgsBuilderTests).
/// </summary>
public static class StepArgsBuilder
{
    // Matches "{assetsFile}" (every asset regardless of type) or
    // "{assetsFile:Subdomain}"/"{assetsFile:Url}"/etc. (filtered to one
    // AssetType) inside an ArgsTemplate.
    private static readonly Regex AssetsFileTokenPattern =
        new(@"\{assetsFile(?::(?<type>\w+))?\}", RegexOptions.Compiled);

    /// <summary>
    /// Builds the container argv for one step. <paramref name="argsTemplate"/>
    /// is normally the step's own ArgsTemplate, but PlaybookExecutor passes
    /// an Approved PlaybookAmendment's ProposedArgsTemplate instead when one
    /// exists for this (ScanJob, step) pair (docs/PHASED_PLAN.md Phase 6) —
    /// this method itself doesn't need to know which. <paramref
    /// name="outputDir"/> is the shared scan-output volume path — identical
    /// in this (Orchestrator) process and the step container, so any
    /// asset-list file written here is immediately visible to the tool by
    /// the same absolute path, the same way "{output}" already works.
    /// </summary>
    public static List<string> Build(
        string argsTemplate,
        Target target,
        string outputDir,
        string containerOutputPath,
        string relativeOutputPath,
        IReadOnlyList<Asset> upstreamAssets)
    {
        var template = AssetsFileTokenPattern.Replace(
            argsTemplate,
            match => WriteAssetsFileAndGetPath(match, outputDir, upstreamAssets));

        return template
            .Replace("{target}", target.Value)
            .Replace("{targetHost}", ExtractHost(target.Value))
            .Replace("{output}", containerOutputPath)
            .Replace("{outputRelative}", relativeOutputPath)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private static string WriteAssetsFileAndGetPath(Match match, string outputDir, IReadOnlyList<Asset> upstreamAssets)
    {
        var typeGroup = match.Groups["type"];
        AssetType? filterType = null;
        if (typeGroup.Success)
        {
            if (!Enum.TryParse<AssetType>(typeGroup.Value, ignoreCase: true, out var parsedType))
            {
                throw new InvalidOperationException(
                    $"Unknown asset type '{typeGroup.Value}' in ArgsTemplate token '{match.Value}'.");
            }

            filterType = parsedType;
        }

        var values = upstreamAssets
            .Where(a => filterType is null || a.Type == filterType)
            .Select(a => a.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var fileName = $"assets-{filterType?.ToString().ToLowerInvariant() ?? "all"}.txt";
        var filePath = Path.Combine(outputDir, fileName);
        File.WriteAllText(filePath, string.Join('\n', values));

        return filePath;
    }

    /// <summary>
    /// nmap (and similar host-oriented tools) need a bare host/IP, not a
    /// full URL with scheme/port — extracts one from a Target.Value that
    /// may be either (e.g. "http://juice-shop:3000" or "juice-shop").
    /// </summary>
    private static string ExtractHost(string targetValue) =>
        Uri.TryCreate(targetValue, UriKind.Absolute, out var uri) ? uri.Host : targetValue;
}
