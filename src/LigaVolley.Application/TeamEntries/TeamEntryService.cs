using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Domain.Common;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.TeamEntries;

namespace LigaVolley.Application.TeamEntries;

public sealed class TeamEntryService(
    ITeamEntryRepository entries,
    ICompetitionRepository competitions,
    ITeamRepository teams,
    IUnitOfWork unit)
{
    public async Task<IReadOnlyList<TeamEntryDto>> ListAsync(int competitionId, CancellationToken ct)
    {
        await RequiredCompetition(competitionId, false, ct);
        return (await entries.ListAsync(competitionId, ct)).Select(ToDto).ToArray();
    }

    public async Task<TeamEntryDto> AddAsync(int competitionId, AddTeamEntryRequest request, CancellationToken ct)
    {
        var competition = await RequiredDraftCompetition(competitionId, ct);
        var team = await teams.GetAsync(request.TeamId, true, ct) ?? throw new ResourceNotFoundException("Team", request.TeamId);
        if (await entries.TeamExistsAsync(competitionId, request.TeamId, ct))
            throw new ResourceConflictException("team_already_entered", $"Team '{request.TeamId}' is already entered in competition '{competitionId}'.");
        var validCount = await entries.CountValidAsync(competitionId, ct);
        if (validCount >= competition.CompetitionFormat.MaxTeams)
            throw new ResourceConflictException("competition_max_teams_reached", $"Competition '{competitionId}' already has the maximum of {competition.CompetitionFormat.MaxTeams} valid teams.");
        var entry = new TeamEntry(competition, team, request.Seed);
        entries.Add(entry);
        await unit.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    public async Task<TeamEntryDto> SetSeedAsync(int competitionId, int entryId, SetTeamEntrySeedRequest request, CancellationToken ct)
    {
        await RequiredDraftCompetition(competitionId, ct);
        var entry = await RequiredEntry(competitionId, entryId, true, ct);
        entry.SetSeed(request.Seed);
        await unit.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    public async Task<TeamEntryDto> ChangeStatusAsync(int competitionId, int entryId, ChangeTeamEntryStatusRequest request, CancellationToken ct)
    {
        var competition = await RequiredDraftCompetition(competitionId, ct);
        var entry = await RequiredEntry(competitionId, entryId, true, ct);
        if (!entry.IsValid && request.Status is TeamEntryStatus.Registered or TeamEntryStatus.Active)
        {
            var validCount = await entries.CountValidAsync(competitionId, ct);
            if (validCount >= competition.CompetitionFormat.MaxTeams)
                throw new ResourceConflictException("competition_max_teams_reached", $"Competition '{competitionId}' already has the maximum of {competition.CompetitionFormat.MaxTeams} valid teams.");
        }
        entry.ChangeStatus(request.Status);
        await unit.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    public async Task RemoveAsync(int competitionId, int entryId, CancellationToken ct)
    {
        await RequiredDraftCompetition(competitionId, ct);
        entries.Remove(await RequiredEntry(competitionId, entryId, true, ct));
        await unit.SaveChangesAsync(ct);
    }

    public async Task<TeamEntryRangeValidationDto> ValidateRangeAsync(int competitionId, CancellationToken ct)
    {
        var competition = await RequiredCompetition(competitionId, false, ct);
        var count = await entries.CountValidAsync(competitionId, ct);
        var format = competition.CompetitionFormat;
        return new(competitionId, count, format.MinTeams, format.MaxTeams, count <= format.MaxTeams, count >= format.MinTeams && count <= format.MaxTeams);
    }

    private async Task<Competition> RequiredCompetition(int id, bool tracking, CancellationToken ct)
        => await competitions.GetAsync(id, tracking, ct) ?? throw new ResourceNotFoundException("Competition", id);
    private async Task<Competition> RequiredDraftCompetition(int id, CancellationToken ct)
    {
        var competition = await RequiredCompetition(id, true, ct);
        if (competition.Status != CompetitionStatus.Draft) throw new ResourceConflictException("competition_not_draft", "Team entries can only be changed while the competition is in Draft status.");
        return competition;
    }
    private async Task<TeamEntry> RequiredEntry(int competitionId, int entryId, bool tracking, CancellationToken ct)
        => await entries.GetAsync(competitionId, entryId, tracking, ct) ?? throw new ResourceNotFoundException("TeamEntry", entryId);
    private static TeamEntryDto ToDto(TeamEntry x) => new(x.TeamEntryId, x.TeamId, x.Team.Name, x.Seed, x.Status);
}
