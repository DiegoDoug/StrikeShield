using System.Text.Json;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Findings.Adapters;

/// <summary>
/// Parses Katana's <c>-jsonl</c> crawl output (docs/ARCHITECTURE.md §6):
/// one JSON object per crawled URL, most commonly under
/// <c>request.endpoint</c>, with a few older/newer field-name variants
/// tolerated so a Katana version bump doesn't silently produce zero
/// Assets. No Finding — a crawled URL isn't a vulnerability by itself; it
/// becomes input to downstream steps (Phase 5's asset hand-off).
/// </summary>
public class KatanaFindingAdapter : IFindingAdapter
{
    public string ToolName => "katana";

    public AdapterParseResult Parse(string rawContent, Guid scanJobId, Guid stepRunId, Target target)
    {
        var assets = new List<Asset>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in rawContent.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            var url = GetString(root, "request", "endpoint")
                ?? GetString(root, "endpoint")
                ?? GetString(root, "url");

            if (string.IsNullOrWhiteSpace(url) || !seen.Add(url))
            {
                continue;
            }

            assets.Add(new Asset
            {
                TargetId = target.Id,
                DiscoveredByStepRunId = stepRunId,
                Type = AssetType.Url,
                Value = url
            });
        }

        return new AdapterParseResult(Array.Empty<Finding>(), assets);
    }

    private static string? GetString(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }
}
