using System.Security.Cryptography;
using LigaVolley.Application.Abstractions.Storage;
using LigaVolley.Application.Clubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace LigaVolley.Infrastructure.Persistence.Seed;

public sealed class Livosur2026ClubLogoSeedOptions
{
    public string Path { get; set; } = "seed-assets/club-logos";
}

public sealed record Livosur2026ClubLogoSeedResult(
    int ManifestRows,
    int ClubsFound,
    int Applied,
    int Replaced,
    int AlreadyCurrent,
    int SkippedClubNotFound,
    int Errors);

public sealed class Livosur2026ClubLogoSeeder(
    LigaVolleyDbContext db,
    ClubService clubs,
    IClubLogoStorage storage,
    IOptions<Livosur2026ClubLogoSeedOptions> options,
    ILogger<Livosur2026ClubLogoSeeder> logger)
{
    public async Task<Livosur2026ClubLogoSeedResult> SeedAsync(CancellationToken ct = default)
    {
        var packageRoot = Path.GetFullPath(options.Value.Path);
        var manifestPath = Path.Combine(packageRoot, "manifest.csv");
        if (!File.Exists(manifestPath))
            throw new InvalidOperationException($"Club logo manifest was not found at '{manifestPath}'.");

        var rows = await LoadManifestAsync(manifestPath, ct);
        var duplicateNames = rows.GroupBy(x => x.ClubName, StringComparer.Ordinal)
            .Where(x => x.Count() > 1).Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        var found = 0; var applied = 0; var replaced = 0; var current = 0; var skipped = 0; var errors = 0;

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            if (duplicateNames.Contains(row.ClubName))
            {
                errors++;
                logger.LogError("Club logo seed error. Club={Club}; Status=DuplicateClubInManifest", row.ClubName);
                continue;
            }

            var club = await db.Clubs.AsNoTracking().SingleOrDefaultAsync(x => x.Name == row.ClubName, ct);
            if (club is null)
            {
                skipped++;
                logger.LogWarning("Club logo seed skipped. Club={Club}; Status=SkippedClubNotFound", row.ClubName);
                continue;
            }
            found++;

            try
            {
                var assetPath = ResolveAsset(packageRoot, row.FileName);
                if (!File.Exists(assetPath)) throw new InvalidDataException("Asset file was not found.");
                await VerifySha256Async(assetPath, row.Sha256, ct);
                var contentType = await DetectContentTypeAsync(assetPath, ct);
                var extensionContentType = ContentTypeFromExtension(assetPath);
                if (!string.Equals(extensionContentType, contentType, StringComparison.OrdinalIgnoreCase))
                    logger.LogWarning(
                        "Club logo seed asset extension does not match its image format. Club={Club}; File={File}; ExtensionContentType={ExtensionContentType}; ActualContentType={ActualContentType}",
                        row.ClubName, row.FileName, extensionContentType ?? "unsupported", contentType);

                if (club.LogoStorageKey is not null)
                {
                    await using var candidate = File.OpenRead(assetPath);
                    if (await storage.ContentEqualsAsync(club.LogoStorageKey, candidate, contentType, ct))
                    {
                        current++;
                        logger.LogInformation("Club logo seed unchanged. Club={Club}; Status=AlreadyCurrent", row.ClubName);
                        continue;
                    }
                }

                await using var content = File.OpenRead(assetPath);
                await clubs.ReplaceLogoAsync(club.ClubId, content, contentType, ct);
                if (club.LogoStorageKey is null) applied++; else replaced++;
                logger.LogInformation("Club logo seed updated. Club={Club}; Status={Status}", row.ClubName, club.LogoStorageKey is null ? "Applied" : "Replaced");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors++;
                logger.LogError(ex, "Club logo seed error. Club={Club}; Status=Error", row.ClubName);
            }
        }

        return new(rows.Count, found, applied, replaced, current, skipped, errors);
    }

    private static async Task<List<ManifestRow>> LoadManifestAsync(string path, CancellationToken ct)
    {
        var lines = await File.ReadAllLinesAsync(path, ct);
        if (lines.Length == 0 || lines[0].TrimStart('\uFEFF') != "club_name,file_name,selection,source_status,sha256")
            throw new InvalidDataException("Club logo manifest header is invalid.");
        var rows = new List<ManifestRow>(Math.Max(0, lines.Length - 1));
        for (var index = 1; index < lines.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index])) continue;
            var fields = lines[index].Split(',');
            if (fields.Length != 5 || fields.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException($"Club logo manifest row {index + 1} is invalid.");
            if (fields[4].Length != 64 || !fields[4].All(Uri.IsHexDigit))
                throw new InvalidDataException($"Club logo manifest SHA-256 at row {index + 1} is invalid.");
            rows.Add(new(fields[0].Trim(), fields[1].Trim(), fields[4].Trim().ToLowerInvariant()));
        }
        return rows;
    }

    private static string ResolveAsset(string packageRoot, string relativePath)
    {
        if (Path.IsPathRooted(relativePath)) throw new InvalidDataException("Asset path must be relative.");
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(normalized, packageRoot);
        if (!fullPath.StartsWith(packageRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Asset path escapes the logo seed package.");
        return fullPath;
    }

    private static async Task VerifySha256Async(string path, string expected, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidDataException($"Asset SHA-256 mismatch. Expected {expected}, got {actual}.");
    }

    private static async Task<string> DetectContentTypeAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        IImageFormat? format;
        try
        {
            format = await Image.DetectFormatAsync(stream, ct);
        }
        catch (UnknownImageFormatException ex)
        {
            throw new InvalidDataException("Asset file is not a supported image.", ex);
        }

        return format switch
        {
            PngFormat => "image/png",
            JpegFormat => "image/jpeg",
            WebpFormat => "image/webp",
            _ => throw new InvalidDataException("Asset image format is not supported.")
        };
    }

    private static string? ContentTypeFromExtension(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        _ => null
    };

    private sealed record ManifestRow(string ClubName, string FileName, string Sha256);
}
