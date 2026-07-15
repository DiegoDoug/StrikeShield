using System.Text.Json;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Findings.Adapters;

/// <summary>
/// Parses Nikto's <c>-Format json</c> output (docs/ARCHITECTURE.md §6):
/// one Finding per reported item under "vulnerabilities". Nikto doesn't
/// emit CVSS/CWE/a severity scale of its own, so every Finding defaults to
/// Low severity pending human triage, same treatment as the nmap adapter's
/// script-output findings.
/// </summary>
public class NiktoFindingAdapter : IFindingAdapter
{
    public string ToolName => "nikto";

    public AdapterParseResult Parse(string rawContent, Guid scanJobId, Guid stepRunId, Target target)
    {
        var findings = new List<Finding>();

        using var doc = JsonDocument.Parse(rawContent);
        var root = doc.RootElement;

        var host = GetString(root, "host") ?? target.Value;

        if (!root.TryGetProperty("vulnerabilities", out var vulnerabilities) || vulnerabilities.ValueKind != JsonValueKind.Array)
        {
            return new AdapterParseResult(findings, Array.Empty<Asset>());
        }

        foreach (var vuln in vulnerabilities.EnumerateArray())
        {
            var url = GetString(vuln, "url");
            var message = GetString(vuln, "msg") ?? "Nikto flagged an item with no message.";
            var method = GetString(vuln, "method");
            var id = GetString(vuln, "id");

            var affectedAsset = string.IsNullOrWhiteSpace(url) ? host : $"{host}{url}";
            var identifier = id ?? message;

            findings.Add(new Finding
            {
                ScanJobId = scanJobId,
                StepRunId = stepRunId,
                SourceTool = ToolName,
                Title = message.Length > 200 ? message[..200] : message,
                Description = string.IsNullOrWhiteSpace(method) ? message : $"{method} {url}\n{message}",
                Severity = FindingSeverity.Low,
                AffectedAsset = affectedAsset,
                DedupeFingerprint = FindingFingerprint.Compute(target.Value, identifier, affectedAsset)
            });
        }

        return new AdapterParseResult(findings, Array.Empty<Asset>());
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
