using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using StrikeShield.Application.Ai;
using StrikeShield.Application.Common;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Reporting;

/// <summary>
/// The Reporting Agent (docs/ARCHITECTURE.md §7, docs/PHASED_PLAN.md Phase
/// 7) — "the highest-value use of the LLM in the whole platform." Every
/// quantitative/structural finding field (CVSS, CWE/CVE, repro steps,
/// evidence, PoC, fix, verification steps) is sourced directly from the
/// already-normalized Finding/FindingEvidence entities, never invented by
/// the LLM. The single LLM call per report only supplies the two
/// narrative fields those entities don't already carry — a per-report
/// summary and, per finding, a businessImpact/riskRating framed for that
/// report's audience — and its response is schema-validated (every
/// finding must get both fields) before anything renders.
/// </summary>
public class ReportGenerationService : IReportGenerationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string JsonInstructions =
        " Respond only with a JSON object: {\"summary\": string, \"findingNarratives\": " +
        "[{\"findingId\": string (the exact id given), \"businessImpact\": string, \"riskRating\": string}, ...]}. " +
        "Include exactly one entry in findingNarratives per finding id given in the input — never omit one.";

    private readonly IAppDbContext _db;
    private readonly ILlmClient _llmClient;
    private readonly IReportRenderer _renderer;

    public ReportGenerationService(IAppDbContext db, ILlmClient llmClient, IReportRenderer renderer)
    {
        _db = db;
        _llmClient = llmClient;
        _renderer = renderer;
    }

    public async Task<Report> GenerateAsync(Guid engagementId, ReportType type, CancellationToken cancellationToken = default)
    {
        var engagement = await _db.Engagements.FirstOrDefaultAsync(e => e.Id == engagementId, cancellationToken)
            ?? throw new NotFoundException($"Engagement '{engagementId}' was not found.");

        // Unlike the Correlator's escalation pass and the Adaptive Planner
        // (Phase 6), which silently no-op without a BYOK key since they're
        // optional background enrichment, report generation IS the
        // requested action — there is no meaningful PDF to return without
        // the LLM, so this fails loudly instead of returning an empty file.
        if (!_llmClient.IsConfigured)
        {
            throw new AppValidationException(
                "Report generation requires AiOrchestration:LlmApiKey to be configured (docs/PHASED_PLAN.md Phase 7) " +
                "— the Reporting Agent has no narrative content to write without an LLM.");
        }

        var scanJobIds = await _db.ScanJobs
            .Where(s => s.EngagementId == engagementId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var findings = await _db.Findings
            .Include(f => f.Evidence)
            .Where(f => scanJobIds.Contains(f.ScanJobId))
            .ToListAsync(cancellationToken);

        if (findings.Count == 0)
        {
            throw new AppValidationException($"Engagement '{engagementId}' has no findings yet — nothing to report.");
        }

        // One representative per CorrelationGroup (docs/ARCHITECTURE.md §7:
        // reports are generated from the correlated finding set, not raw
        // per-tool duplicates) — the highest-severity member of each group,
        // plus every still-ungrouped finding standalone.
        var representativeFindings = findings
            .Where(f => f.CorrelationGroupId is not null)
            .GroupBy(f => f.CorrelationGroupId!.Value)
            .Select(g => g.OrderByDescending(f => f.Severity).ThenByDescending(f => f.CvssScore ?? 0).First())
            .Concat(findings.Where(f => f.CorrelationGroupId is null))
            .OrderByDescending(f => f.Severity)
            .ToList();

        var (summary, narrativesById) = await RequestNarrativesAsync(type, engagement, representativeFindings, cancellationToken);

        var payloads = representativeFindings
            .Select(f => ToPayload(f, narrativesById[f.Id]))
            .ToList();

        var markdown = ReportMarkdownBuilder.Build(type, engagement, summary, payloads);
        var pdfContent = await _renderer.RenderPdfAsync(markdown, cancellationToken);

        var report = new Report
        {
            EngagementId = engagementId,
            Type = type,
            MarkdownContent = markdown,
            PdfContent = pdfContent,
            GeneratedAt = DateTimeOffset.UtcNow
        };
        _db.Reports.Add(report);
        await _db.SaveChangesAsync(cancellationToken);

        return report;
    }

    private async Task<(string Summary, Dictionary<Guid, FindingNarrative> NarrativesById)> RequestNarrativesAsync(
        ReportType type,
        Engagement engagement,
        IReadOnlyList<Finding> findings,
        CancellationToken cancellationToken)
    {
        var systemPrompt = GetSystemPrompt(type) + JsonInstructions;
        var userPrompt = JsonSerializer.Serialize(
            new
            {
                engagementName = engagement.Name,
                findings = findings.Select(f => new
                {
                    id = f.Id,
                    title = f.Title,
                    description = f.Description,
                    severity = f.Severity.ToString(),
                    sourceTool = f.SourceTool,
                    cweIds = f.CweIds,
                    cveIds = f.CveIds,
                    affectedAsset = f.AffectedAsset
                })
            },
            JsonOptions);

        var raw = await _llmClient.CompleteJsonAsync(systemPrompt, userPrompt, cancellationToken)
            ?? throw new AppValidationException("The Reporting Agent's LLM call failed or returned no content.");

        NarrativeResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<NarrativeResponse>(raw, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new AppValidationException($"The Reporting Agent returned unparsable JSON: {ex.Message}");
        }

        if (response is null || string.IsNullOrWhiteSpace(response.Summary))
        {
            throw new AppValidationException("The Reporting Agent's response was missing a summary.");
        }

        var narrativesById = new Dictionary<Guid, FindingNarrative>();
        foreach (var narrative in response.FindingNarratives ?? new List<FindingNarrative>())
        {
            if (narrative.FindingId is not null && Guid.TryParse(narrative.FindingId, out var id))
            {
                narrativesById[id] = narrative;
            }
        }

        // Schema-validation acceptance criterion (docs/PHASED_PLAN.md Phase
        // 7): every finding must have both narrative fields, or the report
        // is refused rather than shipped with a blank/hallucinated gap.
        var missing = findings
            .Where(f => !narrativesById.TryGetValue(f.Id, out var n)
                || string.IsNullOrWhiteSpace(n.BusinessImpact)
                || string.IsNullOrWhiteSpace(n.RiskRating))
            .Select(f => f.Id)
            .ToList();

        if (missing.Count > 0)
        {
            throw new AppValidationException(
                $"The Reporting Agent's response is missing businessImpact/riskRating for {missing.Count} finding(s) " +
                $"({string.Join(", ", missing.Take(5))}{(missing.Count > 5 ? ", ..." : string.Empty)}) — " +
                "refusing to ship an incomplete report.");
        }

        return (response.Summary, narrativesById);
    }

    private static ReportFindingPayload ToPayload(Finding finding, FindingNarrative narrative) => new(
        finding.Id,
        finding.Title,
        finding.Description,
        finding.SourceTool,
        finding.Severity,
        finding.CvssVector,
        finding.CvssScore,
        finding.CweIds,
        finding.CveIds,
        finding.OwaspCategory,
        finding.MitreAttackTechniques,
        finding.AffectedAsset,
        finding.ReproSteps,
        finding.Evidence.Select(e => e.Content).ToList(),
        finding.PocCode,
        finding.RecommendedFix,
        finding.VerificationSteps,
        narrative.BusinessImpact!,
        narrative.RiskRating!);

    private static string GetSystemPrompt(ReportType type) => type switch
    {
        ReportType.Executive =>
            "You are a security reporting assistant writing for a non-technical executive audience. " +
            "Write a short, jargon-free paragraph (the \"summary\") covering overall business risk. " +
            "For each finding, write a one-sentence businessImpact in plain business language (no " +
            "jargon) and a riskRating label (e.g. Critical/High/Medium/Low) reflecting how urgently " +
            "the business should act.",
        ReportType.Technical =>
            "You are a security reporting assistant writing a full technical report for security " +
            "engineers. Write a technical summary paragraph covering the engagement's findings overall. " +
            "For each finding, write a precise businessImpact sentence (technical detail is fine) and a " +
            "riskRating label consistent with its severity.",
        ReportType.DevRemediation =>
            "You are a security reporting assistant writing a remediation guide for software " +
            "developers. Write a summary paragraph oriented around what needs fixing and how. For each " +
            "finding, write a developer-focused businessImpact sentence (why this matters to ship " +
            "safely) and a riskRating label.",
        ReportType.Compliance =>
            "You are a security reporting assistant writing a compliance mapping document, framed as " +
            "\"what was tested and mapped\" rather than a certification. Write a summary paragraph " +
            "describing the scope and coverage of testing. For each finding, write a businessImpact " +
            "sentence framed around compliance/regulatory exposure and a riskRating label.",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    private sealed record NarrativeResponse(
        [property: JsonPropertyName("summary")] string? Summary,
        [property: JsonPropertyName("findingNarratives")] List<FindingNarrative>? FindingNarratives);

    private sealed record FindingNarrative(
        [property: JsonPropertyName("findingId")] string? FindingId,
        [property: JsonPropertyName("businessImpact")] string? BusinessImpact,
        [property: JsonPropertyName("riskRating")] string? RiskRating);
}
