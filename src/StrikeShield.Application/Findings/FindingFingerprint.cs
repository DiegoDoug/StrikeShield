using System.Security.Cryptography;
using System.Text;

namespace StrikeShield.Application.Findings;

/// <summary>
/// Stable hash of (normalizedTarget, cweId ?? cveId ?? templateId,
/// normalizedLocation) per docs/ARCHITECTURE.md §6. The Correlator groups
/// Findings sharing this fingerprint deterministically, before any
/// LLM-assisted cross-tool matching (Phase 6) ever runs.
/// </summary>
public static class FindingFingerprint
{
    public static string Compute(string normalizedTarget, string identifier, string normalizedLocation)
    {
        var key = string.Join(
            '|',
            normalizedTarget.Trim().ToLowerInvariant(),
            identifier.Trim().ToLowerInvariant(),
            normalizedLocation.Trim().ToLowerInvariant());

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
