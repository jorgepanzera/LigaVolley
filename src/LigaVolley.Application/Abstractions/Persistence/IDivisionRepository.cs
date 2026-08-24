using LigaVolley.Domain.Divisions;

namespace LigaVolley.Application.Abstractions.Persistence;

public interface IDivisionRepository
{
    Task<IReadOnlyList<Division>> ListAsync(Gender? gender, bool? active, CancellationToken cancellationToken);
    Task<Division?> GetAsync(int id, bool tracking, CancellationToken cancellationToken);
    Task<bool> NameExistsAsync(string name, Gender gender, int? excludingId, CancellationToken cancellationToken);
    Task<bool> LevelExistsAsync(short levelOrder, Gender gender, int? excludingId, CancellationToken cancellationToken);
    void Add(Division division);
}
