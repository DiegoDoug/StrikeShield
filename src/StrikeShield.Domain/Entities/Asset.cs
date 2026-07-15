using StrikeShield.Domain.Enums;

namespace StrikeShield.Domain.Entities;

/// <summary>
/// Something a recon/scan step discovered about a Target — a host, open
/// port, URL, subdomain, or tech fingerprint (docs/ARCHITECTURE.md §4/§6).
/// Phase 3 only populates these (from the nmap adapter) and exposes them
/// for reading; Phase 5 feeds them into downstream playbook steps as input.
/// </summary>
public class Asset
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TargetId { get; set; }
    public Target? Target { get; set; }

    public Guid? DiscoveredByStepRunId { get; set; }
    public StepRun? DiscoveredByStepRun { get; set; }

    public AssetType Type { get; set; }
    public string Value { get; set; } = string.Empty;
    public string? Metadata { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
