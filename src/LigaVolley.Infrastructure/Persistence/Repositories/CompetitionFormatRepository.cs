using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Domain.CompetitionFormats;
using Microsoft.EntityFrameworkCore;

namespace LigaVolley.Infrastructure.Persistence.Repositories;

internal sealed class CompetitionFormatRepository(LigaVolleyDbContext db) : ICompetitionFormatRepository
{
    public async Task<IReadOnlyList<CompetitionFormat>> ListAsync(bool? active, short? teamCount, CancellationToken ct)
    {
        var query = db.CompetitionFormats.AsNoTracking();
        if (active.HasValue) query = query.Where(x => x.Active == active);
        if (teamCount.HasValue) query = query.Where(x => x.MinTeams <= teamCount && teamCount <= x.MaxTeams);
        return await query.OrderBy(x => x.Name).ToListAsync(ct);
    }

    public Task<CompetitionFormat?> GetAsync(int id, bool tracking, CancellationToken ct)
    {
        IQueryable<CompetitionFormat> query = db.CompetitionFormats;
        if (!tracking) query = query.AsNoTracking();
        return Complete(query).SingleOrDefaultAsync(x => x.CompetitionFormatId == id, ct);
    }

    public Task<bool> CodeExistsAsync(string code, int? excludingId, CancellationToken ct)
        => db.CompetitionFormats.AnyAsync(x => x.Code == code && (!excludingId.HasValue || x.CompetitionFormatId != excludingId), ct);
    public async Task<CompetitionFormatUsage> GetUsageAsync(int id,CancellationToken ct)
    {
        var counts=await db.Competitions.Where(x=>x.CompetitionFormatId==id).GroupBy(x=>x.Status==Domain.Competitions.CompetitionStatus.Draft).Select(x=>new{x.Key,Count=x.Count()}).ToListAsync(ct);
        return new(counts.SingleOrDefault(x=>x.Key)?.Count??0,counts.SingleOrDefault(x=>!x.Key)?.Count??0);
    }
    public void Add(CompetitionFormat format) => db.CompetitionFormats.Add(format);

    public void PrepareReplacement(CompetitionFormat format)
    {
        db.RemoveRange(format.Phases.SelectMany(x => x.Series).SelectMany(x => x.ParticipantSources));
        db.RemoveRange(format.QualificationRules); db.RemoveRange(format.MovementRules); db.RemoveRange(format.ScoringRules); db.RemoveRange(format.TiebreakRules);
        db.RemoveRange(format.Phases.SelectMany(x => x.Series)); db.RemoveRange(format.Phases.SelectMany(x => x.Groups)); db.RemoveRange(format.Phases);
    }

    private static IQueryable<CompetitionFormat> Complete(IQueryable<CompetitionFormat> query) => query.AsSplitQuery()
        .Include(x => x.Phases).ThenInclude(x => x.Groups)
        .Include(x => x.Phases).ThenInclude(x => x.Series).ThenInclude(x => x.ParticipantSources).ThenInclude(x => x.SourceSeries)
        .Include(x => x.QualificationRules).ThenInclude(x => x.SourcePhase)
        .Include(x => x.QualificationRules).ThenInclude(x => x.SourceGroup)
        .Include(x => x.QualificationRules).ThenInclude(x => x.TargetPhase)
        .Include(x => x.QualificationRules).ThenInclude(x => x.TargetGroup)
        .Include(x => x.QualificationRules).ThenInclude(x => x.TargetSeries)
        .Include(x => x.ScoringRules).Include(x => x.TiebreakRules)
        .Include(x => x.MovementRules).ThenInclude(x => x.SourcePhase)
        .Include(x => x.MovementRules).ThenInclude(x => x.SourceGroup)
        .Include(x => x.MovementRules).ThenInclude(x => x.SourceSeries);
}
