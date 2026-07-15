namespace StrikeShield.Application.Findings;

public interface IFindingIngestionService
{
    /// <summary>
    /// Parses a completed step's raw artifact output (Nuclei JSONL, SARIF,
    /// ZAP JSON, nmap XML, ...) via the adapter matching <paramref name="toolName"/>
    /// and persists the resulting Findings/Assets. A no-op (returns 0) if no
    /// adapter is registered for the tool — not every PlaybookStep produces
    /// normalized findings yet.
    /// </summary>
    Task<int> IngestAsync(
        Guid scanJobId,
        Guid stepRunId,
        Guid targetId,
        string toolName,
        string rawContent,
        CancellationToken cancellationToken = default);
}
