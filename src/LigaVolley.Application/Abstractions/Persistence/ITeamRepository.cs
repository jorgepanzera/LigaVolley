using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Teams;
namespace LigaVolley.Application.Abstractions.Persistence;
public interface ITeamRepository { Task<(IReadOnlyList<Team> Items,int Total)> ListAsync(string? search,int? clubId,Gender? gender,bool? active,int page,int pageSize,CancellationToken ct); Task<Team?> GetAsync(int id, bool tracking, CancellationToken ct); Task<bool> NameGenderExistsAsync(string name, Gender gender, int? excludingId, CancellationToken ct); void Add(Team team); }
