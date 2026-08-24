using LigaVolley.Domain.Seasons;

namespace LigaVolley.Application.Abstractions.Persistence;

public interface ISeasonRepository
{
    Task<IReadOnlyList<Season>> ListAsync(bool? active, short? year, CancellationToken cancellationToken);
    Task<Season?> GetAsync(int id, bool tracking, CancellationToken cancellationToken);
    Task<bool> YearExistsAsync(short year, int? excludingId, CancellationToken cancellationToken);
    void Add(Season season);
}
