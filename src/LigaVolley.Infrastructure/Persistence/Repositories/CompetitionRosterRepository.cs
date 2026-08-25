using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Domain.CompetitionRosters;
using Microsoft.EntityFrameworkCore;
namespace LigaVolley.Infrastructure.Persistence.Repositories;
internal sealed class CompetitionRosterRepository(LigaVolleyDbContext db):ICompetitionRosterRepository
{
 public async Task AcquireTeamEntryLockAsync(int id,CancellationToken ct)=>_ = await db.Database.SqlQuery<int>($"SELECT team_entry_id AS [Value] FROM dbo.TEAM_ENTRY WITH (UPDLOCK,HOLDLOCK) WHERE team_entry_id={id}").SingleOrDefaultAsync(ct);
 public Task<CompetitionRoster?> GetAsync(int id,bool tracking,CancellationToken ct){IQueryable<CompetitionRoster>q=db.CompetitionRosters.Include(x=>x.TeamEntry).ThenInclude(x=>x.Team).Include(x=>x.Players).ThenInclude(x=>x.Player).ThenInclude(x=>x.Person).ThenInclude(x=>x.AdditionalDocuments).Include(x=>x.Staff).ThenInclude(x=>x.Coach).ThenInclude(x=>x.Person).AsSplitQuery();if(!tracking)q=q.AsNoTracking();return q.SingleOrDefaultAsync(x=>x.TeamEntryId==id,ct);}
 public void Add(CompetitionRoster roster)=>db.CompetitionRosters.Add(roster);
}
