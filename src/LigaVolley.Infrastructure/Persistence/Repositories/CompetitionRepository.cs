using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Domain.Competitions;
using Microsoft.EntityFrameworkCore;

namespace LigaVolley.Infrastructure.Persistence.Repositories;

internal sealed class CompetitionRepository(LigaVolleyDbContext db) : ICompetitionRepository
{
    public async Task<IReadOnlyList<Competition>> ListAsync(int? seasonId, int? divisionId, CompetitionStatus? status, CancellationToken ct)
    {
        var query = Complete(db.Competitions.AsNoTracking());
        if (seasonId.HasValue) query = query.Where(x => x.SeasonId == seasonId);
        if (divisionId.HasValue) query = query.Where(x => x.DivisionId == divisionId);
        if (status.HasValue) query = query.Where(x => x.Status == status);
        return await query.OrderByDescending(x => x.Season.Year).ThenBy(x => x.Name).ToListAsync(ct);
    }
    public Task<Competition?> GetAsync(int id, bool tracking, CancellationToken ct)
    { IQueryable<Competition> query = db.Competitions; if (!tracking) query = query.AsNoTracking(); return Complete(query).SingleOrDefaultAsync(x => x.CompetitionId == id, ct); }
    public void Add(Competition competition) => db.Competitions.Add(competition);
    private static IQueryable<Competition> Complete(IQueryable<Competition> query) => query.AsSplitQuery().Include(x => x.Season).Include(x => x.Division)
        .Include(x => x.CompetitionFormat).ThenInclude(x => x.ScoringRules)
        .Include(x => x.CompetitionFormat).ThenInclude(x => x.TiebreakRules)
        .Include(x => x.CompetitionFormat).ThenInclude(x => x.QualificationRules).ThenInclude(x => x.SourcePhase)
        .Include(x => x.CompetitionFormat).ThenInclude(x => x.QualificationRules).ThenInclude(x => x.SourceGroup)
        .Include(x => x.CompetitionFormat).ThenInclude(x => x.QualificationRules).ThenInclude(x => x.TargetPhase)
        .Include(x => x.CompetitionFormat).ThenInclude(x => x.QualificationRules).ThenInclude(x => x.TargetGroup)
        .Include(x => x.CompetitionFormat).ThenInclude(x => x.QualificationRules).ThenInclude(x => x.TargetSeries)
        .Include(x => x.Phases).ThenInclude(x => x.Groups)
        .Include(x => x.Phases).ThenInclude(x => x.Series).ThenInclude(x => x.Team1Entry).ThenInclude(x=>x!.Team)
        .Include(x => x.Phases).ThenInclude(x => x.Series).ThenInclude(x => x.Team2Entry).ThenInclude(x=>x!.Team)
        .Include(x => x.Phases).ThenInclude(x => x.Series).ThenInclude(x => x.ParticipantSources).ThenInclude(x => x.SourceSeries);
}
