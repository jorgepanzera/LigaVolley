using LigaVolley.Application.Common;
using LigaVolley.Application.Seasons;
using LigaVolley.Domain.Seasons;

namespace LigaVolley.Application.Tests;

public sealed class SeasonServiceTests
{
    [Fact]
    public async Task Create_WhenYearIsAvailable_AddsAndSaves()
    {
        var repository = new FakeSeasonRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new SeasonService(repository, unitOfWork);

        var result = await service.CreateAsync(new(2026, "Season 2026", null, null), default);

        Assert.Equal((short)2026, result.Year);
        Assert.NotNull(repository.Added);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_WhenYearExists_ThrowsConflict()
    {
        var repository = new FakeSeasonRepository();
        repository.Seed(1, new Season(2026, "Existing", null, null));
        var service = new SeasonService(repository, new FakeUnitOfWork());

        var exception = await Assert.ThrowsAsync<ResourceConflictException>(() =>
            service.CreateAsync(new(2026, "Duplicate", null, null), default));

        Assert.Equal("season_year_conflict", exception.Code);
    }

    [Fact]
    public async Task SetActive_ForMissingSeason_ThrowsNotFound()
    {
        var service = new SeasonService(new FakeSeasonRepository(), new FakeUnitOfWork());
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => service.SetActiveAsync(99, false, default));
    }

    [Fact]
    public async Task Update_ExcludesCurrentSeasonFromUniquenessCheck()
    {
        var repository = new FakeSeasonRepository();
        var season = new Season(2026, "Old", null, null);
        repository.Seed(1, season);
        var service = new SeasonService(repository, new FakeUnitOfWork());

        await service.UpdateAsync(1, new(2026, "Updated", null, null), default);

        Assert.Equal("Updated", season.Name);
    }
}
