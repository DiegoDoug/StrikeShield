using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Findings.Adapters;

/// <summary>
/// Parses Subfinder's/Amass's plain-text subdomain-list output (one
/// subdomain per line) into Subdomain Assets — no Finding, per
/// docs/ARCHITECTURE.md §6: recon output feeds later playbook steps
/// (Phase 5's asset hand-off), it isn't a vulnerability by itself.
/// Registered under "subfinder"; FindingIngestionService aliases "amass"
/// to this same adapter since both tools' default output is one hostname
/// per line, close enough to share a parser (same idea as "strix" ->
/// "sarif").
/// </summary>
public class SubdomainReconFindingAdapter : IFindingAdapter
{
    public string ToolName => "subfinder";

    public AdapterParseResult Parse(string rawContent, Guid scanJobId, Guid stepRunId, Target target)
    {
        var assets = rawContent
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith('#'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(subdomain => new Asset
            {
                TargetId = target.Id,
                DiscoveredByStepRunId = stepRunId,
                Type = AssetType.Subdomain,
                Value = subdomain
            })
            .ToList();

        return new AdapterParseResult(Array.Empty<Finding>(), assets);
    }
}
