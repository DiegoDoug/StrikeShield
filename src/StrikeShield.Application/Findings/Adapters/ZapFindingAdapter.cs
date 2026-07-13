using System.Text.Json;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Findings.Adapters;

/// <summary>
/// Parses OWASP ZAP's <c>-J report.json</c> baseline/full-scan output
/// (docs/ARCHITECTURE.md §6): one Finding per alert, one FindingEvidence
/// row per reported instance (request URI/method/evidence snippet).
/// </summary>
public class ZapFindingAdapter : IFindingAdapter
{
    public string ToolName => "zap";

    public AdapterParseResult Parse(string rawContent, Guid scanJobId, Guid stepRunId, Target target)
    {
        var findings = new List<Finding>();

        using var doc = JsonDocument.Parse(rawContent);
        if (!doc.RootElement.TryGetProperty("site", out var sites) || sites.ValueKind != JsonValueKind.Array)
        {
            return new AdapterParseResult(findings, Array.Empty<Asset>());
        }

        foreach (var site in sites.EnumerateArray())
        {
            if (!site.TryGetProperty("alerts", out var alerts) || alerts.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var alert in alerts.EnumerateArray())
            {
                var name = GetString(alert, "name") ?? GetString(alert, "alert") ?? "Unknown ZAP alert";
                var description = GetString(alert, "desc");
                var solution = GetString(alert, "solution");
                var severity = MapRiskCode(GetString(alert, "riskcode"));

                var cweIds = new List<string>();
                if (GetString(alert, "cweid") is { } cweId && cweId != "-1" && int.TryParse(cweId, out _))
                {
                    cweIds.Add($"CWE-{cweId}");
                }

                var instances = alert.TryGetProperty("instances", out var instancesEl) && instancesEl.ValueKind == JsonValueKind.Array
                    ? instancesEl.EnumerateArray().ToList()
                    : new List<JsonElement>();

                var affectedAsset = instances.Count > 0
                    ? GetString(instances[0], "uri") ?? target.Value
                    : GetString(site, "@name") ?? target.Value;

                var identifier = cweIds.FirstOrDefault() ?? name;

                var finding = new Finding
                {
                    ScanJobId = scanJobId,
                    StepRunId = stepRunId,
                    SourceTool = ToolName,
                    Title = name,
                    Description = description,
                    Severity = severity,
                    CweIds = cweIds,
                    AffectedAsset = affectedAsset,
                    RecommendedFix = solution,
                    DedupeFingerprint = FindingFingerprint.Compute(target.Value, identifier, affectedAsset)
                };

                foreach (var instance in instances)
                {
                    var uri = GetString(instance, "uri");
                    var method = GetString(instance, "method");
                    var evidence = GetString(instance, "evidence");
                    if (uri is null && method is null && evidence is null)
                    {
                        continue;
                    }

                    finding.Evidence.Add(new FindingEvidence
                    {
                        Type = "http-instance",
                        Content = $"{method} {uri}" + (string.IsNullOrWhiteSpace(evidence) ? string.Empty : $"\nEvidence: {evidence}")
                    });
                }

                findings.Add(finding);
            }
        }

        return new AdapterParseResult(findings, Array.Empty<Asset>());
    }

    private static FindingSeverity MapRiskCode(string? riskCode) => riskCode switch
    {
        "3" => FindingSeverity.High,
        "2" => FindingSeverity.Medium,
        "1" => FindingSeverity.Low,
        _ => FindingSeverity.Info
    };

    private static string? GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
