using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StrikeShield.Application.Ai;
using StrikeShield.Application.Common;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Application.Playbooks;

public class AdaptivePlanner : IAdaptivePlanner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string SystemPrompt =
        "You are an assistant that adaptively tunes a security-scan playbook mid-run. You are given " +
        "assets a recon step just discovered (subdomains/URLs/hosts/tech fingerprints) and a list of " +
        "not-yet-run steps later in the same playbook, each with its current tool/args template. " +
        "Decide whether the discovered assets justify tightening or expanding ONE of those steps' " +
        "arguments (e.g. adding nuclei tags for a fingerprinted CMS). Propose a change only when the " +
        "assets give you a genuinely specific reason to — most recon output does not. Respond only " +
        "with a JSON object: {\"proposeAmendment\": bool, \"targetStepKey\": string or null, " +
        "\"proposedArgsTemplate\": string or null (the target step's full replacement args template, " +
        "reusing the same {target}/{output}/{assetsFile...} placeholders it already uses), " +
        "\"rationale\": a short plain-language justification or null}.";

    private readonly IAppDbContext _db;
    private readonly ILlmClient _llmClient;
    private readonly ILogger<AdaptivePlanner> _logger;

    public AdaptivePlanner(IAppDbContext db, ILlmClient llmClient, ILogger<AdaptivePlanner> logger)
    {
        _db = db;
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task ProposeAmendmentAsync(
        Guid scanJobId,
        Guid completedStepRunId,
        IReadOnlyList<PlaybookStep> remainingSteps,
        CancellationToken cancellationToken = default)
    {
        if (!_llmClient.IsConfigured || remainingSteps.Count == 0)
        {
            return;
        }

        var discoveredAssets = await _db.Assets
            .Where(a => a.DiscoveredByStepRunId == completedStepRunId)
            .ToListAsync(cancellationToken);

        if (discoveredAssets.Count == 0)
        {
            return;
        }

        // Don't ask twice for the same trigger — a re-run after an
        // unrelated amendment's approve/reject shouldn't re-invoke the LLM
        // for a step that already produced a proposal (approved, rejected,
        // or still pending).
        var alreadyProposed = await _db.PlaybookAmendments
            .AnyAsync(a => a.ProposedByStepRunId == completedStepRunId, cancellationToken);
        if (alreadyProposed)
        {
            return;
        }

        var userPrompt = JsonSerializer.Serialize(
            new
            {
                discoveredAssets = discoveredAssets.Select(a => new { type = a.Type.ToString(), value = a.Value, metadata = a.Metadata }),
                remainingSteps = remainingSteps.Select(s => new { stepKey = s.StepKey, toolName = s.ToolName, currentArgsTemplate = s.ArgsTemplate })
            },
            JsonOptions);

        var raw = await _llmClient.CompleteJsonAsync(SystemPrompt, userPrompt, cancellationToken);
        if (raw is null)
        {
            return;
        }

        AmendmentProposal? proposal;
        try
        {
            proposal = JsonSerializer.Deserialize<AmendmentProposal>(raw, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Adaptive Planner returned unparsable JSON for StepRun {StepRunId}; skipping.", completedStepRunId);
            return;
        }

        if (proposal is null
            || !proposal.ProposeAmendment
            || string.IsNullOrWhiteSpace(proposal.TargetStepKey)
            || string.IsNullOrWhiteSpace(proposal.ProposedArgsTemplate))
        {
            return;
        }

        var targetStep = remainingSteps.FirstOrDefault(
            s => s.StepKey.Equals(proposal.TargetStepKey, StringComparison.OrdinalIgnoreCase));
        if (targetStep is null)
        {
            _logger.LogWarning(
                "Adaptive Planner proposed an amendment for unknown step key '{StepKey}'; skipping.",
                proposal.TargetStepKey);
            return;
        }

        _db.PlaybookAmendments.Add(new PlaybookAmendment
        {
            ScanJobId = scanJobId,
            ProposedByStepRunId = completedStepRunId,
            TargetPlaybookStepId = targetStep.Id,
            Rationale = string.IsNullOrWhiteSpace(proposal.Rationale) ? "No rationale provided." : proposal.Rationale,
            ProposedArgsTemplate = proposal.ProposedArgsTemplate
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    private sealed record AmendmentProposal(
        [property: JsonPropertyName("proposeAmendment")] bool ProposeAmendment,
        [property: JsonPropertyName("targetStepKey")] string? TargetStepKey,
        [property: JsonPropertyName("proposedArgsTemplate")] string? ProposedArgsTemplate,
        [property: JsonPropertyName("rationale")] string? Rationale);
}
