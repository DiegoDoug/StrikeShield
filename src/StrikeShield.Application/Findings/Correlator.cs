using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StrikeShield.Application.Ai;
using StrikeShield.Application.Common;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Application.Findings;

public class Correlator : ICorrelator
{
    // Below this confidence, a "maybe" from the LLM shouldn't silently
    // collapse two findings a human hasn't looked at yet — leave them
    // ungrouped rather than guess.
    private const double MinimumConfidenceToMerge = 0.7;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAppDbContext _db;
    private readonly ILlmClient _llmClient;
    private readonly ILogger<Correlator> _logger;

    public Correlator(IAppDbContext db, ILlmClient llmClient, ILogger<Correlator> logger)
    {
        _db = db;
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<int> CorrelateAsync(Guid scanJobId, CancellationToken cancellationToken = default)
    {
        var findings = await _db.Findings
            .Where(f => f.ScanJobId == scanJobId)
            .ToListAsync(cancellationToken);

        var groupsCreated = 0;

        // Deterministic pass: exact DedupeFingerprint match. Always runs,
        // never touches the LLM — cost control (docs/ARCHITECTURE.md §3).
        foreach (var group in findings.GroupBy(f => f.DedupeFingerprint))
        {
            var members = group.ToList();
            if (members.Count < 2)
            {
                continue;
            }

            // Some members may already belong to a group from an earlier
            // correlation pass (e.g. a re-run) — reuse the first one found
            // instead of creating duplicate groups for the same fingerprint.
            var existingGroupId = members.Select(f => f.CorrelationGroupId).FirstOrDefault(id => id is not null);

            Guid groupId;
            if (existingGroupId is { } id)
            {
                groupId = id;
            }
            else
            {
                var correlationGroup = new CorrelationGroup();
                _db.CorrelationGroups.Add(correlationGroup);
                groupId = correlationGroup.Id;
                groupsCreated++;
            }

            foreach (var finding in members)
            {
                finding.CorrelationGroupId = groupId;
            }
        }

        // LLM-assisted escalation pass (Phase 6): only for findings the
        // deterministic pass left ungrouped, and only if a BYOK key is
        // configured — a no-op otherwise (NullLlmClient), same contract as
        // the "strix" step type with no LLM_API_KEY.
        if (_llmClient.IsConfigured)
        {
            groupsCreated += await EscalateAmbiguousMatchesAsync(findings, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return groupsCreated;
    }

    /// <summary>
    /// Ambiguous cluster = findings sharing a normalized target + severity
    /// but nothing else (different fingerprints, different sourceTools) —
    /// "same target/severity, different everything else" per
    /// docs/ARCHITECTURE.md §3. One batched LLM call per cluster, never one
    /// per pair, so an N-way overlap costs one call, not N choose 2.
    /// </summary>
    private async Task<int> EscalateAmbiguousMatchesAsync(List<Finding> findings, CancellationToken cancellationToken)
    {
        var groupsCreated = 0;

        var candidateClusters = findings
            .Where(f => f.CorrelationGroupId is null)
            .GroupBy(f => (NormalizedTarget: f.AffectedAsset.Trim().ToLowerInvariant(), f.Severity))
            .Where(g => g.Count() > 1 && g.Select(f => f.SourceTool).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1);

        foreach (var cluster in candidateClusters)
        {
            var members = cluster.ToList();
            var judgment = await RequestMergeJudgmentAsync(members, cancellationToken);
            if (judgment is null || !judgment.ShouldMerge || judgment.Confidence < MinimumConfidenceToMerge)
            {
                continue;
            }

            var toMerge = members.Where(f => judgment.FindingIds?.Contains(f.Id) == true).ToList();
            if (toMerge.Count < 2)
            {
                continue;
            }

            var correlationGroup = new CorrelationGroup();
            _db.CorrelationGroups.Add(correlationGroup);
            foreach (var finding in toMerge)
            {
                finding.CorrelationGroupId = correlationGroup.Id;
                finding.Confidence = judgment.Confidence;
            }

            groupsCreated++;
        }

        return groupsCreated;
    }

    private async Task<MergeJudgment?> RequestMergeJudgmentAsync(IReadOnlyList<Finding> cluster, CancellationToken cancellationToken)
    {
        const string systemPrompt =
            "You are a security-findings correlator. You are given several findings from different " +
            "security tools that hit the same target at the same severity but disagree on everything " +
            "else. Decide whether they describe the same underlying issue. Respond only with a JSON " +
            "object: {\"shouldMerge\": bool, \"confidence\": number between 0 and 1, \"findingIds\": " +
            "[the ids of the findings that belong together, empty if shouldMerge is false]}.";

        var userPrompt = JsonSerializer.Serialize(
            cluster.Select(f => new
            {
                id = f.Id,
                sourceTool = f.SourceTool,
                title = f.Title,
                description = f.Description,
                cweIds = f.CweIds,
                cveIds = f.CveIds
            }),
            JsonOptions);

        var raw = await _llmClient.CompleteJsonAsync(systemPrompt, userPrompt, cancellationToken);
        if (raw is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<MergeJudgment>(raw, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Correlator's LLM escalation call returned unparsable JSON; skipping this cluster.");
            return null;
        }
    }

    private sealed record MergeJudgment(
        [property: JsonPropertyName("shouldMerge")] bool ShouldMerge,
        [property: JsonPropertyName("confidence")] double Confidence,
        [property: JsonPropertyName("findingIds")] List<Guid>? FindingIds);
}
