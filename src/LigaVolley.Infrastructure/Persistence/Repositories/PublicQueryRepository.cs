using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.MatchSheets;
using LigaVolley.Domain.Seasons;
using LigaVolley.Domain.TeamEntries;
using Microsoft.EntityFrameworkCore;

namespace LigaVolley.Infrastructure.Persistence.Repositories;

internal sealed class PublicQueryRepository(LigaVolleyDbContext db) : IPublicQueryRepository
{
    private static readonly CompetitionStatus[] PublicStatuses = [CompetitionStatus.Scheduled,CompetitionStatus.InProgress,CompetitionStatus.Finished,CompetitionStatus.Cancelled];

    public async Task<IReadOnlyList<Season>> ListSeasonsAsync(CancellationToken ct) => await db.Seasons.AsNoTracking().Where(s=>db.Competitions.Any(c=>c.SeasonId==s.SeasonId&&PublicStatuses.Contains(c.Status))).OrderByDescending(s=>s.Year).ToListAsync(ct);
    public async Task<IReadOnlyList<Competition>> ListCompetitionsAsync(int? seasonId,int? divisionId,Gender? gender,CompetitionStatus? status,CancellationToken ct)
    {
        var q=BaseCompetitions().Where(x=>PublicStatuses.Contains(x.Status));if(seasonId.HasValue)q=q.Where(x=>x.SeasonId==seasonId);if(divisionId.HasValue)q=q.Where(x=>x.DivisionId==divisionId);if(gender.HasValue)q=q.Where(x=>x.Division.Gender==gender);if(status.HasValue)q=q.Where(x=>x.Status==status);
        return await q.OrderByDescending(x=>x.Season.Year).ThenBy(x=>x.Division.LevelOrder).ThenBy(x=>x.Name).ToListAsync(ct);
    }
    public Task<Competition?> GetCompetitionAsync(int id,CancellationToken ct)=>Complete(BaseCompetitions()).SingleOrDefaultAsync(x=>x.CompetitionId==id,ct);
    public async Task<IReadOnlyList<TeamEntry>> ListTeamsAsync(int id,CancellationToken ct)=>await db.TeamEntries.AsNoTracking().Include(x=>x.Team).ThenInclude(x=>x.Club).Where(x=>x.CompetitionId==id&&(x.Status==TeamEntryStatus.Registered||x.Status==TeamEntryStatus.Active)).OrderBy(x=>x.Team.Name).ToListAsync(ct);
    public async Task<IReadOnlyList<Match>> ListMatchesAsync(int id,CancellationToken ct)=>await Matches().Where(x=>x.CompetitionId==id).OrderBy(x=>x.Phase.Sequence).ThenBy(x=>x.RoundNumber).ThenBy(x=>x.MatchNumber).ToListAsync(ct);
    public Task<Match?> GetMatchAsync(int id,CancellationToken ct)=>Matches().Include(x=>x.Competition).ThenInclude(x=>x.Season).Include(x=>x.Competition).ThenInclude(x=>x.Division).SingleOrDefaultAsync(x=>x.MatchId==id,ct);
    public Task<MatchSheet?> GetMatchSheetAsync(int id,CancellationToken ct)=>new MatchSheetRepository(db).GetAsync(id,false,ct);

    private IQueryable<Competition> BaseCompetitions()=>db.Competitions.AsNoTracking().Include(x=>x.Season).Include(x=>x.Division);
    private static IQueryable<Competition> Complete(IQueryable<Competition> q)=>q.AsSplitQuery().Include(x=>x.Phases).ThenInclude(x=>x.Groups).Include(x=>x.Phases).ThenInclude(x=>x.Series).ThenInclude(x=>x.Team1Entry).ThenInclude(x=>x!.Team).Include(x=>x.Phases).ThenInclude(x=>x.Series).ThenInclude(x=>x.Team2Entry).ThenInclude(x=>x!.Team).Include(x=>x.Phases).ThenInclude(x=>x.Series).ThenInclude(x=>x.ParticipantSources).ThenInclude(x=>x.SourceSeries);
    private IQueryable<Match> Matches()=>db.Matches.AsNoTracking().AsSplitQuery().Include(x=>x.Phase).Include(x=>x.PhaseGroup).Include(x=>x.Series).Include(x=>x.HomeTeamEntry).ThenInclude(x=>x!.Team).ThenInclude(x=>x.Club).Include(x=>x.AwayTeamEntry).ThenInclude(x=>x!.Team).ThenInclude(x=>x.Club).Include(x=>x.Venue).Include(x=>x.Sets);
}
