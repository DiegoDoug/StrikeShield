namespace StrikeShield.Orchestrator;

/// <summary>
/// Bound from the "Orchestrator" configuration section. The network and
/// volume names must match docker-compose.yml exactly — they're how the
/// Orchestrator's dynamically-created step containers reach the target
/// (e.g. juice-shop) and share output with the Orchestrator's own
/// filesystem view of the scan-output volume.
/// </summary>
public class OrchestratorOptions
{
    public string NetworkName { get; set; } = "strikeshield-net";
    public string ScanOutputVolumeName { get; set; } = "strikeshield-scan-output";
    public string ScanOutputMountPath { get; set; } = "/scan-output";
    public int PollIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// BYOK for the "strix" step type (docs/ARCHITECTURE.md §3) — instance-
    /// wide for this phase rather than a per-organization encrypted secret
    /// (that's Phase 10 scope). Empty StrixLlmApiKey simply means the strix
    /// step type has nothing to run against, same as any BYOK integration.
    /// </summary>
    public string StrixLlmModel { get; set; } = "anthropic/claude-sonnet-4-6";
    public string StrixLlmApiKey { get; set; } = string.Empty;
}
