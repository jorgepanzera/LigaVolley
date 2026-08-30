using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Domain.Venues;
namespace LigaVolley.Application.Venues;
public sealed class VenueService(IVenueRepository repository, IUnitOfWork unit)
{
    public async Task<PagedResult<VenueSummaryDto>> ListAsync(string? search,bool? active,int page,int pageSize,CancellationToken ct){Page(ref page,ref pageSize);var result=await repository.ListAsync(search?.Trim(),active,page,pageSize,ct);return new(result.Items.Select(ToSummary).ToArray(),page,pageSize,result.Total);}
    public async Task<VenueDto> GetAsync(int id, CancellationToken ct) => ToDto(await Required(id, false, ct));
    public async Task<VenueDto> CreateAsync(CreateVenueRequest request, CancellationToken ct) { await Unique(request.Name, null, ct); var value = new Venue(request.Name, request.Address); repository.Add(value); await unit.SaveChangesAsync(ct); return ToDto(value); }
    public async Task<VenueDto> UpdateAsync(int id, UpdateVenueRequest request, CancellationToken ct) { var value = await Required(id, true, ct); await Unique(request.Name, id, ct); value.Update(request.Name, request.Address); await unit.SaveChangesAsync(ct); return ToDto(value); }
    public async Task<VenueDto> SetActiveAsync(int id, bool active, CancellationToken ct) { var value = await Required(id, true, ct); value.SetActive(active); await unit.SaveChangesAsync(ct); return ToDto(value); }
    private async Task<Venue> Required(int id, bool tracking, CancellationToken ct) => await repository.GetAsync(id, tracking, ct) ?? throw new ResourceNotFoundException("Venue", id);
    private async Task Unique(string name, int? id, CancellationToken ct) { var normalized = name?.Trim() ?? ""; if (await repository.NameExistsAsync(normalized, id, ct)) throw new ResourceConflictException("venue_name_duplicate", $"Venue name '{normalized}' already exists."); }
    private static VenueDto ToDto(Venue x) => new(x.VenueId, x.Name, x.Address, x.Active);
    private static VenueSummaryDto ToSummary(Venue x) => new(x.VenueId, x.Name, x.Address, x.Active);
    private static void Page(ref int page,ref int size){if(page<1)page=1;if(size<1)size=20;if(size>100)size=100;}
}
