namespace StrikeShield.Application.Ai;

/// <summary>
/// No-op ILlmClient used whenever no BYOK LLM key is configured
/// (docs/PHASED_PLAN.md Phase 6) — the Correlator's escalation pass and
/// the Adaptive Planner simply have nothing to run against, exactly like
/// the "strix" step type with no LLM_API_KEY (Phase 4): every other
/// feature keeps working, this one just never fires.
/// </summary>
public sealed class NullLlmClient : ILlmClient
{
    public static readonly NullLlmClient Instance = new();

    public bool IsConfigured => false;

    public Task<string?> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}
