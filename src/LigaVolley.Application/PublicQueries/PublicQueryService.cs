using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Application.Standings;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.MatchSheets;
using LigaVolley.Application.Clubs;

namespace LigaVolley.Application.PublicQueries;

public sealed class PublicQueryService(IPublicQueryRepository repository, StandingsService standings)
{
    private static readonly CompetitionStatus[] PublicStatuses = [CompetitionStatus.Scheduled, CompetitionStatus.InProgress, CompetitionStatus.Finished, CompetitionStatus.Cancelled];

    public async Task<IReadOnlyList<PublicSeasonDto>> ListSeasonsAsync(CancellationToken ct) =>
        (await repository.ListSeasonsAsync(ct)).Select(x => new PublicSeasonDto(x.SeasonId,x.Year,x.Name)).ToArray();

    public async Task<IReadOnlyList<PublicCompetitionSummaryDto>> ListCompetitionsAsync(int? seasonId,int? divisionId,Domain.Divisions.Gender? gender,CompetitionStatus? status,CancellationToken ct)
    {
        if (status.HasValue && !PublicStatuses.Contains(status.Value)) return [];
        return (await repository.ListCompetitionsAsync(seasonId,divisionId,gender,status,ct)).Select(Summary).ToArray();
    }

    public async Task<PublicCompetitionDto> GetCompetitionAsync(int id,CancellationToken ct)
    {
        var competition=await Competition(id,ct); var teams=await repository.ListTeamsAsync(id,ct); var matches=await repository.ListMatchesAsync(id,ct);
        return new(competition.CompetitionId,competition.Name,Season(competition),Division(competition),competition.PeriodType,competition.StartDate,competition.EndDate,competition.Status,
            teams.Select(x=>new PublicCompetitionTeamDto(x.TeamEntryId,x.TeamId,x.Team.Name)).ToArray(),competition.Phases.OrderBy(x=>x.Sequence).Select(p=>new PublicCompetitionPhaseDto(p.CompetitionPhaseId,p.Code,p.Name,p.PhaseType,p.PhaseRole,p.Sequence,p.Status,p.Groups.OrderBy(x=>x.Sequence).Select(g=>new PublicCompetitionGroupDto(g.PhaseGroupId,g.Code,g.Name,g.GroupRole,g.Sequence)).ToArray(),p.Series.OrderBy(x=>x.Sequence).Select(s=>Series(s,matches)).ToArray())).ToArray());
    }

    public async Task<PublicCompetitionFixtureDto> GetFixtureAsync(int id,int? phaseId,int? phaseGroupId,int? teamEntryId,IReadOnlySet<MatchStatus>? statuses,CancellationToken ct)
    {
        var c=await Competition(id,ct); var all=await repository.ListMatchesAsync(id,ct);
        var matches=all.Where(x=>(!phaseId.HasValue||x.PhaseId==phaseId)&&(!phaseGroupId.HasValue||x.PhaseGroupId==phaseGroupId)&&(!teamEntryId.HasValue||x.HomeTeamEntryId==teamEntryId||x.AwayTeamEntryId==teamEntryId)&&(statuses is null||statuses.Count==0||statuses.Contains(x.Status))).ToArray();
        var phases=c.Phases.Where(p=>!phaseId.HasValue||p.CompetitionPhaseId==phaseId).OrderBy(p=>p.Sequence).Select(p=>
        {
            var pm=matches.Where(x=>x.PhaseId==p.CompetitionPhaseId).ToArray();
            var rounds=p.Groups.Count==0&&p.PhaseType!=PhaseType.Playoff?Rounds(pm.Where(x=>x.SeriesId is null)):[];
            var groups=p.Groups.Where(g=>!phaseGroupId.HasValue||g.PhaseGroupId==phaseGroupId).OrderBy(g=>g.Sequence).Select(g=>new PublicFixtureGroupDto(g.PhaseGroupId,g.Code,g.Name,g.GroupRole,g.Sequence,Rounds(pm.Where(x=>x.PhaseGroupId==g.PhaseGroupId)))).ToArray();
            var series=p.Series.OrderBy(s=>s.Sequence).Select(s=>{var sm=pm.Where(x=>x.SeriesId==s.PlayoffSeriesId).OrderBy(x=>x.MatchNumber).ToArray();var wins=Wins(s,all);return new PublicFixtureSeriesDto(s.PlayoffSeriesId,s.Code,s.Name,s.Sequence,s.Status,Team(s.Team1Entry),Team(s.Team2Entry),s.Team1InitialWins,s.Team2InitialWins,s.WinsRequired,wins.team1Series,wins.team2Series,s.WinnerTeamEntryId,sm.Select(x=>FixtureMatch(x,true)).ToArray());}).ToArray();
            return new PublicFixturePhaseDto(p.CompetitionPhaseId,p.Code,p.Name,p.PhaseType,p.PhaseRole,p.Sequence,rounds,groups,series);
        }).ToArray();
        return new(c.CompetitionId,c.Name,phases);
    }

    public async Task<PublicCompetitionStandingsDto> GetStandingsAsync(int id,int? phaseId,int? phaseGroupId,CancellationToken ct)
    {
        var c=await Competition(id,ct); if(phaseGroupId.HasValue&&!phaseId.HasValue)throw new RequestValidationException("public_invalid_standings_scope","phaseGroupId requires phaseId.");
        var tables=new List<PublicStandingsTableDto>();
        foreach(var phase in c.Phases.Where(x=>x.PhaseType!=PhaseType.Playoff&&(!phaseId.HasValue||x.CompetitionPhaseId==phaseId)).OrderBy(x=>x.Sequence))
        {
            var groups=phase.Groups.Where(x=>!phaseGroupId.HasValue||x.PhaseGroupId==phaseGroupId).OrderBy(x=>x.Sequence).ToArray();
            if(phaseGroupId.HasValue&&groups.Length==0)throw new RequestValidationException("public_invalid_standings_scope","The phase group does not belong to the phase.");
            if(groups.Length==0) tables.Add(Table(await Canonical(c,phase,null,ct),phase,null)); else foreach(var group in groups)tables.Add(Table(await Canonical(c,phase,group,ct),phase,group));
        }
        return new(c.CompetitionId,c.Name,tables);
    }

    public async Task<PublicMatchDto> GetMatchAsync(int matchId,CancellationToken ct)
    {
        var m=await repository.GetMatchAsync(matchId,ct); if(m is null||!IsPublic(m.Competition.Status))throw new ResourceNotFoundException("PublicMatch",matchId);
        var result=m.Status==MatchStatus.Finished&&m.WinnerTeamEntryId.HasValue?new PublicMatchResultDto(m.HomeSets??0,m.AwaySets??0,m.WinnerTeamEntryId.Value,Sets(m)):null;
        return new(m.MatchId,new(m.CompetitionId,m.Competition.Name,m.Competition.SeasonId,m.Competition.Season.Year,m.Competition.DivisionId,m.Competition.Division.Name,m.Competition.Division.Gender),new(m.PhaseId,m.Phase.Code,m.Phase.Name,m.PhaseGroupId,m.PhaseGroup?.Code,m.PhaseGroup?.Name,m.SeriesId,m.Series?.Code,m.Series?.Name),Team(m.HomeTeamEntry)!,Team(m.AwayTeamEntry)!,Date(m.MatchDate),Venue(m),m.Status,m.SeriesId.HasValue?null:m.RoundNumber,m.SeriesId.HasValue?m.MatchNumber:null,result,m.Status is MatchStatus.InProgress or MatchStatus.Suspended or MatchStatus.Finished);
    }

    public async Task<PublicLiveMatchDto> GetLiveAsync(int matchId,CancellationToken ct)
    {
        var m=await repository.GetMatchAsync(matchId,ct); if(m is null||!IsPublic(m.Competition.Status))throw new ResourceNotFoundException("PublicMatch",matchId);
        if(m.Status is not (MatchStatus.InProgress or MatchStatus.Suspended or MatchStatus.Finished))throw new ResourceNotFoundException("PublicLiveMatch",matchId);
        var s=await repository.GetMatchSheetAsync(matchId,ct)??throw new ResourceConflictException("public_live_state_inconsistent","The central live state is unavailable."); var current=s.Sets.OrderByDescending(x=>x.SetNumber).FirstOrDefault();
        var home=s.Teams.SingleOrDefault(x=>x.Side==MatchSide.Home);var away=s.Teams.SingleOrDefault(x=>x.Side==MatchSide.Away);if(home is null||away is null)throw new ResourceConflictException("public_live_state_inconsistent","Live teams are inconsistent.");
        return new(matchId,m.Status,new(home.TeamEntryId,home.TeamEntry.Team.Name,s.HomeSets,m.HomeTeamEntry?.Team.Club is null?null:ClubService.LogoUrl(m.HomeTeamEntry.Team.Club)),new(away.TeamEntryId,away.TeamEntry.Team.Name,s.AwaySets,m.AwayTeamEntry?.Team.Club is null?null:ClubService.LogoUrl(m.AwayTeamEntry.Team.Club)),current?.SetNumber,s.Sets.OrderBy(x=>x.SetNumber).Select(x=>new PublicLiveSetDto(x.SetNumber,x.Status,x.HomePoints,x.AwayPoints,x.WinnerSide)).ToArray(),current?.CurrentServingSide,Court(current,home),Court(current,away),s.LastOperationalUpdateAt,DateTimeOffset.UtcNow,ServingPlayer(m.Status,current,home,away));
    }

    private static PublicServingPlayerDto? ServingPlayer(MatchStatus status, MatchSet? set, MatchTeam home, MatchTeam away)
    {
        if (status != MatchStatus.InProgress || set?.Status != MatchSetStatus.InProgress || set.CurrentServingSide is null)
            return null;

        var team = set.CurrentServingSide == MatchSide.Home ? home : away;
        var lineup = set.Lineups.SingleOrDefault(x => x.MatchTeamId == team.MatchTeamId);
        if (lineup is null) return null;

        // Use the same regular-player court and canonical server resolver as Scorer.
        var court = MatchCourtStateCalculator.Calculate(lineup,
            team.Side == MatchSide.Home ? set.HomeRotationOffset : set.AwayRotationOffset,
            set.Substitutions.Where(x => x.MatchTeamId == team.MatchTeamId), []);
        var serverId = MatchCourtStateCalculator.Server(court);
        var player = team.Players.SingleOrDefault(x => x.MatchPlayerId == serverId);
        if (player?.JerseyNumber is not short jerseyNumber) return null;
        var person = player.CompetitionRosterPlayer.Player.Person;
        return new(jerseyNumber, $"{person.FirstName} {person.LastName}");
    }

    private async Task<StandingsDto> Canonical(Competition c,CompetitionPhase p,CompetitionPhaseGroup? g,CancellationToken ct){try{return await standings.GetAsync(c.CompetitionId,p.CompetitionPhaseId,g?.PhaseGroupId,ct);}catch(ResourceConflictException ex){throw new ResourceConflictException("public_standings_inconsistent",ex.Message);}}
    private static PublicStandingsTableDto Table(StandingsDto x,CompetitionPhase p,CompetitionPhaseGroup? g)=>new(x.PhaseId,x.PhaseCode,x.PhaseName,p.Sequence,x.PhaseGroupId,x.PhaseGroupCode,x.PhaseGroupName,g?.Sequence,x.IsFinal,x.Positions.Select(r=>new PublicStandingRowDto(r.Position,r.TeamEntryId,r.TeamName,r.Played,r.Won,r.Lost,r.SetsWon,r.SetsLost,r.SetRatio,r.PointsWon,r.PointsLost,r.PointRatio,r.TablePoints,r.IsTied)).ToArray());
    private async Task<Competition> Competition(int id,CancellationToken ct){var c=await repository.GetCompetitionAsync(id,ct);if(c is null||!IsPublic(c.Status))throw new ResourceNotFoundException("PublicCompetition",id);return c;}
    private static bool IsPublic(CompetitionStatus s)=>PublicStatuses.Contains(s);
    private static PublicCompetitionSummaryDto Summary(Competition c)=>new(c.CompetitionId,c.Name,Season(c),Division(c),c.PeriodType,c.StartDate,c.EndDate,c.Status);
    private static PublicSeasonDto Season(Competition c)=>new(c.SeasonId,c.Season.Year,c.Season.Name);
    private static PublicDivisionDto Division(Competition c)=>new(c.DivisionId,c.Division.Name,c.Division.LevelOrder,c.Division.Gender);
    private static PublicTeamSummaryDto? Team(Domain.TeamEntries.TeamEntry? x)=>x is null?null:new(x.TeamEntryId,x.Team.Name,x.Team.Club is null?null:ClubService.LogoUrl(x.Team.Club));
    private static PublicVenueDto? Venue(Match m)=>m.Venue is null?null:new(m.Venue.VenueId,m.Venue.Name);
    private static DateTimeOffset? Date(DateTime? value)=>value.HasValue?new DateTimeOffset(DateTime.SpecifyKind(value.Value,DateTimeKind.Utc)):null;
    private static IReadOnlyList<PublicSetResultDto> Sets(Match m)=>m.Sets.OrderBy(x=>x.SetNumber).Select(x=>new PublicSetResultDto(x.SetNumber,x.HomePoints,x.AwayPoints)).ToArray();
    private static PublicMatchScoreDto? Score(Match m)=>m.Status is MatchStatus.Pending or MatchStatus.Scheduled or MatchStatus.Cancelled?null:new(m.HomeSets??m.Sets.Count(x=>x.Status==MatchSetStatus.Finished&&x.WinnerSide==MatchSide.Home),m.AwaySets??m.Sets.Count(x=>x.Status==MatchSetStatus.Finished&&x.WinnerSide==MatchSide.Away),Sets(m));
    private static PublicFixtureMatchDto FixtureMatch(Match m,bool playoff)=>new(m.MatchId,playoff?m.MatchNumber:null,Team(m.HomeTeamEntry)!,Team(m.AwayTeamEntry)!,Date(m.MatchDate),Venue(m),m.Status,Score(m));
    private static IReadOnlyList<PublicFixtureRoundDto> Rounds(IEnumerable<Match> ms)=>ms.GroupBy(x=>x.RoundNumber).OrderBy(x=>x.Key).Select(x=>new PublicFixtureRoundDto(x.Key,x.OrderBy(m=>m.MatchNumber).Select(m=>FixtureMatch(m,false)).ToArray())).ToArray();
    private static (int team1Real,int team2Real,int team1Series,int team2Series) Wins(CompetitionPlayoffSeries s,IEnumerable<Match> matches){var finished=matches.Where(x=>x.SeriesId==s.PlayoffSeriesId&&x.Status==MatchStatus.Finished);var r1=finished.Count(x=>x.WinnerTeamEntryId==s.Team1EntryId);var r2=finished.Count(x=>x.WinnerTeamEntryId==s.Team2EntryId);return(r1,r2,s.Team1InitialWins+r1,s.Team2InitialWins+r2);}
    private static PublicPlayoffSeriesDto Series(CompetitionPlayoffSeries s,IReadOnlyList<Match> matches){var sm=matches.Where(x=>x.SeriesId==s.PlayoffSeriesId).OrderBy(x=>x.MatchNumber).ToArray();var w=Wins(s,matches);return new(s.PlayoffSeriesId,s.Code,s.Name,s.Sequence,s.Status,Participant(s,1),Participant(s,2),s.Team1InitialWins,s.Team2InitialWins,w.team1Real,w.team2Real,w.team1Series,w.team2Series,s.WinsRequired,s.WinnerTeamEntryId,sm.Select(x=>new PublicPlayoffMatchDto(x.MatchId,x.MatchNumber,Team(x.HomeTeamEntry)!,Team(x.AwayTeamEntry)!,x.Status,Date(x.MatchDate),Venue(x),Score(x))).ToArray());}
    private static PublicPlayoffParticipantDto Participant(CompetitionPlayoffSeries s,byte side){var team=side==1?s.Team1Entry:s.Team2Entry;var source=s.ParticipantSources.SingleOrDefault(x=>x.TargetSide==side);var dto=source is null?null:new PublicPlayoffParticipantSourceDto(source.SourceType,source.SourceSeries.Code,$"{(source.SourceType==SeriesParticipantSourceType.SeriesWinner?"Ganador":"Perdedor")} {source.SourceSeries.Name}");return new(side,Team(team),dto);}
    private static PublicCourtDto? Court(MatchSet? set,MatchTeam team){if(set is null)return null;var lineup=set.Lineups.SingleOrDefault(x=>x.MatchTeamId==team.MatchTeamId);if(lineup is null)return null;var state=MatchCourtStateCalculator.Calculate(lineup,team.Side==MatchSide.Home?set.HomeRotationOffset:set.AwayRotationOffset,set.Substitutions.Where(x=>x.MatchTeamId==team.MatchTeamId),set.LiberoReplacements.Where(x=>x.MatchTeamId==team.MatchTeamId));if(state.Count!=6||state.Select(x=>x.PhysicalPosition).Distinct().Count()!=6)throw new ResourceConflictException("public_live_state_inconsistent","The effective court must contain six unique positions.");return new(state.OrderBy(x=>x.PhysicalPosition).Select(x=>{var player=team.Players.Single(p=>p.MatchPlayerId==x.EffectiveMatchPlayerId);var person=player.CompetitionRosterPlayer.Player.Person;return new PublicCourtPositionDto((byte)x.PhysicalPosition,new(player.JerseyNumber?.ToString()??"—",$"{person.FirstName} {person.LastName}",team.Liberos.Any(l=>l.MatchPlayerId==player.MatchPlayerId)));}).ToArray());}
}
