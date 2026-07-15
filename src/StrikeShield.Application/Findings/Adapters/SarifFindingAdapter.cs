using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Findings.Adapters;

/// <summary>
/// Generic SARIF 2.1.0 importer. Covers Semgrep/CodeQL/Trivy/Strix for free
/// once they land (Phase 4+) — they all emit valid SARIF natively, so this
/// one adapter is the only parser those tools will ever need
/// (docs/ARCHITECTURE.md §6).
/// </summary>
public class SarifFindingAdapter : IFindingAdapter
{
    private static readonly Regex CwePattern = new(@"cwe-(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Only ever used as a registration/lookup key (see
    // FindingIngestionService's AdapterFormatAliases) — never surfaced as
    // Finding.SourceTool, which instead comes from each run's own
    // tool.driver.name so Strix/Semgrep/CodeQL/Trivy findings all show
    // their real tool name despite sharing this one parser.
    public string ToolName => "sarif";

    public AdapterParseResult Parse(string rawContent, Guid scanJobId, Guid stepRunId, Target target)
    {
        var findings = new List<Finding>();

        using var doc = JsonDocument.Parse(rawContent);
        if (!doc.RootElement.TryGetProperty("runs", out var runs) || runs.ValueKind != JsonValueKind.Array)
        {
            return new AdapterParseResult(findings, Array.Empty<Asset>());
        }

        foreach (var run in runs.EnumerateArray())
        {
            var sourceTool = GetDriverName(run) ?? ToolName;

            var rules = new Dictionary<string, JsonElement>();
            if (run.TryGetProperty("tool", out var tool)
                && tool.TryGetProperty("driver", out var driver)
                && driver.TryGetProperty("rules", out var rulesEl)
                && rulesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var rule in rulesEl.EnumerateArray())
                {
                    if (GetString(rule, "id") is { } ruleId)
                    {
                        rules[ruleId] = rule;
                    }
                }
            }

            if (!run.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var result in results.EnumerateArray())
            {
                var ruleId = GetString(result, "ruleId") ?? "unknown-rule";
                rules.TryGetValue(ruleId, out var rule);

                var message = GetNestedString(result, "message", "text") ?? ruleId;
                var (location, line) = GetPrimaryLocation(result);

                var (severity, cvssScore) = DetermineSeverity(result, rule);
                var cweIds = ExtractCweIds(rule);

                var affectedAsset = line is null ? location : $"{location}:{line}";

                findings.Add(new Finding
                {
                    ScanJobId = scanJobId,
                    StepRunId = stepRunId,
                    SourceTool = sourceTool,
                    Title = GetNestedString(rule, "shortDescription", "text") ?? ruleId,
                    Description = message,
                    Severity = severity,
                    CvssScore = cvssScore,
                    CweIds = cweIds,
                    AffectedAsset = affectedAsset,
                    // Best-effort: SARIF's "properties" bag is tool-defined,
                    // not standardized, so this degrades to null rather than
                    // failing when a tool uses a different key.
                    PocCode = GetResultProperty(result, "poc") ?? GetResultProperty(result, "proofOfConcept") ?? GetResultProperty(result, "pocCode"),
                    DedupeFingerprint = FindingFingerprint.Compute(
                        target.Value,
                        cweIds.FirstOrDefault() ?? ruleId,
                        affectedAsset)
                });
            }
        }

        return new AdapterParseResult(findings, Array.Empty<Asset>());
    }

    private static string? GetDriverName(JsonElement run) =>
        run.TryGetProperty("tool", out var tool) && tool.TryGetProperty("driver", out var driver)
            ? GetString(driver, "name")?.ToLowerInvariant()
            : null;

    private static string? GetResultProperty(JsonElement result, string propertyName) =>
        result.ValueKind == JsonValueKind.Object && result.TryGetProperty("properties", out var props)
            ? GetString(props, propertyName)
            : null;

    private static (FindingSeverity Severity, double? CvssScore) DetermineSeverity(JsonElement result, JsonElement rule)
    {
        if (rule.ValueKind == JsonValueKind.Object
            && rule.TryGetProperty("properties", out var props)
            && props.TryGetProperty("security-severity", out var scoreEl)
            && scoreEl.ValueKind == JsonValueKind.String
            && double.TryParse(scoreEl.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var score))
        {
            var severity = score switch
            {
                >= 9.0 => FindingSeverity.Critical,
                >= 7.0 => FindingSeverity.High,
                >= 4.0 => FindingSeverity.Medium,
                > 0.0 => FindingSeverity.Low,
                _ => FindingSeverity.Info
            };
            return (severity, score);
        }

        var levelSeverity = GetString(result, "level") switch
        {
            "error" => FindingSeverity.High,
            "warning" => FindingSeverity.Medium,
            "note" => FindingSeverity.Low,
            _ => FindingSeverity.Info
        };
        return (levelSeverity, null);
    }

    private static List<string> ExtractCweIds(JsonElement rule)
    {
        var cweIds = new List<string>();
        if (rule.ValueKind != JsonValueKind.Object
            || !rule.TryGetProperty("properties", out var props)
            || !props.TryGetProperty("tags", out var tags)
            || tags.ValueKind != JsonValueKind.Array)
        {
            return cweIds;
        }

        foreach (var tag in tags.EnumerateArray())
        {
            if (tag.ValueKind != JsonValueKind.String || tag.GetString() is not { } tagText)
            {
                continue;
            }

            var match = CwePattern.Match(tagText);
            if (match.Success)
            {
                cweIds.Add($"CWE-{match.Groups[1].Value}");
            }
        }

        return cweIds;
    }

    private static (string Location, int? Line) GetPrimaryLocation(JsonElement result)
    {
        if (!result.TryGetProperty("locations", out var locations)
            || locations.ValueKind != JsonValueKind.Array
            || locations.GetArrayLength() == 0)
        {
            return ("unknown", null);
        }

        var first = locations[0];
        if (!first.TryGetProperty("physicalLocation", out var physicalLocation))
        {
            return ("unknown", null);
        }

        var uri = GetNestedString(physicalLocation, "artifactLocation", "uri") ?? "unknown";
        int? line = null;
        if (physicalLocation.TryGetProperty("region", out var region)
            && region.TryGetProperty("startLine", out var startLine)
            && startLine.ValueKind == JsonValueKind.Number)
        {
            line = startLine.GetInt32();
        }

        return (uri, line);
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? GetNestedString(JsonElement element, string propertyName, string nestedPropertyName) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var nested)
            ? GetString(nested, nestedPropertyName)
            : null;
}
