using LigaVolley.Domain.Clubs;
namespace LigaVolley.Application.Abstractions.Persistence;
public interface IClubRepository { Task<(IReadOnlyList<Club> Items,int Total)> ListAsync(string? search,bool? active,int page,int pageSize,CancellationToken ct); Task<Club?> GetAsync(int id, bool tracking, CancellationToken ct); Task<bool> NameExistsAsync(string name, int? excludingId, CancellationToken ct); void Add(Club club); }
