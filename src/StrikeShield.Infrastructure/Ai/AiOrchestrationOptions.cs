namespace StrikeShield.Infrastructure.Ai;

/// <summary>
/// BYOK config for Phase 6's two LLM extension points (the Correlator's
/// escalation path and the Adaptive Planner) — instance-wide for this
/// phase rather than a full per-organization encrypted secret vault
/// (Phase 10 scope), same simplification already made for Strix's
/// LLM config in Phase 4. An empty LlmApiKey means both features simply
/// have nothing to run against (NullLlmClient is registered instead of
/// AnthropicLlmClient — see Infrastructure.DependencyInjection).
/// </summary>
public class AiOrchestrationOptions
{
    public string LlmModel { get; set; } = "claude-sonnet-4-6";
    public string LlmApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Which ILlmClient implementation to register — "anthropic" (default)
    /// or "deepseek". Case-insensitive; anything else falls back to
    /// Anthropic. See Infrastructure.DependencyInjection.
    /// </summary>
    public string LlmProvider { get; set; } = "anthropic";
}
