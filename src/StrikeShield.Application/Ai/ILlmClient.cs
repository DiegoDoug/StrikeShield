namespace StrikeShield.Application.Ai;

/// <summary>
/// The seam both of Phase 6's AI extension points (docs/ARCHITECTURE.md §3
/// — the Correlator's LLM-escalation path and the Adaptive Planner) call
/// through. Deliberately narrow: one prompt in, one JSON string out, no
/// conversation/tool-use state — keeps every call auditable and batched,
/// never a free-roaming agent.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// False when no BYOK key is configured (docs/PHASED_PLAN.md Phase 6)
    /// — callers must skip their LLM path entirely rather than call and
    /// fail, the same contract Strix's step type has with no LLM_API_KEY
    /// (Phase 4).
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Returns the model's raw JSON response text, or null if the call
    /// wasn't made (not configured) or failed/returned something
    /// unparsable — callers treat null as "skip this proposal/judgment",
    /// never as an error to surface to the operator.
    /// </summary>
    Task<string?> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}
