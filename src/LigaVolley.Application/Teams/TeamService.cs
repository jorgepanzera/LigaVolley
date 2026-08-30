using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Clubs;
using LigaVolley.Application.Common;
using LigaVolley.Domain.Clubs;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Teams;
namespace LigaVolley.Application.Teams;
public sealed class TeamService(ITeamRepository repository, IClubRepository clubs, IUnitOfWork unit)
{
    public async Task<PagedResult<TeamSummaryDto>> ListAsync(string? search,int? clubId,Gender? gender,bool? active,int page,int pageSize,CancellationToken ct){Page(ref page,ref pageSize);var result=await repository.ListAsync(search?.Trim(),clubId,gender,active,page,pageSize,ct);return new(result.Items.Select(ToSummary).ToArray(),page,pageSize,result.Total);}
    public async Task<TeamDto> GetAsync(int id, CancellationToken ct) => ToDto(await Required(id, false, ct));
    public async Task<TeamDto> CreateAsync(CreateTeamRequest request, CancellationToken ct) { if(!request.ClubId.HasValue)throw new RequestValidationException("team_club_not_found","ClubId is required.");await Unique(request.Name, request.Gender, null, ct); var club = await ResolveClub(request.ClubId, ct);if(!club!.Active)throw new ResourceConflictException("team_club_inactive","Teams can only be created for an active club."); var value = new Team(request.Name, request.Gender, club); repository.Add(value); await unit.SaveChangesAsync(ct); return ToDto(value); }
    public async Task<TeamDto> UpdateAsync(int id, UpdateTeamRequest request, CancellationToken ct) { var value = await Required(id, true, ct); await Unique(request.Name, request.Gender, id, ct); value.Update(request.Name, request.Gender, value.Club); await unit.SaveChangesAsync(ct); return ToDto(value); }
    public async Task<TeamDto> SetActiveAsync(int id, bool active, CancellationToken ct) { var value = await Required(id, true, ct); value.SetActive(active); await unit.SaveChangesAsync(ct); return ToDto(value); }
    private async Task<Club?> ResolveClub(int? id, CancellationToken ct) => id.HasValue ? await clubs.GetAsync(id.Value, true, ct) ?? throw new ResourceNotFoundException("Club", id.Value) : null;
    private async Task<Team> Required(int id, bool tracking, CancellationToken ct) => await repository.GetAsync(id, tracking, ct) ?? throw new ResourceNotFoundException("Team", id);
    private async Task Unique(string name, Gender gender, int? id, CancellationToken ct) { var normalized = name?.Trim() ?? ""; if (await repository.NameGenderExistsAsync(normalized, gender, id, ct)) throw new ResourceConflictException("team_name_duplicate", $"Team '{normalized}' already exists for gender '{gender}'."); }
    private static ClubSummaryDto? Club(Team x) => x.Club is null ? null : new(x.Club.ClubId, x.Club.Name, x.Club.ShortName, x.Club.Active,ClubService.LogoUrl(x.Club));
    private static TeamDto ToDto(Team x) => new(x.TeamId, x.Name, x.Gender, Club(x), x.Active);
    private static TeamSummaryDto ToSummary(Team x) => new(x.TeamId, x.Name, x.Gender, x.ClubId, x.Club?.Name, x.Active,x.Club is null?null:ClubService.LogoUrl(x.Club));
    private static void Page(ref int page,ref int size){if(page<1)page=1;if(size<1)size=20;if(size>100)size=100;}
}
