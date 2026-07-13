using System.Text.Json;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Findings.Adapters;

/// <summary>
/// Parses Nuclei's <c>-jsonl</c> output (one JSON object per line). See
/// docs/ARCHITECTURE.md §6: template-id becomes the fallback dedupe
/// identifier when Nuclei's own classification doesn't supply a CWE/CVE.
/// </summary>
public class NucleiFindingAdapter : IFindingAdapter
{
    public string ToolName => "nuclei";

    public AdapterParseResult Parse(string rawContent, Guid scanJobId, Guid stepRunId, Target target)
    {
        var findings = new List<Finding>();

        foreach (var line in rawContent.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            var templateId = GetString(root, "template-id") ?? "unknown-template";
            var info = root.TryGetProperty("info", out var infoEl) ? infoEl : default;

            var name = GetString(info, "name") ?? templateId;
            var description = GetString(info, "description");
            var severity = ParseSeverity(GetString(info, "severity"));

            var cweIds = new List<string>();
            var cveIds = new List<string>();
            if (info.ValueKind == JsonValueKind.Object && info.TryGetProperty("classification", out var classification)
                && classification.ValueKind == JsonValueKind.Object)
            {
                cweIds.AddRange(GetStringArray(classification, "cwe-id"));
                cveIds.AddRange(GetStringArray(classification, "cve-id"));
            }

            var affectedAsset = GetString(root, "matched-at") ?? GetString(root, "host") ?? target.Value;
            var identifier = cweIds.FirstOrDefault() ?? cveIds.FirstOrDefault() ?? templateId;

            findings.Add(new Finding
            {
                ScanJobId = scanJobId,
                StepRunId = stepRunId,
                SourceTool = ToolName,
                Title = name,
                Description = description,
                Severity = severity,
                CweIds = cweIds,
                CveIds = cveIds,
                AffectedAsset = affectedAsset,
                DedupeFingerprint = FindingFingerprint.Compute(target.Value, identifier, affectedAsset)
            });
        }

        return new AdapterParseResult(findings, Array.Empty<Asset>());
    }

    private static FindingSeverity ParseSeverity(string? severity) => severity?.Trim().ToLowerInvariant() switch
    {
        "critical" => FindingSeverity.Critical,
        "high" => FindingSeverity.High,
        "medium" => FindingSeverity.Medium,
        "low" => FindingSeverity.Low,
        _ => FindingSeverity.Info
    };

    private static string? GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IEnumerable<string> GetStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            yield break;
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { } s)
                {
                    yield return s;
                }
            }
        }
        else if (value.ValueKind == JsonValueKind.String && value.GetString() is { } single)
        {
            yield return single;
        }
    }
}
