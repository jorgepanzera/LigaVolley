using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Domain.Clubs;
using LigaVolley.Application.Abstractions.Storage;
namespace LigaVolley.Application.Clubs;
public sealed class ClubService(IClubRepository repository, IUnitOfWork unit, IClubLogoStorage logos)
{
    public ClubService(IClubRepository repository,IUnitOfWork unit):this(repository,unit,new UnavailableLogoStorage()){}
    public async Task<PagedResult<ClubSummaryDto>> ListAsync(string? search,bool? active,int page,int pageSize,CancellationToken ct){Page(ref page,ref pageSize);var result=await repository.ListAsync(search?.Trim(),active,page,pageSize,ct);return new(result.Items.Select(ToSummary).ToArray(),page,pageSize,result.Total);}
    public async Task<ClubDto> GetAsync(int id, CancellationToken ct) => ToDto(await Required(id, false, ct));
    public async Task<ClubDto> CreateAsync(CreateClubRequest request, CancellationToken ct) { await Unique(request.Name, null, ct); var value = new Club(request.Name, request.ShortName); repository.Add(value); await unit.SaveChangesAsync(ct); return ToDto(value); }
    public async Task<ClubDto> UpdateAsync(int id, UpdateClubRequest request, CancellationToken ct) { var value = await Required(id, true, ct); await Unique(request.Name, id, ct); value.Update(request.Name, request.ShortName); await unit.SaveChangesAsync(ct); return ToDto(value); }
    public async Task<ClubDto> SetActiveAsync(int id, bool active, CancellationToken ct) { var value = await Required(id, true, ct); value.SetActive(active); await unit.SaveChangesAsync(ct); return ToDto(value); }
    public async Task<ClubLogoDto> ReplaceLogoAsync(int id,Stream content,string contentType,CancellationToken ct){var club=await Required(id,true,ct);var old=club.LogoStorageKey;var saved=await logos.SaveAsync(id,content,contentType,ct);try{club.ReplaceLogo(saved.StorageKey,saved.ContentType);await unit.SaveChangesAsync(ct);}catch{await logos.DeleteAsync(saved.StorageKey,ct);throw;}if(old is not null)await logos.DeleteAsync(old,ct);return new(id,LogoUrl(club)!,club.LogoVersion);}
    public async Task RemoveLogoAsync(int id,CancellationToken ct){var club=await Required(id,true,ct);var old=club.LogoStorageKey;if(old is null)return;club.RemoveLogo();await unit.SaveChangesAsync(ct);await logos.DeleteAsync(old,ct);}
    public async Task<ClubLogoContent> OpenLogoAsync(int id,CancellationToken ct){var club=await Required(id,false,ct);if(club.LogoStorageKey is null)throw new ResourceNotFoundException("ClubLogo",id);return await logos.OpenReadAsync(club.LogoStorageKey,ct)??throw new ResourceNotFoundException("ClubLogo",id);}
    private async Task<Club> Required(int id, bool tracking, CancellationToken ct) => await repository.GetAsync(id, tracking, ct) ?? throw new ResourceNotFoundException("Club", id);
    private async Task Unique(string name, int? id, CancellationToken ct) { var normalized = name?.Trim() ?? ""; if (await repository.NameExistsAsync(normalized, id, ct)) throw new ResourceConflictException("club_name_duplicate", $"Club name '{normalized}' already exists."); }
    public static string? LogoUrl(Club x)=>x.LogoStorageKey is null?null:$"/api/public/clubs/{x.ClubId}/logo?v={x.LogoVersion}";
    private static ClubDto ToDto(Club x) => new(x.ClubId, x.Name, x.ShortName, x.Active,LogoUrl(x));
    private static ClubSummaryDto ToSummary(Club x) => new(x.ClubId, x.Name, x.ShortName, x.Active,LogoUrl(x));
    private static void Page(ref int page,ref int size){if(page<1)page=1;if(size<1)size=20;if(size>100)size=100;}
    private sealed class UnavailableLogoStorage:IClubLogoStorage
    { public Task<StoredClubLogo> SaveAsync(int id,Stream s,string t,CancellationToken ct)=>throw new InvalidOperationException();public Task<ClubLogoContent?> OpenReadAsync(string k,CancellationToken ct)=>Task.FromResult<ClubLogoContent?>(null);public Task<bool> ContentEqualsAsync(string k,Stream s,string t,CancellationToken ct)=>throw new InvalidOperationException();public Task DeleteAsync(string k,CancellationToken ct)=>Task.CompletedTask; }
}
