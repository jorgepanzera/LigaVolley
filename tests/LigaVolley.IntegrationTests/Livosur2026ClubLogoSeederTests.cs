using System.Security.Cryptography;
using LigaVolley.Application.Abstractions.Storage;
using LigaVolley.Application.Clubs;
using LigaVolley.Domain.Clubs;
using LigaVolley.Infrastructure.Persistence;
using LigaVolley.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LigaVolley.IntegrationTests;

public sealed class Livosur2026ClubLogoSeederTests(LigaVolleyApiFactory factory) : IClassFixture<LigaVolleyApiFactory>
{
    [Fact]
    public async Task ExistingClubWithoutLogo_IsApplied_AndSecondRunIsBinaryIdempotent()
    {
        var clubName = $"Logo seed {Guid.NewGuid():N}";
        var package = CreatePackage((clubName, SourceAsset("ACJ.jpg"), null));
        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
            var club = new Club(clubName, null); db.Clubs.Add(club); await db.SaveChangesAsync();
            var seeder = CreateSeeder(scope, package);

            var first = await seeder.SeedAsync();
            db.ChangeTracker.Clear(); var afterFirst = await db.Clubs.SingleAsync(x => x.ClubId == club.ClubId);
            var firstKey = afterFirst.LogoStorageKey; var firstVersion = afterFirst.LogoVersion;
            var firstHash = await StoredHash(scope, firstKey!);
            var second = await seeder.SeedAsync();
            db.ChangeTracker.Clear(); var afterSecond = await db.Clubs.SingleAsync(x => x.ClubId == club.ClubId);

            Assert.Equal(1, first.Applied); Assert.Equal(1, second.AlreadyCurrent);
            Assert.Equal(firstVersion, afterSecond.LogoVersion); Assert.Equal(firstKey, afterSecond.LogoStorageKey);
            Assert.Equal(firstHash, await StoredHash(scope, afterSecond.LogoStorageKey!));
        }
        finally { Directory.Delete(package, true); }
    }

    [Fact]
    public async Task ExistingDifferentLogo_IsReplaced_AndOldAssetIsCleaned()
    {
        var clubName = $"Logo replace {Guid.NewGuid():N}";
        var package = CreatePackage((clubName, SourceAsset("ACJ_PORTONES.jpg"), null));
        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
            var club = new Club(clubName, null); db.Clubs.Add(club); await db.SaveChangesAsync();
            var service = scope.ServiceProvider.GetRequiredService<ClubService>();
            await using (var original = File.OpenRead(SourceAsset("ACJ.jpg"))) await service.ReplaceLogoAsync(club.ClubId, original, "image/jpeg", default);
            db.ChangeTracker.Clear(); var oldKey = (await db.Clubs.SingleAsync(x => x.ClubId == club.ClubId)).LogoStorageKey!;

            var result = await CreateSeeder(scope, package).SeedAsync();

            Assert.Equal(1, result.Replaced);
            Assert.Null(await scope.ServiceProvider.GetRequiredService<IClubLogoStorage>().OpenReadAsync(oldKey, default));
        }
        finally { Directory.Delete(package, true); }
    }

    [Fact]
    public async Task MissingClub_IsSkipped_AndMixedExistingClubsContinue()
    {
        var firstName = $"Logo mixed A {Guid.NewGuid():N}"; var secondName = $"Logo mixed B {Guid.NewGuid():N}";
        var package = CreatePackage(
            (firstName, SourceAsset("ACJ.jpg"), null),
            (secondName, SourceAsset("ATENAS.jpg"), null),
            ($"Missing {Guid.NewGuid():N}", SourceAsset("BAGE.jpg"), null));
        try
        {
            await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
            db.Clubs.AddRange(new Club(firstName, null), new Club(secondName, null)); await db.SaveChangesAsync();
            var before = await db.Clubs.CountAsync(); var result = await CreateSeeder(scope, package).SeedAsync();
            Assert.Equal(2, result.Applied); Assert.Equal(1, result.SkippedClubNotFound); Assert.Equal(before, await db.Clubs.CountAsync());
        }
        finally { Directory.Delete(package, true); }
    }

    [Fact]
    public async Task InvalidSha_DoesNotModifyClub_AndReportsError()
    {
        var clubName = $"Logo bad hash {Guid.NewGuid():N}";
        var package = CreatePackage((clubName, SourceAsset("ACJ.jpg"), new string('0', 64)));
        try
        {
            await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
            var club = new Club(clubName, null); db.Clubs.Add(club); await db.SaveChangesAsync();
            var result = await CreateSeeder(scope, package).SeedAsync(); db.ChangeTracker.Clear(); var unchanged = await db.Clubs.SingleAsync(x => x.ClubId == club.ClubId);
            Assert.Equal(1, result.Errors); Assert.Null(unchanged.LogoStorageKey); Assert.Equal(0, unchanged.LogoVersion);
        }
        finally { Directory.Delete(package, true); }
    }

    [Fact]
    public async Task MisnamedImage_UsesDetectedContentType_AndLogsWarning()
    {
        var clubName = $"Logo misnamed {Guid.NewGuid():N}";
        var package = CreatePackage((clubName, SourceAsset("ACJ.jpg"), null, ".png"));
        var logger = new CapturingLogger<Livosur2026ClubLogoSeeder>();
        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>();
            var club = new Club(clubName, null); db.Clubs.Add(club); await db.SaveChangesAsync();

            var result = await CreateSeeder(scope, package, logger).SeedAsync();
            db.ChangeTracker.Clear();
            var updated = await db.Clubs.SingleAsync(x => x.ClubId == club.ClubId);

            Assert.Equal(1, result.Applied); Assert.Equal(0, result.Errors);
            Assert.Equal("image/jpeg", updated.LogoContentType);
            Assert.Contains(logger.Entries, x => x.Level == LogLevel.Warning && x.Message.Contains("extension does not match", StringComparison.Ordinal));
        }
        finally { Directory.Delete(package, true); }
    }

    [Fact]
    public async Task ApprovedDataset_Processes98Clubs_AndRerunIsFullyIdempotent()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<Livosur2026Seeder>().SeedAsync();
        var seeder = scope.ServiceProvider.GetRequiredService<Livosur2026ClubLogoSeeder>();
        var first = await seeder.SeedAsync(); var second = await seeder.SeedAsync();
        Assert.Equal(98, first.ManifestRows); Assert.Equal(98, first.ClubsFound); Assert.Equal(0, first.Errors);
        Assert.Equal(98, first.Applied + first.Replaced + first.AlreadyCurrent);
        Assert.Equal(98, second.AlreadyCurrent); Assert.Equal(0, second.Applied); Assert.Equal(0, second.Replaced); Assert.Equal(0, second.Errors);
    }

    private static Livosur2026ClubLogoSeeder CreateSeeder(
        AsyncServiceScope scope,
        string package,
        ILogger<Livosur2026ClubLogoSeeder>? logger = null) => new(
        scope.ServiceProvider.GetRequiredService<LigaVolleyDbContext>(),
        scope.ServiceProvider.GetRequiredService<ClubService>(),
        scope.ServiceProvider.GetRequiredService<IClubLogoStorage>(),
        Options.Create(new Livosur2026ClubLogoSeedOptions { Path = package }),
        logger ?? NullLogger<Livosur2026ClubLogoSeeder>.Instance);

    private static string CreatePackage(params (string ClubName, string Asset, string? Hash, string Extension)[] rows)
    {
        var root = Path.Combine(Path.GetTempPath(), $"ligavolley-logo-seed-{Guid.NewGuid():N}"); var images = Path.Combine(root, "images"); Directory.CreateDirectory(images);
        var manifest = new List<string> { "club_name,file_name,selection,source_status,sha256" };
        for (var index = 0; index < rows.Length; index++)
        {
            var name = $"asset-{index}{rows[index].Extension}"; var target = Path.Combine(images, name); File.Copy(rows[index].Asset, target);
            var hash = rows[index].Hash ?? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(target))).ToLowerInvariant();
            manifest.Add($"{rows[index].ClubName},images/{name},AUTOMATIC,SINGLE,{hash}");
        }
        File.WriteAllLines(Path.Combine(root, "manifest.csv"), manifest); return root;
    }

    private static string CreatePackage(params (string ClubName, string Asset, string? Hash)[] rows) =>
        CreatePackage(rows.Select(x => (x.ClubName, x.Asset, x.Hash, ".jpg")).ToArray());

    private static string SourceAsset(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "seed-assets"))) directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found."), "seed-assets", "club-logos", "images", name);
    }

    private static async Task<string> StoredHash(AsyncServiceScope scope, string key)
    {
        await using var asset = await scope.ServiceProvider.GetRequiredService<IClubLogoStorage>().OpenReadAsync(key, default) ?? throw new InvalidOperationException();
        return Convert.ToHexString(await SHA256.HashDataAsync(asset.Content)).ToLowerInvariant();
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
