namespace LigaVolley.Application.Abstractions.Storage;

public sealed record StoredClubLogo(string StorageKey, string ContentType);
public sealed record ClubLogoContent(Stream Content, string ContentType) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public interface IClubLogoStorage
{
    Task<StoredClubLogo> SaveAsync(int clubId, Stream content, string contentType, CancellationToken ct);
    Task<ClubLogoContent?> OpenReadAsync(string storageKey, CancellationToken ct);
    Task<bool> ContentEqualsAsync(string storageKey, Stream candidate, string contentType, CancellationToken ct);
    Task DeleteAsync(string storageKey, CancellationToken ct);
}
