using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Domain.CompetitionFormats;

namespace LigaVolley.Application.CompetitionFormats;

public sealed class CompetitionFormatService(ICompetitionFormatRepository repository, IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<CompetitionFormatSummaryDto>> ListAsync(bool? active, short? teamCount, CancellationToken ct)
        => (await repository.ListAsync(active, teamCount, ct)).Select(ToSummary).ToArray();

    public async Task<CompetitionFormatDto> GetAsync(int id, CancellationToken ct) => ToDto(await Required(id, false, ct));

    public Task<CompetitionFormatValidationDto> ValidateAsync(ValidateCompetitionFormatRequest request)
        => Task.FromResult(CompetitionFormatDefinitionFactory.Validate(request.MinTeams, request.MaxTeams, request.Definition));

    public async Task<CompetitionFormatDto> CreateAsync(CreateCompetitionFormatRequest request, CancellationToken ct)
    {
        await Unique(request.Code, null, ct);
        var format = CompetitionFormatDefinitionFactory.Build(request.Code, request.Name, request.Description, request.MinTeams, request.MaxTeams, request.Definition);
        repository.Add(format); await unitOfWork.SaveChangesAsync(ct); return ToDto(format);
    }

    public async Task<CompetitionFormatDto> UpdateAsync(int id, UpdateCompetitionFormatRequest request, CancellationToken ct)
    {
        var format = await Required(id, true, ct); await Unique(request.Code, id, ct);
        var replacement = CompetitionFormatDefinitionFactory.Build(request.Code, request.Name, request.Description, request.MinTeams, request.MaxTeams, request.Definition);
        repository.PrepareReplacement(format); format.ReplaceWith(replacement);
        await unitOfWork.SaveChangesAsync(ct); return ToDto(format);
    }

    public async Task<CompetitionFormatDto> CloneAsync(int id, CloneCompetitionFormatRequest request, CancellationToken ct)
    {
        var source = await Required(id, false, ct); await Unique(request.Code, null, ct);
        var clone = CompetitionFormatDefinitionFactory.Build(request.Code, request.Name, request.Description ?? source.Description, source.MinTeams, source.MaxTeams, ToDefinition(source));
        repository.Add(clone); await unitOfWork.SaveChangesAsync(ct); return ToDto(clone);
    }

    public async Task<CompetitionFormatDto> SetActiveAsync(int id, bool active, CancellationToken ct)
    { var format = await Required(id, true, ct); format.SetActive(active); await unitOfWork.SaveChangesAsync(ct); return ToDto(format); }

    private Task<CompetitionFormat?> Get(int id, bool tracking, CancellationToken ct) => repository.GetAsync(id, tracking, ct);
    private async Task<CompetitionFormat> Required(int id, bool tracking, CancellationToken ct) => await Get(id, tracking, ct) ?? throw new ResourceNotFoundException("CompetitionFormat", id);
    private async Task Unique(string code, int? id, CancellationToken ct) { if (await repository.CodeExistsAsync(code?.Trim() ?? "", id, ct)) throw new ResourceConflictException("competition_format_code_conflict", $"Competition format code '{code}' already exists."); }
    private static CompetitionFormatSummaryDto ToSummary(CompetitionFormat x) => new(x.CompetitionFormatId, x.Code, x.Name, x.MinTeams, x.MaxTeams, x.Active);
    private static CompetitionFormatDto ToDto(CompetitionFormat x) => new(x.CompetitionFormatId, x.Code, x.Name, x.Description, x.MinTeams, x.MaxTeams, x.Active, ToDefinition(x));
    private static CompetitionFormatDefinitionDto ToDefinition(CompetitionFormat x) => new(
        x.Phases.OrderBy(p => p.Sequence).Select(p => new FormatPhaseInputDto(p.Code, p.Name, p.PhaseType, p.PhaseRole, p.Sequence, p.Rounds, p.FixtureMode,
            p.Groups.OrderBy(g => g.Sequence).Select(g => new FormatGroupInputDto(g.Code, g.Name, g.GroupRole, g.Sequence, g.Rounds, g.FixtureMode, g.CarryOverMode)).ToArray(),
            p.Series.OrderBy(s => s.Sequence).Select(s => new FormatPlayoffSeriesInputDto(s.Code, s.Name, s.Sequence, s.WinsRequired, s.Team1InitialWins, s.Team2InitialWins, s.ParticipantSources.OrderBy(q => q.TargetSide).Select(q => new SeriesParticipantSourceInputDto(q.TargetSide, q.SourceType, q.SourceSeries.Code)).ToArray())).ToArray())).ToArray(),
        x.QualificationRules.OrderBy(r => r.Sequence).Select(r => new FormatQualificationRuleInputDto(r.SourcePhase.Code, r.SourceGroup?.Code, r.SelectionMode, r.FromPosition, r.ToPosition, r.TargetType, r.TargetPhase.Code, r.TargetGroup?.Code, r.TargetSeries?.Code, r.TargetSide, r.Sequence)).ToArray(),
        x.ScoringRules.Select(r => new FormatScoringRuleInputDto(r.WinnerSets, r.LoserSets, r.WinnerTablePoints, r.LoserTablePoints)).ToArray(),
        x.TiebreakRules.OrderBy(r => r.Sequence).Select(r => new FormatTiebreakRuleInputDto(r.Sequence, r.Criterion, r.SortDirection)).ToArray(),
        x.MovementRules.Select(r => new FormatMovementRuleInputDto(r.MovementType, r.SourceType, r.SourcePhase.Code, r.SourceGroup?.Code, r.SourceSeries?.Code, r.FromPosition, r.ToPosition, r.TargetLevelDelta, r.AppliesIfTargetExists)).ToArray());
}
