using Microsoft.EntityFrameworkCore;
using StrikeShield.Application.Common;
using StrikeShield.Application.Findings.Adapters;

namespace StrikeShield.Application.Findings;

public class FindingIngestionService : IFindingIngestionService
{
    private readonly IAppDbContext _db;
    private readonly IReadOnlyDictionary<string, IFindingAdapter> _adaptersByToolName;

    public FindingIngestionService(IAppDbContext db, IEnumerable<IFindingAdapter> adapters)
    {
        _db = db;
        _adaptersByToolName = adapters.ToDictionary(a => a.ToolName, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<int> IngestAsync(
        Guid scanJobId,
        Guid stepRunId,
        Guid targetId,
        string toolName,
        string rawContent,
        CancellationToken cancellationToken = default)
    {
        if (!_adaptersByToolName.TryGetValue(toolName, out var adapter) || string.IsNullOrWhiteSpace(rawContent))
        {
            return 0;
        }

        var target = await _db.Targets.FirstOrDefaultAsync(t => t.Id == targetId, cancellationToken)
            ?? throw new NotFoundException($"Target '{targetId}' was not found.");

        var result = adapter.Parse(rawContent, scanJobId, stepRunId, target);

        _db.Findings.AddRange(result.Findings);
        _db.Assets.AddRange(result.Assets);
        await _db.SaveChangesAsync(cancellationToken);

        return result.Findings.Count;
    }
}
