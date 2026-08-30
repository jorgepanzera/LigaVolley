using LigaVolley.Domain.Venues;
namespace LigaVolley.Application.Abstractions.Persistence;
public interface IVenueRepository { Task<(IReadOnlyList<Venue> Items,int Total)> ListAsync(string? search,bool? active,int page,int pageSize,CancellationToken ct); Task<Venue?> GetAsync(int id, bool tracking, CancellationToken ct); Task<bool> NameExistsAsync(string name, int? excludingId, CancellationToken ct); void Add(Venue venue); }
