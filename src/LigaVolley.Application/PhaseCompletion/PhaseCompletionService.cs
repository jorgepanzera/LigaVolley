using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Application.Standings;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.TeamEntries;

namespace LigaVolley.Application.PhaseCompletion;

public sealed class PhaseCompletionService(ICompetitionRepository competitions,IPhaseCompletionRepository progression,
    IFixtureRepository fixtures,IUnitOfWork unit,StandingsService standingsService)
{
    public async Task<PhaseCompletionPreviewDto> PreviewAsync(int competitionId,int phaseId,CancellationToken ct)
    {
        var context=await Load(competitionId,phaseId,false,ct); var plan=await BuildPlan(context.Competition,context.Phase,ct);
        return ToPreview(context.Competition,context.Phase,plan);
    }

    public Task<PhaseCompletionResultDto> CompleteAsync(int competitionId,int phaseId,CancellationToken ct)
        =>progression.ExecuteExclusiveAsync(competitionId,phaseId,async innerCt=>
        {
            var context=await Load(competitionId,phaseId,true,innerCt);
            if(context.Phase.Status==CompetitionPhaseStatus.Finished)
            {
                var existing=await BuildPlan(context.Competition,context.Phase,innerCt);
                return ToResult(context.Competition,context.Phase,existing,true);
            }
            var plan=await BuildPlan(context.Competition,context.Phase,innerCt);
            if(!plan.CanComplete) throw new ResourceConflictException("phase_cannot_complete",string.Join(" ",plan.Blockers.Select(x=>x.Message)));
            await Apply(context.Competition,context.Phase,plan,innerCt);
            context.Phase.Complete(); await unit.SaveChangesAsync(innerCt);
            return ToResult(context.Competition,context.Phase,plan,false);
        },ct);

    private async Task Apply(Competition competition,CompetitionPhase source,CompletionPlan plan,CancellationToken ct)
    {
        var entries=(await progression.ListTeamEntriesAsync(competition.CompetitionId,ct)).ToDictionary(x=>x.TeamEntryId);
        var existing=(await progression.ListGroupEntriesAsync(competition.CompetitionId,ct)).ToDictionary(x=>(x.PhaseGroupId,x.TeamEntryId));
        var additions=new List<PhaseGroupEntry>();
        foreach(var q in plan.Qualifications.Where(x=>x.TargetGroupId.HasValue))
        {
            var key=(q.TargetGroupId!.Value,q.TeamEntryId);
            if(existing.TryGetValue(key,out var current))
            {
                if(current.SourcePosition!=q.SourcePosition||current.Seed is not null) throw new ResourceConflictException("phase_group_entry_conflict","An existing phase-group participant contradicts the calculated qualification.");
                continue;
            }
            var target=competition.Phases.SelectMany(x=>x.Groups).Single(x=>x.PhaseGroupId==q.TargetGroupId);
            additions.Add(new(target,entries[q.TeamEntryId],(short)q.SourcePosition,null));
        }
        progression.AddGroupEntries(additions);

        foreach(var q in plan.Qualifications.Where(x=>x.TargetSeriesId.HasValue))
        {
            var series=competition.Phases.SelectMany(x=>x.Series).Single(x=>x.PlayoffSeriesId==q.TargetSeriesId);
            var occupied=q.TargetSide==1?series.Team1EntryId:series.Team2EntryId;
            if(occupied.HasValue&&occupied!=q.TeamEntryId) throw new ResourceConflictException("playoff_series_participant_conflict","A playoff-series side is occupied by another team.");
            if(!occupied.HasValue) series.AssignParticipant(q.TargetSide!.Value,entries[q.TeamEntryId]);
        }

        foreach(var summary in plan.GeneratedFixtures)
        {
            if(summary.PhaseGroupId.HasValue)
            {
                if(await fixtures.GenerationExistsAsync(competition.CompetitionId,summary.PhaseId,summary.PhaseGroupId,ct)) continue;
                var targetPhase=competition.Phases.Single(x=>x.CompetitionPhaseId==summary.PhaseId); var group=targetPhase.Groups.Single(x=>x.PhaseGroupId==summary.PhaseGroupId);
                var participantIds=plan.Qualifications.Where(x=>x.TargetGroupId==group.PhaseGroupId).Select(x=>x.TeamEntryId)
                    .Concat(existing.Keys.Where(x=>x.PhaseGroupId==group.PhaseGroupId).Select(x=>x.TeamEntryId)).Distinct().OrderBy(x=>x).ToArray();
                var seed=StableSeed(competition.CompetitionId,source.CompetitionPhaseId,targetPhase.CompetitionPhaseId,group.PhaseGroupId);
                var pairings=RoundRobinFixtureGenerator.Generate(participantIds,seed,group.FixtureMode==FixtureMode.MirroredHomeAway);
                fixtures.AddGeneration(new(competition,targetPhase,group,seed,DateTime.UtcNow));
                fixtures.AddMatches(pairings.Select(x=>new Match(competition,targetPhase,group,entries[x.HomeParticipantId],entries[x.AwayParticipantId],x.RoundNumber,x.MatchNumber)));
            }
            else if(summary.SeriesId.HasValue)
            {
                var series=competition.Phases.SelectMany(x=>x.Series).Single(x=>x.PlayoffSeriesId==summary.SeriesId); var targetPhase=competition.Phases.Single(x=>x.CompetitionPhaseId==summary.PhaseId);
                var allMatches=await progression.ListPhaseMatchesAsync(competition.CompetitionId,targetPhase.CompetitionPhaseId,ct);
                if(allMatches.Any(x=>x.SeriesId==series.PlayoffSeriesId)) continue;
                fixtures.AddMatches([new Match(competition,targetPhase,series,entries[series.Team1EntryId!.Value],entries[series.Team2EntryId!.Value],1)]);
            }
        }
    }

    private async Task<CompletionPlan> BuildPlan(Competition competition,CompetitionPhase phase,CancellationToken ct)
    {
        ValidatePhase(phase);
        var matches=await progression.ListPhaseMatchesAsync(competition.CompetitionId,phase.CompetitionPhaseId,ct);
        if(phase.Groups.Count>0&&matches.Any(x=>!x.PhaseGroupId.HasValue||phase.Groups.All(g=>g.PhaseGroupId!=x.PhaseGroupId))) throw Conflict("phase_scope_invalid","Phase matches do not belong to a valid group scope.");
        if(phase.Groups.Count==0&&matches.Any(x=>x.PhaseGroupId.HasValue)) throw Conflict("phase_scope_invalid","Ungrouped phase contains group-scoped matches.");
        var tables=new List<StandingsDto>();
        if(phase.Groups.Count==0) tables.Add(await standingsService.GetAsync(competition.CompetitionId,phase.CompetitionPhaseId,null,ct));
        else foreach(var group in phase.Groups.OrderBy(x=>x.Sequence)) tables.Add(await standingsService.GetAsync(competition.CompetitionId,phase.CompetitionPhaseId,group.PhaseGroupId,ct));
        var blockers=new List<PhaseCompletionBlockerDto>();
        var cancelled=matches.Where(x=>x.Status==MatchStatus.Cancelled).Select(x=>x.MatchId).ToArray();
        var unresolved=matches.Where(x=>x.Status!=MatchStatus.Finished&&x.Status!=MatchStatus.Cancelled).Select(x=>x.MatchId).ToArray();
        if(unresolved.Length>0) blockers.Add(new("phase_matches_unresolved","The phase has unresolved matches.",unresolved,null,null));
        if(cancelled.Length>0) blockers.Add(new("phase_cancelled_matches","Cancelled matches are not resolved sporting results in v1.",cancelled,null,null));
        if(blockers.Count>0) return new(false,blockers,tables,[],[],[]);

        var qualifications=new List<PlannedQualification>();
        var rules=competition.CompetitionFormat.QualificationRules.Where(x=>x.SourceFormatPhaseId==phase.FormatPhaseId).OrderBy(x=>x.Sequence).ToArray();
        foreach(var rule in rules)
        {
            StandingsDto table;
            if(phase.Groups.Count>0)
            {
                if(!rule.SourceFormatGroupId.HasValue) throw Conflict("qualification_source_group_required","A grouped phase qualification must identify its source group.");
                var sourceGroup=phase.Groups.SingleOrDefault(x=>x.FormatGroupId==rule.SourceFormatGroupId)??throw Conflict("qualification_configuration_invalid","Qualification source group is not part of the source phase.");
                table=tables.Single(x=>x.PhaseGroupId==sourceGroup.PhaseGroupId);
            }
            else
            {
                if(rule.SourceFormatGroupId.HasValue) throw Conflict("qualification_configuration_invalid","Ungrouped phase cannot use a source group.");
                table=tables.Single();
            }
            var selected=QualificationSelector.Select(rule.SelectionMode,rule.FromPosition,rule.ToPosition,rule.QualificationRuleId,table.Positions,out var boundaryBlocker);
            if(boundaryBlocker is not null) blockers.Add(boundaryBlocker);
            if(blockers.Count>0) continue;
            var targetPhase=competition.Phases.SingleOrDefault(x=>x.FormatPhaseId==rule.TargetFormatPhaseId);
            if(targetPhase is null||targetPhase.Sequence<=phase.Sequence) throw Conflict("qualification_target_invalid","Qualification target phase is missing or is not after the source phase.");
            if(rule.TargetType==QualificationTargetType.Group)
            {
                var targetGroup=targetPhase.Groups.SingleOrDefault(x=>x.FormatGroupId==rule.TargetFormatGroupId);
                if(targetGroup is null) throw Conflict("qualification_target_invalid","Qualification target group is invalid.");
                if(targetGroup.CarryOverMode!=CarryOverMode.None) throw Conflict("carry_over_mode_not_supported","Only CarryOverMode.None is supported in v1.");
                qualifications.AddRange(selected.Select(x=>Planned(rule,x,targetPhase,targetGroup,null)));
            }
            else
            {
                if(rule.SelectionMode!=QualificationSelectionMode.PositionRange||rule.FromPosition!=rule.ToPosition||selected.Count!=1||rule.TargetSide is not 1 and not 2) throw Conflict("qualification_configuration_invalid","Series qualification must select exactly one position and a valid side.");
                var series=targetPhase.Series.SingleOrDefault(x=>x.FormatSeriesId==rule.TargetFormatSeriesId)??throw Conflict("qualification_target_invalid","Qualification target series is invalid.");
                qualifications.Add(Planned(rule,selected[0],targetPhase,null,series));
            }
        }
        if(blockers.Count>0)return new(false,blockers,tables,[],[],[]);
        ValidateDuplicateEffects(qualifications);
        await ValidateExistingEffects(competition,qualifications,ct);
        var generated=new List<GeneratedFixtureSummaryDto>();
        foreach(var grouping in qualifications.Where(x=>x.TargetGroupId.HasValue).GroupBy(x=>new{x.TargetPhaseId,x.TargetGroupId}))
        {
            if(await fixtures.GenerationExistsAsync(competition.CompetitionId,grouping.Key.TargetPhaseId,grouping.Key.TargetGroupId,ct))continue;
            var existing=await progression.ListGroupEntriesAsync(competition.CompetitionId,ct); var count=grouping.Select(x=>x.TeamEntryId).Concat(existing.Where(x=>x.PhaseGroupId==grouping.Key.TargetGroupId).Select(x=>x.TeamEntryId)).Distinct().Count();
            if(count>=2){var group=competition.Phases.Single(x=>x.CompetitionPhaseId==grouping.Key.TargetPhaseId).Groups.Single(x=>x.PhaseGroupId==grouping.Key.TargetGroupId);generated.Add(new(grouping.Key.TargetPhaseId,group.PhaseGroupId,null,MatchCount(count,group.FixtureMode==FixtureMode.MirroredHomeAway)));}
        }
        var projectedSeries=new List<ResolvedSeriesDto>();
        foreach(var series in competition.Phases.SelectMany(x=>x.Series))
        {
            var t1=qualifications.LastOrDefault(x=>x.TargetSeriesId==series.PlayoffSeriesId&&x.TargetSide==1)?.TeamEntryId??series.Team1EntryId;
            var t2=qualifications.LastOrDefault(x=>x.TargetSeriesId==series.PlayoffSeriesId&&x.TargetSide==2)?.TeamEntryId??series.Team2EntryId;
            var status=t1.HasValue&&t2.HasValue&&series.Status==PlayoffSeriesStatus.Pending?PlayoffSeriesStatus.Ready:series.Status;
            if(qualifications.Any(x=>x.TargetSeriesId==series.PlayoffSeriesId)){projectedSeries.Add(new(series.PlayoffSeriesId,series.Code,t1,t2,status));if(status==PlayoffSeriesStatus.Ready){var target=competition.Phases.Single(x=>x.Series.Contains(series));var existingMatches=await progression.ListPhaseMatchesAsync(competition.CompetitionId,target.CompetitionPhaseId,ct);if(!existingMatches.Any(x=>x.SeriesId==series.PlayoffSeriesId))generated.Add(new(target.CompetitionPhaseId,null,series.PlayoffSeriesId,1));}}
        }
        return new(true,[],tables,qualifications,generated,projectedSeries);
    }

    private async Task ValidateExistingEffects(Competition competition,List<PlannedQualification> qualifications,CancellationToken ct)
    {
        var existing=await progression.ListGroupEntriesAsync(competition.CompetitionId,ct);
        foreach(var target in qualifications.Where(x=>x.TargetGroupId.HasValue).GroupBy(x=>x.TargetGroupId!.Value))
        {
            var expected=target.ToDictionary(x=>x.TeamEntryId);
            foreach(var row in existing.Where(x=>x.PhaseGroupId==target.Key))if(!expected.TryGetValue(row.TeamEntryId,out var q)||q.SourcePosition!=row.SourcePosition||row.Seed is not null)throw Conflict("phase_group_entry_conflict","Persisted group participants contradict the calculated qualification.");
        }
        foreach(var q in qualifications.Where(x=>x.TargetSeriesId.HasValue)){var series=competition.Phases.SelectMany(x=>x.Series).Single(x=>x.PlayoffSeriesId==q.TargetSeriesId);var current=q.TargetSide==1?series.Team1EntryId:series.Team2EntryId;if(current.HasValue&&current!=q.TeamEntryId)throw Conflict("playoff_series_participant_conflict","Persisted series participant contradicts the calculated qualification.");var other=q.TargetSide==1?series.Team2EntryId:series.Team1EntryId;if(other==q.TeamEntryId)throw Conflict("playoff_series_participant_conflict","The same team cannot occupy both series sides.");}
    }
    private static void ValidateDuplicateEffects(List<PlannedQualification> q)
    {
        if(q.Where(x=>x.TargetGroupId.HasValue).GroupBy(x=>new{x.TargetGroupId,x.TeamEntryId}).Any(x=>x.Count()>1)||q.Where(x=>x.TargetSeriesId.HasValue).GroupBy(x=>new{x.TargetSeriesId,x.TargetSide}).Any(x=>x.Select(y=>y.TeamEntryId).Distinct().Count()>1))throw Conflict("qualification_configuration_invalid","Qualification rules produce duplicate or contradictory effects.");
    }
    private async Task<(Competition Competition,CompetitionPhase Phase)> Load(int competitionId,int phaseId,bool tracking,CancellationToken ct){var competition=await competitions.GetAsync(competitionId,tracking,ct)??throw new ResourceNotFoundException("Competition",competitionId);var phase=competition.Phases.SingleOrDefault(x=>x.CompetitionPhaseId==phaseId)??throw new ResourceNotFoundException("CompetitionPhase",phaseId);return(competition,phase);}
    private static void ValidatePhase(CompetitionPhase phase){if(phase.PhaseType==PhaseType.Playoff)throw Conflict("phase_completion_not_supported","Phase completion is supported only for table phases.");if(phase.Status is CompetitionPhaseStatus.Pending or CompetitionPhaseStatus.Cancelled)throw Conflict("phase_completion_status_invalid","Only an in-progress or already-finished phase can be completed.");}
    private static PlannedQualification Planned(FormatQualificationRule r,StandingPositionDto p,CompetitionPhase tp,CompetitionPhaseGroup? g,CompetitionPlayoffSeries? s)=>new(r.QualificationRuleId,p.TeamEntryId,p.TeamName,p.Position,tp.CompetitionPhaseId,tp.Code,g?.PhaseGroupId,g?.Code,s?.PlayoffSeriesId,s?.Code,r.TargetSide);
    private static int MatchCount(int n,bool mirrored)=>(n*(n-1)/2)*(mirrored?2:1);
    private static int StableSeed(int c,int s,int p,int g){unchecked{uint h=2166136261;foreach(var x in new[]{c,s,p,g}){h^=(uint)x;h*=16777619;}return(int)(h&0x7fffffff);}}
    private static ResourceConflictException Conflict(string code,string message)=>new(code,message);
    private static PhaseCompletionPreviewDto ToPreview(Competition c,CompetitionPhase p,CompletionPlan x)=>new(c.CompetitionId,p.CompetitionPhaseId,p.Code,p.Name,x.CanComplete,x.Blockers,x.Standings,x.Qualifications.Select(QPreview).ToArray(),x.GeneratedFixtures,x.ResolvedSeries);
    private static PhaseCompletionResultDto ToResult(Competition c,CompetitionPhase p,CompletionPlan x,bool already)=>new(c.CompetitionId,p.CompetitionPhaseId,p.Status,already,x.Standings,x.Qualifications.Select(q=>new QualifiedTeamDto(q.RuleId,q.TeamEntryId,q.TeamName,q.SourcePosition,q.TargetPhaseCode,q.TargetGroupCode,q.TargetSeriesCode,q.TargetSide)).ToArray(),x.GeneratedFixtures,x.ResolvedSeries);
    private static QualificationPreviewDto QPreview(PlannedQualification q)=>new(q.RuleId,q.TeamEntryId,q.TeamName,q.SourcePosition,q.TargetPhaseCode,q.TargetGroupCode,q.TargetSeriesCode,q.TargetSide);
    private sealed record CompletionPlan(bool CanComplete,IReadOnlyList<PhaseCompletionBlockerDto> Blockers,IReadOnlyList<StandingsDto> Standings,List<PlannedQualification> Qualifications,IReadOnlyList<GeneratedFixtureSummaryDto> GeneratedFixtures,IReadOnlyList<ResolvedSeriesDto> ResolvedSeries);
    private sealed record PlannedQualification(int RuleId,int TeamEntryId,string TeamName,int SourcePosition,int TargetPhaseId,string TargetPhaseCode,int? TargetGroupId,string? TargetGroupCode,int? TargetSeriesId,string? TargetSeriesCode,byte? TargetSide);
}
