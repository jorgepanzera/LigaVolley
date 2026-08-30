using LigaVolley.Application.Abstractions.Storage;
using LigaVolley.Application.Common;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace LigaVolley.Infrastructure.Storage;

public sealed class ClubLogoStorageOptions { public string RootPath { get; set; } = "App_Data/club-logos"; }

internal sealed class FileSystemClubLogoStorage : IClubLogoStorage
{
    private const int MaxBytes=2*1024*1024; private readonly string root;
    public FileSystemClubLogoStorage(IOptions<ClubLogoStorageOptions> options)
    { root=Path.GetFullPath(options.Value.RootPath,AppContext.BaseDirectory); Directory.CreateDirectory(root); }

    public async Task<StoredClubLogo> SaveAsync(int clubId,Stream content,string contentType,CancellationToken ct)
    {
        var normalized=await NormalizeAsync(content,contentType,ct);var extension=contentType switch{"image/png"=>"png","image/jpeg"=>"jpg",_=>"webp"};var key=$"clubs/{clubId}/{Guid.NewGuid():N}.{extension}";var path=Resolve(key);Directory.CreateDirectory(Path.GetDirectoryName(path)!);try{await File.WriteAllBytesAsync(path,normalized,ct);return new(key,contentType);}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){throw new ResourceConflictException("club_logo_storage_error","The logo could not be stored.");}
    }
    public Task<ClubLogoContent?> OpenReadAsync(string key,CancellationToken ct){var path=Resolve(key);return Task.FromResult<ClubLogoContent?>(File.Exists(path)?new(new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,81920,true),Mime(path)):null);}
    public async Task<bool> ContentEqualsAsync(string key,Stream candidate,string contentType,CancellationToken ct)
    {
        var normalized=await NormalizeAsync(candidate,contentType,ct);var path=Resolve(key);if(!File.Exists(path))return false;
        await using var stored=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,81920,true);
        var storedHash=await SHA256.HashDataAsync(stored,ct);return storedHash.AsSpan().SequenceEqual(SHA256.HashData(normalized));
    }
    public Task DeleteAsync(string key,CancellationToken ct){var path=Resolve(key);try{if(File.Exists(path))File.Delete(path);}catch(IOException){}catch(UnauthorizedAccessException){}return Task.CompletedTask;}
    private string Resolve(string key){var path=Path.GetFullPath(key.Replace('/',Path.DirectorySeparatorChar),root);if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new ResourceConflictException("club_logo_storage_error","Invalid logo storage key.");return path;}
    private static string Mime(string p)=>Path.GetExtension(p).ToLowerInvariant() switch{".png"=>"image/png",".jpg" or ".jpeg"=>"image/jpeg",_=>"image/webp"};
    private static async Task<byte[]> NormalizeAsync(Stream content,string contentType,CancellationToken ct)
    {
        if(content is null)throw new RequestValidationException("club_logo_missing_file","A logo file is required.");
        if(contentType is not ("image/png" or "image/jpeg" or "image/webp"))throw new RequestValidationException("club_logo_invalid_type","Only PNG, JPEG and WebP images are accepted.");
        await using var input=new MemoryStream();var buffer=new byte[81920];int read,total=0;
        while((read=await content.ReadAsync(buffer,ct))>0){total+=read;if(total>MaxBytes)throw new RequestValidationException("club_logo_file_too_large","The logo cannot exceed 2 MB.");await input.WriteAsync(buffer.AsMemory(0,read),ct);}
        input.Position=0;Image image;try{image=await Image.LoadAsync(input,ct);}catch(UnknownImageFormatException){throw new RequestValidationException("club_logo_invalid_image","The file is not a decodable image.");}catch(InvalidImageContentException){throw new RequestValidationException("club_logo_invalid_image","The file is not a valid image.");}
        using(image){if(image.Width>2048||image.Height>2048)throw new RequestValidationException("club_logo_dimensions_too_large","Logo dimensions cannot exceed 2048x2048.");var expected=image.Metadata.DecodedImageFormat?.DefaultMimeType;if(!string.Equals(expected,contentType,StringComparison.OrdinalIgnoreCase))throw new RequestValidationException("club_logo_invalid_type","Declared content type does not match the image.");image.Mutate(x=>x.Resize(new ResizeOptions{Mode=ResizeMode.Max,Size=new Size(512,512)}));await using var output=new MemoryStream();if(contentType=="image/png")await image.SaveAsync(output,new PngEncoder(),ct);else if(contentType=="image/jpeg")await image.SaveAsync(output,new JpegEncoder{Quality=85},ct);else await image.SaveAsync(output,new WebpEncoder{Quality=85},ct);return output.ToArray();}
    }
}
