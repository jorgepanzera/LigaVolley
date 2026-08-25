using LigaVolley.Domain.MatchOfficials;
namespace LigaVolley.Application.Abstractions.Persistence;
public interface IMatchOfficialRepository{Task AcquireMatchLockAsync(int id,CancellationToken ct);Task<IReadOnlyList<MatchOfficial>> ListAsync(int id,bool tracking,CancellationToken ct);Task<MatchOfficial?> GetByIdAsync(int id,CancellationToken ct);void Add(MatchOfficial x);void Remove(MatchOfficial x);}
