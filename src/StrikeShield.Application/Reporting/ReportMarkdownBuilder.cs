using System.Text;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Reporting;

/// <summary>
/// Deterministically renders one report type's Markdown from an already-
/// validated finding payload set (docs/ARCHITECTURE.md §7) — no LLM call
/// happens here. Every finding, regardless of report type, keeps the same
/// underlying fields (business impact, risk rating, CVSS, repro steps,
/// evidence, PoC, fix, verification steps); each builder just picks a
/// different subset/grouping/level of detail for its audience.
/// </summary>
public static class ReportMarkdownBuilder
{
    public static string Build(ReportType type, Engagement engagement, string summary, IReadOnlyList<ReportFindingPayload> findings) => type switch
    {
        ReportType.Executive => BuildExecutive(engagement, summary, findings),
        ReportType.Technical => BuildTechnical(engagement, summary, findings),
        ReportType.DevRemediation => BuildDevRemediation(engagement, summary, findings),
        ReportType.Compliance => BuildCompliance(engagement, summary, findings),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    /// <summary>Business risk framing, no jargon, grouped by severity/business impact — no CVSS vectors or PoC code.</summary>
    private static string BuildExecutive(Engagement engagement, string summary, IReadOnlyList<ReportFindingPayload> findings)
    {
        var sb = new StringBuilder();
        AppendHeader(sb, "Executive Summary", engagement);

        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine(summary);
        sb.AppendLine();
        sb.AppendLine("## Findings by Business Impact");
        sb.AppendLine();

        foreach (var group in findings.GroupBy(f => f.Severity).OrderByDescending(g => g.Key))
        {
            sb.AppendLine($"### {group.Key} severity");
            sb.AppendLine();
            foreach (var finding in group)
            {
                sb.AppendLine($"- **{finding.Title}** ({finding.RiskRating} risk) — {finding.BusinessImpact}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>Full finding detail: CVSS vector breakdown, repro steps, evidence, source tool provenance, MITRE ATT&amp;CK mapping.</summary>
    private static string BuildTechnical(Engagement engagement, string summary, IReadOnlyList<ReportFindingPayload> findings)
    {
        var sb = new StringBuilder();
        AppendHeader(sb, "Technical Report", engagement);

        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine(summary);
        sb.AppendLine();
        sb.AppendLine("## Findings");
        sb.AppendLine();

        foreach (var finding in findings.OrderByDescending(f => f.Severity))
        {
            sb.AppendLine($"### {finding.Title} ({finding.Severity})");
            sb.AppendLine();
            sb.AppendLine($"- **Source tool:** {finding.SourceTool}");
            sb.AppendLine($"- **CVSS:** {FormatCvss(finding)}");
            sb.AppendLine($"- **CWE:** {JoinOrNone(finding.CweIds)}");
            sb.AppendLine($"- **CVE:** {JoinOrNone(finding.CveIds)}");
            sb.AppendLine($"- **OWASP category:** {finding.OwaspCategory ?? "(none)"}");
            sb.AppendLine($"- **MITRE ATT&CK:** {JoinOrNone(finding.MitreAttackTechniques)}");
            sb.AppendLine($"- **Affected asset:** {finding.AffectedAsset}");
            sb.AppendLine($"- **Business impact:** {finding.BusinessImpact}");
            sb.AppendLine($"- **Risk rating:** {finding.RiskRating}");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(finding.Description))
            {
                sb.AppendLine("**Description**");
                sb.AppendLine();
                sb.AppendLine(finding.Description);
                sb.AppendLine();
            }

            AppendNumberedList(sb, "Reproduction steps", finding.ReproSteps);
            AppendBulletedList(sb, "Evidence", finding.EvidenceRefs);

            if (!string.IsNullOrWhiteSpace(finding.PocCode))
            {
                sb.AppendLine("**Proof of concept**");
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine(finding.PocCode);
                sb.AppendLine("```");
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(finding.RecommendedFix))
            {
                sb.AppendLine("**Recommended fix**");
                sb.AppendLine();
                sb.AppendLine(finding.RecommendedFix);
                sb.AppendLine();
            }

            AppendNumberedList(sb, "Verification steps", finding.VerificationSteps);
        }

        return sb.ToString();
    }

    /// <summary>Grouped by affected component/asset instead of severity — PoC + fix + verification steps a dev can run locally.</summary>
    private static string BuildDevRemediation(Engagement engagement, string summary, IReadOnlyList<ReportFindingPayload> findings)
    {
        var sb = new StringBuilder();
        AppendHeader(sb, "Developer Remediation Guide", engagement);

        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine(summary);
        sb.AppendLine();

        foreach (var group in findings.GroupBy(f => f.AffectedAsset).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"## {group.Key}");
            sb.AppendLine();

            foreach (var finding in group.OrderByDescending(f => f.Severity))
            {
                sb.AppendLine($"### {finding.Title} ({finding.Severity})");
                sb.AppendLine();
                sb.AppendLine($"- **CWE:** {JoinOrNone(finding.CweIds)}");
                sb.AppendLine($"- **Source tool:** {finding.SourceTool}");
                sb.AppendLine();

                if (!string.IsNullOrWhiteSpace(finding.PocCode))
                {
                    sb.AppendLine("**Proof of concept**");
                    sb.AppendLine();
                    sb.AppendLine("```");
                    sb.AppendLine(finding.PocCode);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }

                if (!string.IsNullOrWhiteSpace(finding.RecommendedFix))
                {
                    sb.AppendLine("**Fix**");
                    sb.AppendLine();
                    sb.AppendLine(finding.RecommendedFix);
                    sb.AppendLine();
                }

                AppendNumberedList(sb, "Verification steps", finding.VerificationSteps);
            }
        }

        return sb.ToString();
    }

    /// <summary>A matrix view — OWASP category x CWE x finding, plus a MITRE ATT&amp;CK coverage table. "What we tested and mapped," not a certification.</summary>
    private static string BuildCompliance(Engagement engagement, string summary, IReadOnlyList<ReportFindingPayload> findings)
    {
        var sb = new StringBuilder();
        AppendHeader(sb, "Compliance Mapping", engagement);

        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine(summary);
        sb.AppendLine();

        sb.AppendLine("## OWASP / CWE Matrix");
        sb.AppendLine();
        sb.AppendLine("| OWASP Category | CWE | Finding | Severity |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var finding in findings.OrderBy(f => f.OwaspCategory ?? "zzz", StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine(
                $"| {finding.OwaspCategory ?? "(unmapped)"} | {JoinOrNone(finding.CweIds)} | {finding.Title} | {finding.Severity} |");
        }

        sb.AppendLine();
        sb.AppendLine("## MITRE ATT&CK Coverage");
        sb.AppendLine();
        sb.AppendLine("| Technique | Findings |");
        sb.AppendLine("|---|---|");
        var techniqueCounts = findings
            .SelectMany(f => f.MitreAttackTechniques, (f, technique) => technique)
            .GroupBy(technique => technique, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var group in techniqueCounts)
        {
            sb.AppendLine($"| {group.Key} | {group.Count()} |");
        }

        return sb.ToString();
    }

    private static void AppendHeader(StringBuilder sb, string title, Engagement engagement)
    {
        sb.AppendLine($"# {title} — {engagement.Name}");
        sb.AppendLine();
        sb.AppendLine($"*Generated {DateTimeOffset.UtcNow:yyyy-MM-dd}*");
        sb.AppendLine();
    }

    private static void AppendNumberedList(StringBuilder sb, string heading, IReadOnlyList<string> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        sb.AppendLine($"**{heading}**");
        sb.AppendLine();
        for (var i = 0; i < items.Count; i++)
        {
            sb.AppendLine($"{i + 1}. {items[i]}");
        }

        sb.AppendLine();
    }

    private static void AppendBulletedList(StringBuilder sb, string heading, IReadOnlyList<string> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        sb.AppendLine($"**{heading}**");
        sb.AppendLine();
        foreach (var item in items)
        {
            sb.AppendLine($"- {item}");
        }

        sb.AppendLine();
    }

    private static string FormatCvss(ReportFindingPayload finding) =>
        finding.CvssScore is { } score
            ? $"{score:0.0}{(string.IsNullOrWhiteSpace(finding.CvssVector) ? string.Empty : $" ({finding.CvssVector})")}"
            : "(none)";

    private static string JoinOrNone(IReadOnlyList<string> values) =>
        values.Count == 0 ? "(none)" : string.Join(", ", values);
}
