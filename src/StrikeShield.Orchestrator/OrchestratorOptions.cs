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
}
