using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Application.CompetitionFormats;
using LigaVolley.Application.Divisions;
using LigaVolley.Application.Seasons;
using LigaVolley.Domain.Common;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.CompetitionFormats;

namespace LigaVolley.Application.Competitions;

public sealed class CompetitionService(
    ICompetitionRepository competitions,
    ISeasonRepository seasons,
    IDivisionRepository divisions,
    ICompetitionFormatRepository formats,
    IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<CompetitionSummaryDto>> ListAsync(int? seasonId, int? divisionId, CompetitionStatus? status, CancellationToken ct)
        => (await competitions.ListAsync(seasonId, divisionId, status, ct)).Select(ToSummary).ToArray();

    public async Task<CompetitionDto> GetAsync(int id, CancellationToken ct) => ToDto(await Required(id, false, ct));
    public async Task<CompetitionStructureDto> GetStructureAsync(int id, CancellationToken ct) => ToStructure(await Required(id, false, ct));

    public async Task<CompetitionDto> CreateAsync(CreateCompetitionRequest request, CancellationToken ct)
    {
        var season = await seasons.GetAsync(request.SeasonId, true, ct) ?? throw new ResourceNotFoundException("Season", request.SeasonId);
        var division = await divisions.GetAsync(request.DivisionId, true, ct) ?? throw new ResourceNotFoundException("Division", request.DivisionId);
        var format = await ResolveFormat(request.StructureSource, ct);
        var competition = new Competition(request.Name, season, division, format, request.PeriodType, request.StartDate, request.EndDate);
        competitions.Add(competition);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(competition);
    }

    public async Task<CompetitionDto> UpdateAsync(int id, UpdateCompetitionRequest request, CancellationToken ct)
    {
        var competition = await Required(id, true, ct);
        competition.Update(request.Name, request.PeriodType, request.StartDate, request.EndDate);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(competition);
    }

    public async Task<CompetitionDto> ChangeStatusAsync(int id, ChangeCompetitionStatusRequest request, CancellationToken ct)
    {
        var competition = await Required(id, true, ct);
        competition.ChangeStatus(request.Status);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(competition);
    }

    private async Task<CompetitionFormat> ResolveFormat(CompetitionStructureSourceDto? source, CancellationToken ct)
    {
        if (source is null) throw new DomainValidationException("StructureSource is required.");
        if (source.Type == CompetitionStructureSourceType.Format)
        {
            if (!source.CompetitionFormatId.HasValue || source.SourceCompetitionId.HasValue)
                throw new DomainValidationException("FROM_FORMAT requires CompetitionFormatId and forbids SourceCompetitionId.");
            return await formats.GetAsync(source.CompetitionFormatId.Value, true, ct)
                ?? throw new ResourceNotFoundException("CompetitionFormat", source.CompetitionFormatId.Value);
        }
        if (source.Type == CompetitionStructureSourceType.Competition)
        {
            if (!source.SourceCompetitionId.HasValue || source.CompetitionFormatId.HasValue)
                throw new DomainValidationException("FROM_COMPETITION requires SourceCompetitionId and forbids CompetitionFormatId.");
            var model = await Required(source.SourceCompetitionId.Value, false, ct);
            return await formats.GetAsync(model.CompetitionFormatId, true, ct)
                ?? throw new ResourceNotFoundException("CompetitionFormat", model.CompetitionFormatId);
        }
        throw new DomainValidationException("Structure source type is invalid.");
    }

    private async Task<Competition> Required(int id, bool tracking, CancellationToken ct)
        => await competitions.GetAsync(id, tracking, ct) ?? throw new ResourceNotFoundException("Competition", id);

    private static CompetitionDto ToDto(Competition x) => new(x.CompetitionId, x.Name,
        new SeasonSummaryDto(x.Season.SeasonId, x.Season.Year, x.Season.Name, x.Season.Active),
        new DivisionSummaryDto(x.Division.DivisionId, x.Division.Name, x.Division.LevelOrder, x.Division.Gender, x.Division.Active),
        new CompetitionFormatSummaryDto(x.CompetitionFormat.CompetitionFormatId, x.CompetitionFormat.Code, x.CompetitionFormat.Name, x.CompetitionFormat.MinTeams, x.CompetitionFormat.MaxTeams, x.CompetitionFormat.Active),
        x.PeriodType, x.StartDate, x.EndDate, x.Status, x.ScheduledAt, x.CompletedAt);

    private static CompetitionSummaryDto ToSummary(Competition x) => new(x.CompetitionId, x.Name, x.Season.Year, x.Division.Name, x.Division.Gender, x.CompetitionFormat.Name, x.PeriodType, x.Status);

    private static CompetitionStructureDto ToStructure(Competition x) => new(x.CompetitionId,
        x.Phases.OrderBy(p => p.Sequence).Select(p => new CompetitionPhaseDto(p.CompetitionPhaseId, p.Code, p.Name, p.PhaseType, p.PhaseRole, p.Sequence, p.Rounds, p.FixtureMode, p.Status,
            p.Groups.OrderBy(g => g.Sequence).Select(g => new CompetitionPhaseGroupDto(g.PhaseGroupId, g.Code, g.Name, g.GroupRole, g.Sequence, g.Rounds, g.FixtureMode, g.CarryOverMode)).ToArray(),
            p.Series.OrderBy(s => s.Sequence).Select(s => new CompetitionPlayoffSeriesDto(s.PlayoffSeriesId, s.Code, s.Name, s.Sequence, s.WinsRequired, s.Team1InitialWins, s.Team2InitialWins, s.Status,
                s.ParticipantSources.OrderBy(q => q.TargetSide).Select(q => new CompetitionSeriesParticipantSourceDto(q.SeriesParticipantSourceId, q.TargetSide, q.SourceType, q.SourcePlayoffSeriesId, q.SourceSeries.Code)).ToArray())).ToArray())).ToArray());
}
