using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Domain.Clubs;
namespace LigaVolley.Application.Clubs;
public sealed class ClubService(IClubRepository repository, IUnitOfWork unit)
{
    public async Task<IReadOnlyList<ClubSummaryDto>> ListAsync(bool? active, CancellationToken ct) => (await repository.ListAsync(active, ct)).Select(ToSummary).ToArray();
    public async Task<ClubDto> GetAsync(int id, CancellationToken ct) => ToDto(await Required(id, false, ct));
    public async Task<ClubDto> CreateAsync(CreateClubRequest request, CancellationToken ct) { await Unique(request.Name, null, ct); var value = new Club(request.Name, request.ShortName); repository.Add(value); await unit.SaveChangesAsync(ct); return ToDto(value); }
    public async Task<ClubDto> UpdateAsync(int id, UpdateClubRequest request, CancellationToken ct) { var value = await Required(id, true, ct); await Unique(request.Name, id, ct); value.Update(request.Name, request.ShortName); await unit.SaveChangesAsync(ct); return ToDto(value); }
    public async Task<ClubDto> SetActiveAsync(int id, bool active, CancellationToken ct) { var value = await Required(id, true, ct); value.SetActive(active); await unit.SaveChangesAsync(ct); return ToDto(value); }
    private async Task<Club> Required(int id, bool tracking, CancellationToken ct) => await repository.GetAsync(id, tracking, ct) ?? throw new ResourceNotFoundException("Club", id);
    private async Task Unique(string name, int? id, CancellationToken ct) { var normalized = name?.Trim() ?? ""; if (await repository.NameExistsAsync(normalized, id, ct)) throw new ResourceConflictException("club_name_conflict", $"Club name '{normalized}' already exists."); }
    private static ClubDto ToDto(Club x) => new(x.ClubId, x.Name, x.ShortName, x.Active);
    private static ClubSummaryDto ToSummary(Club x) => new(x.ClubId, x.Name, x.ShortName, x.Active);
}
