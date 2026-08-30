using System.Text.Json;
using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Domain.Common;
using LigaVolley.Domain.CompetitionFormats;

namespace LigaVolley.Application.CompetitionFormats;

public sealed class CompetitionFormatService(ICompetitionFormatRepository repository, IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<CompetitionFormatSummaryDto>> ListAsync(bool? active, short? teamCount, CancellationToken ct)
    {
        var formats=await repository.ListAsync(active,teamCount,ct);var result=new List<CompetitionFormatSummaryDto>(formats.Count);
        foreach(var format in formats)result.Add(ToSummary(format,await repository.GetUsageAsync(format.CompetitionFormatId,ct)));
        return result;
    }
    public async Task<CompetitionFormatDto> GetAsync(int id,CancellationToken ct){var format=await Required(id,false,ct);return ToDto(format,await repository.GetUsageAsync(id,ct));}
    public Task<CompetitionFormatValidationDto> ValidateAsync(ValidateCompetitionFormatRequest request)=>Task.FromResult(CompetitionFormatDefinitionFactory.Validate(request.MinTeams,request.MaxTeams,request.Definition,request.Code,request.Name));
    public async Task<CompetitionFormatDto> CreateAsync(CreateCompetitionFormatRequest request,CancellationToken ct)
    {await Unique(request.Code,null,ct);EnsureValid(CompetitionFormatDefinitionFactory.Validate(request.MinTeams,request.MaxTeams,request.Definition,request.Code,request.Name));var format=CompetitionFormatDefinitionFactory.Build(request.Code,request.Name,request.Description,request.MinTeams,request.MaxTeams,request.Definition);format.SetActive(false);repository.Add(format);await unitOfWork.SaveChangesAsync(ct);return ToDto(format,new(0,0));}
    public async Task<CompetitionFormatDto> UpdateAsync(int id,UpdateCompetitionFormatRequest request,CancellationToken ct)
    {
        var format=await Required(id,true,ct);var usage=await repository.GetUsageAsync(id,ct);var structuralChange=!string.Equals(format.Code,request.Code?.Trim(),StringComparison.Ordinal)||format.MinTeams!=request.MinTeams||format.MaxTeams!=request.MaxTeams||!DefinitionEquals(ToDefinition(format),request.Definition);
        if(usage.IsStructurallyLocked&&structuralChange)throw new ResourceConflictException("competition_format_structurally_locked","The competition format structure is locked because an operational competition uses it.");
        if(structuralChange){await Unique(request.Code??string.Empty,id,ct);EnsureValid(CompetitionFormatDefinitionFactory.Validate(request.MinTeams,request.MaxTeams,request.Definition,request.Code,request.Name));var replacement=CompetitionFormatDefinitionFactory.Build(request.Code!,request.Name,request.Description,request.MinTeams,request.MaxTeams,request.Definition);repository.PrepareReplacement(format);format.ReplaceWith(replacement);}else format.UpdateDescriptiveMetadata(request.Name,request.Description);
        await unitOfWork.SaveChangesAsync(ct);return ToDto(format,usage);
    }
    public async Task<CompetitionFormatDto> CloneAsync(int id,CloneCompetitionFormatRequest request,CancellationToken ct)
    {var source=await Required(id,false,ct);await Unique(request.Code,null,ct);var definition=ToDefinition(source);EnsureValid(CompetitionFormatDefinitionFactory.Validate(source.MinTeams,source.MaxTeams,definition,request.Code,request.Name));var clone=CompetitionFormatDefinitionFactory.Build(request.Code,request.Name,request.Description??source.Description,source.MinTeams,source.MaxTeams,definition);clone.SetActive(false);repository.Add(clone);await unitOfWork.SaveChangesAsync(ct);return ToDto(clone,new(0,0));}
    public async Task<CompetitionFormatDto> SetActiveAsync(int id,bool active,CancellationToken ct)
    {var format=await Required(id,true,ct);var usage=await repository.GetUsageAsync(id,ct);if(active)EnsureValid(CompetitionFormatDefinitionFactory.Validate(format.MinTeams,format.MaxTeams,ToDefinition(format),format.Code,format.Name));format.SetActive(active);await unitOfWork.SaveChangesAsync(ct);return ToDto(format,usage);}
    private async Task<CompetitionFormat> Required(int id,bool tracking,CancellationToken ct)=>await repository.GetAsync(id,tracking,ct)??throw new ResourceNotFoundException("CompetitionFormat",id);
    private async Task Unique(string code,int? id,CancellationToken ct){var normalized=code?.Trim()??"";if(await repository.CodeExistsAsync(normalized,id,ct))throw new ResourceConflictException("competition_format_code_conflict",$"Competition format code '{normalized}' already exists.");}
    private static void EnsureValid(CompetitionFormatValidationDto validation){if(!validation.IsValid)throw new DomainValidationException(string.Join(" ",validation.Errors.Select(x=>$"{x.Path}: {x.Message}")));}
    private static bool DefinitionEquals(CompetitionFormatDefinitionDto left,CompetitionFormatDefinitionDto right)=>JsonSerializer.Serialize(left)==JsonSerializer.Serialize(right);
    private static CompetitionFormatSummaryDto ToSummary(CompetitionFormat x,CompetitionFormatUsage u)=>new(x.CompetitionFormatId,x.Code,x.Name,x.MinTeams,x.MaxTeams,x.Active,u.Used,u.IsStructurallyLocked,u.DraftCompetitionCount,u.OperationalCompetitionCount);
    private static CompetitionFormatDto ToDto(CompetitionFormat x,CompetitionFormatUsage u)=>new(x.CompetitionFormatId,x.Code,x.Name,x.Description,x.MinTeams,x.MaxTeams,x.Active,ToDefinition(x),u.Used,u.IsStructurallyLocked,u.DraftCompetitionCount,u.OperationalCompetitionCount);
    public static CompetitionFormatDefinitionDto ToDefinition(CompetitionFormat x)=>new(
        x.Phases.OrderBy(p=>p.Sequence).Select(p=>new FormatPhaseInputDto(p.Code,p.Name,p.PhaseType,p.PhaseRole,p.Sequence,p.Rounds,p.FixtureMode,p.Groups.OrderBy(g=>g.Sequence).Select(g=>new FormatGroupInputDto(g.Code,g.Name,g.GroupRole,g.Sequence,g.Rounds,g.FixtureMode,g.CarryOverMode)).ToArray(),p.Series.OrderBy(s=>s.Sequence).Select(s=>new FormatPlayoffSeriesInputDto(s.Code,s.Name,s.Sequence,s.WinsRequired,s.Team1InitialWins,s.Team2InitialWins,s.ParticipantSources.OrderBy(q=>q.TargetSide).Select(q=>new SeriesParticipantSourceInputDto(q.TargetSide,q.SourceType,q.SourceSeries.Code)).ToArray())).ToArray())).ToArray(),
        x.QualificationRules.OrderBy(r=>r.Sequence).Select(r=>new FormatQualificationRuleInputDto(r.SourcePhase.Code,r.SourceGroup?.Code,r.SelectionMode,r.FromPosition,r.ToPosition,r.TargetType,r.TargetPhase.Code,r.TargetGroup?.Code,r.TargetSeries?.Code,r.TargetSide,r.Sequence)).ToArray(),
        x.ScoringRules.OrderBy(r=>r.LoserSets).Select(r=>new FormatScoringRuleInputDto(r.WinnerSets,r.LoserSets,r.WinnerTablePoints,r.LoserTablePoints)).ToArray(),
        x.TiebreakRules.OrderBy(r=>r.Sequence).Select(r=>new FormatTiebreakRuleInputDto(r.Sequence,r.Criterion,r.SortDirection)).ToArray(),
        x.MovementRules.OrderBy(r=>r.MovementType).Select(r=>new FormatMovementRuleInputDto(r.MovementType,r.SourceType,r.SourcePhase.Code,r.SourceGroup?.Code,r.SourceSeries?.Code,r.FromPosition,r.ToPosition,r.TargetLevelDelta,r.AppliesIfTargetExists)).ToArray());
}
