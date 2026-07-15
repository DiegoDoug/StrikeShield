using System.Text.Json;
using System.Text.RegularExpressions;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Findings.Adapters;

/// <summary>
/// Parses ffuf's <c>-of json</c> output (docs/ARCHITECTURE.md §6): every
/// discovered path becomes a Url Asset (feeds later steps, per Phase 5's
/// asset hand-off); only paths matching a sensitive-file heuristic
/// (<c>.git/</c>, <c>.env</c>, backups, etc. — ffuf itself has no concept
/// of "sensitive", it just reports what returned a non-excluded status
/// code) also become an Info-severity Finding worth a human glance.
/// </summary>
public class FfufFindingAdapter : IFindingAdapter
{
    private static readonly Regex SensitivePathPattern = new(
        @"(^|/)(\.git(/|$)|\.env|\.svn(/|$)|\.htpasswd|\.htaccess|wp-config\.php|id_rsa|\.sql$|\.bak$|\.zip$|\.tar(\.gz)?$|backup)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string ToolName => "ffuf";

    public AdapterParseResult Parse(string rawContent, Guid scanJobId, Guid stepRunId, Target target)
    {
        var assets = new List<Asset>();
        var findings = new List<Finding>();

        using var doc = JsonDocument.Parse(rawContent);
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return new AdapterParseResult(findings, assets);
        }

        foreach (var result in results.EnumerateArray())
        {
            var url = GetString(result, "url");
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            var status = result.TryGetProperty("status", out var statusEl) && statusEl.ValueKind == JsonValueKind.Number
                ? statusEl.GetInt32()
                : (int?)null;

            assets.Add(new Asset
            {
                TargetId = target.Id,
                DiscoveredByStepRunId = stepRunId,
                Type = AssetType.Url,
                Value = url,
                Metadata = status is null ? null : $"status={status}"
            });

            if (SensitivePathPattern.IsMatch(url))
            {
                findings.Add(new Finding
                {
                    ScanJobId = scanJobId,
                    StepRunId = stepRunId,
                    SourceTool = ToolName,
                    Title = $"ffuf discovered a sensitive-looking path: {url}",
                    Description = $"HTTP status {(status?.ToString() ?? "unknown")}. Fuzzed path matched a sensitive-file heuristic (.git/.env/backup/etc.) — worth manual triage.",
                    Severity = FindingSeverity.Info,
                    AffectedAsset = url,
                    DedupeFingerprint = FindingFingerprint.Compute(target.Value, "ffuf-sensitive-path", url)
                });
            }
        }

        return new AdapterParseResult(findings, assets);
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
