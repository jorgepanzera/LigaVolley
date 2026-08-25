using LigaVolley.Domain.CompetitionRosters;
namespace LigaVolley.Application.Abstractions.Persistence;
public interface ICompetitionRosterRepository
{
 Task AcquireTeamEntryLockAsync(int teamEntryId,CancellationToken ct);
 Task<CompetitionRoster?> GetAsync(int teamEntryId,bool tracking,CancellationToken ct);
 void Add(CompetitionRoster roster);
}
