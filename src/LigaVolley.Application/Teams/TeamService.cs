using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Clubs;
using LigaVolley.Application.Common;
using LigaVolley.Domain.Clubs;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Teams;
namespace LigaVolley.Application.Teams;
public sealed class TeamService(ITeamRepository repository, IClubRepository clubs, IUnitOfWork unit)
{
    public async Task<IReadOnlyList<TeamSummaryDto>> ListAsync(int? clubId, Gender? gender, bool? active, CancellationToken ct) => (await repository.ListAsync(clubId, gender, active, ct)).Select(ToSummary).ToArray();
    public async Task<TeamDto> GetAsync(int id, CancellationToken ct) => ToDto(await Required(id, false, ct));
    public async Task<TeamDto> CreateAsync(CreateTeamRequest request, CancellationToken ct) { await Unique(request.Name, request.Gender, null, ct); var club = await ResolveClub(request.ClubId, ct); var value = new Team(request.Name, request.Gender, club); repository.Add(value); await unit.SaveChangesAsync(ct); return ToDto(value); }
    public async Task<TeamDto> UpdateAsync(int id, UpdateTeamRequest request, CancellationToken ct) { var value = await Required(id, true, ct); await Unique(request.Name, request.Gender, id, ct); value.Update(request.Name, request.Gender, await ResolveClub(request.ClubId, ct)); await unit.SaveChangesAsync(ct); return ToDto(value); }
    public async Task<TeamDto> SetActiveAsync(int id, bool active, CancellationToken ct) { var value = await Required(id, true, ct); value.SetActive(active); await unit.SaveChangesAsync(ct); return ToDto(value); }
    private async Task<Club?> ResolveClub(int? id, CancellationToken ct) => id.HasValue ? await clubs.GetAsync(id.Value, true, ct) ?? throw new ResourceNotFoundException("Club", id.Value) : null;
    private async Task<Team> Required(int id, bool tracking, CancellationToken ct) => await repository.GetAsync(id, tracking, ct) ?? throw new ResourceNotFoundException("Team", id);
    private async Task Unique(string name, Gender gender, int? id, CancellationToken ct) { var normalized = name?.Trim() ?? ""; if (await repository.NameGenderExistsAsync(normalized, gender, id, ct)) throw new ResourceConflictException("team_name_gender_conflict", $"Team '{normalized}' already exists for gender '{gender}'."); }
    private static ClubSummaryDto? Club(Team x) => x.Club is null ? null : new(x.Club.ClubId, x.Club.Name, x.Club.ShortName, x.Club.Active);
    private static TeamDto ToDto(Team x) => new(x.TeamId, x.Name, x.Gender, Club(x), x.Active);
    private static TeamSummaryDto ToSummary(Team x) => new(x.TeamId, x.Name, x.Gender, x.ClubId, x.Club?.Name, x.Active);
}
