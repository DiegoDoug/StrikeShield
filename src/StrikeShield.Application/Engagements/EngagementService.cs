using Microsoft.EntityFrameworkCore;
using StrikeShield.Application.Common;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Application.Engagements;

public class EngagementService : IEngagementService
{
    private readonly IAppDbContext _db;

    public EngagementService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<EngagementResponse> CreateAsync(CreateEngagementRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new AppValidationException("Name is required.");
        }

        if (request.ScopeEnd <= request.ScopeStart)
        {
            throw new AppValidationException("ScopeEnd must be after ScopeStart.");
        }

        var projectExists = await _db.Projects.AnyAsync(p => p.Id == request.ProjectId, cancellationToken);
        if (!projectExists)
        {
            throw new NotFoundException($"Project '{request.ProjectId}' was not found.");
        }

        var engagement = new Engagement
        {
            ProjectId = request.ProjectId,
            Name = request.Name.Trim(),
            RulesOfEngagement = request.RulesOfEngagement,
            AuthorizationEvidenceUri = request.AuthorizationEvidenceUri,
            ScopeStart = request.ScopeStart,
            ScopeEnd = request.ScopeEnd,
            AllowedScopeRules = request.AllowedScopeRules ?? new List<string>()
        };

        _db.Engagements.Add(engagement);
        await _db.SaveChangesAsync(cancellationToken);

        return EngagementResponse.FromEntity(engagement);
    }

    public async Task<EngagementResponse> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var engagement = await FindOrThrowAsync(id, cancellationToken);
        return EngagementResponse.FromEntity(engagement);
    }

    public async Task<IReadOnlyList<EngagementResponse>> GetAllAsync(Guid? projectId, CancellationToken cancellationToken = default)
    {
        var query = _db.Engagements.AsQueryable();
        if (projectId is not null)
        {
            query = query.Where(e => e.ProjectId == projectId);
        }

        var engagements = await query.OrderBy(e => e.CreatedAt).ToListAsync(cancellationToken);
        return engagements.Select(EngagementResponse.FromEntity).ToList();
    }

    public async Task<EngagementResponse> ApproveAsync(Guid id, ApproveEngagementRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ApprovedBy))
        {
            throw new AppValidationException("ApprovedBy is required.");
        }

        var engagement = await FindOrThrowAsync(id, cancellationToken);
        engagement.Approve(request.ApprovedBy.Trim(), DateTimeOffset.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);

        return EngagementResponse.FromEntity(engagement);
    }

    private async Task<Engagement> FindOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _db.Engagements.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Engagement '{id}' was not found.");
    }
}
