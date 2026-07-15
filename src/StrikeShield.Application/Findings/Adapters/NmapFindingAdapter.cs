using System.Text.RegularExpressions;
using System.Xml.Linq;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Findings.Adapters;

/// <summary>
/// Parses nmap's <c>-oX</c> XML output (docs/ARCHITECTURE.md §6): every
/// host/open-port becomes an Asset, and only <c>--script vuln</c> output
/// that names a CVE becomes a Finding — nmap itself doesn't emit CVSS/CWE,
/// so severity is a fixed Medium default pending human triage.
/// </summary>
public class NmapFindingAdapter : IFindingAdapter
{
    private static readonly Regex CvePattern = new(@"CVE-\d{4}-\d{4,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string ToolName => "nmap";

    public AdapterParseResult Parse(string rawContent, Guid scanJobId, Guid stepRunId, Target target)
    {
        var findings = new List<Finding>();
        var assets = new List<Asset>();

        var doc = XDocument.Parse(rawContent);

        foreach (var host in doc.Descendants("host"))
        {
            var address = host.Element("address")?.Attribute("addr")?.Value;
            if (string.IsNullOrWhiteSpace(address))
            {
                continue;
            }

            assets.Add(new Asset
            {
                TargetId = target.Id,
                DiscoveredByStepRunId = stepRunId,
                Type = AssetType.Host,
                Value = address
            });

            foreach (var port in host.Descendants("port"))
            {
                var portId = port.Attribute("portid")?.Value;
                var protocol = port.Attribute("protocol")?.Value;
                var state = port.Element("state")?.Attribute("state")?.Value;
                if (portId is null || state != "open")
                {
                    continue;
                }

                var service = port.Element("service");
                var serviceName = service?.Attribute("name")?.Value;
                var product = service?.Attribute("product")?.Value;
                var version = service?.Attribute("version")?.Value;

                assets.Add(new Asset
                {
                    TargetId = target.Id,
                    DiscoveredByStepRunId = stepRunId,
                    Type = AssetType.Port,
                    Value = $"{address}:{portId}/{protocol}",
                    Metadata = string.Join(' ', new[] { serviceName, product, version }.Where(s => !string.IsNullOrWhiteSpace(s)))
                });

                foreach (var script in port.Descendants("script"))
                {
                    CollectCveFindings(script.Attribute("output")?.Value, $"{address}:{portId}", target, scanJobId, stepRunId, findings);
                }
            }

            foreach (var script in host.Element("hostscript")?.Descendants("script") ?? Enumerable.Empty<XElement>())
            {
                CollectCveFindings(script.Attribute("output")?.Value, address, target, scanJobId, stepRunId, findings);
            }
        }

        return new AdapterParseResult(findings, assets);
    }

    private static void CollectCveFindings(
        string? scriptOutput,
        string affectedAsset,
        Target target,
        Guid scanJobId,
        Guid stepRunId,
        List<Finding> findings)
    {
        if (string.IsNullOrWhiteSpace(scriptOutput))
        {
            return;
        }

        foreach (var cveId in CvePattern.Matches(scriptOutput).Select(m => m.Value.ToUpperInvariant()).Distinct())
        {
            findings.Add(new Finding
            {
                ScanJobId = scanJobId,
                StepRunId = stepRunId,
                SourceTool = "nmap",
                Title = $"nmap vuln script flagged {cveId}",
                Description = scriptOutput,
                Severity = FindingSeverity.Medium,
                CveIds = new List<string> { cveId },
                AffectedAsset = affectedAsset,
                DedupeFingerprint = FindingFingerprint.Compute(target.Value, cveId, affectedAsset)
            });
        }
    }
}
