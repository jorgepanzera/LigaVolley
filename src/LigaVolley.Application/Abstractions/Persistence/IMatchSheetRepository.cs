using LigaVolley.Domain.MatchSheets;
namespace LigaVolley.Application.Abstractions.Persistence;
public interface IMatchSheetRepository{Task AcquireMatchLockAsync(int matchId,CancellationToken ct);Task<MatchSheet?> GetAsync(int matchId,bool tracking,CancellationToken ct);void Add(MatchSheet sheet);}
