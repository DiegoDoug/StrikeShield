namespace StrikeShield.Application.Playbooks;

public interface IPlaybookService
{
    Task<IReadOnlyList<PlaybookResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<PlaybookResponse> GetAsync(Guid id, CancellationToken cancellationToken = default);
}
