using Microsoft.EntityFrameworkCore;
using StrikeShield.Application.Common;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Application.Targets;

public class TargetService : ITargetService
{
    private readonly IAppDbContext _db;

    public TargetService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<TargetResponse> CreateAsync(CreateTargetRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Value))
        {
            throw new AppValidationException("Value is required.");
        }

        var projectExists = await _db.Projects.AnyAsync(p => p.Id == request.ProjectId, cancellationToken);
        if (!projectExists)
        {
            throw new NotFoundException($"Project '{request.ProjectId}' was not found.");
        }

        var target = new Target
        {
            ProjectId = request.ProjectId,
            Type = request.Type,
            Value = request.Value.Trim()
        };

        _db.Targets.Add(target);
        await _db.SaveChangesAsync(cancellationToken);

        return TargetResponse.FromEntity(target);
    }

    public async Task<TargetResponse> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var target = await FindOrThrowAsync(id, cancellationToken);
        return TargetResponse.FromEntity(target);
    }

    public async Task<IReadOnlyList<TargetResponse>> GetAllAsync(Guid? projectId, CancellationToken cancellationToken = default)
    {
        var query = _db.Targets.AsQueryable();
        if (projectId is not null)
        {
            query = query.Where(t => t.ProjectId == projectId);
        }

        var targets = await query.OrderBy(t => t.CreatedAt).ToListAsync(cancellationToken);
        return targets.Select(TargetResponse.FromEntity).ToList();
    }

    public async Task<TargetResponse> UpdateAsync(Guid id, UpdateTargetRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Value))
        {
            throw new AppValidationException("Value is required.");
        }

        var target = await FindOrThrowAsync(id, cancellationToken);
        target.Type = request.Type;
        target.Value = request.Value.Trim();
        await _db.SaveChangesAsync(cancellationToken);

        return TargetResponse.FromEntity(target);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var target = await FindOrThrowAsync(id, cancellationToken);
        _db.Targets.Remove(target);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Target> FindOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _db.Targets.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Target '{id}' was not found.");
    }
}
