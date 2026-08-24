using LigaVolley.Application.Common;
using LigaVolley.Application.Divisions;
using LigaVolley.Domain.Divisions;

namespace LigaVolley.Application.Tests;

public sealed class DivisionServiceTests
{
    [Fact]
    public async Task Create_WhenValuesAreAvailable_AddsAndSaves()
    {
        var repository = new FakeDivisionRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new DivisionService(repository, unitOfWork);

        var result = await service.CreateAsync(new("B Femenina", 2, Gender.Female), default);

        Assert.Equal(Gender.Female, result.Gender);
        Assert.NotNull(repository.Added);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_WhenNameAndGenderExist_ThrowsConflict()
    {
        var repository = new FakeDivisionRepository();
        repository.Seed(1, new Division("B", 2, Gender.Female));
        var service = new DivisionService(repository, new FakeUnitOfWork());

        var exception = await Assert.ThrowsAsync<ResourceConflictException>(() =>
            service.CreateAsync(new("B", 3, Gender.Female), default));

        Assert.Equal("division_name_gender_conflict", exception.Code);
    }

    [Fact]
    public async Task Create_WhenLevelAndGenderExist_ThrowsConflict()
    {
        var repository = new FakeDivisionRepository();
        repository.Seed(1, new Division("B", 2, Gender.Female));
        var service = new DivisionService(repository, new FakeUnitOfWork());

        var exception = await Assert.ThrowsAsync<ResourceConflictException>(() =>
            service.CreateAsync(new("C", 2, Gender.Female), default));

        Assert.Equal("division_level_gender_conflict", exception.Code);
    }

    [Fact]
    public async Task SetActive_ChangesDivisionAndSaves()
    {
        var repository = new FakeDivisionRepository();
        var division = new Division("B", 2, Gender.Female);
        repository.Seed(1, division);
        var unitOfWork = new FakeUnitOfWork();
        var service = new DivisionService(repository, unitOfWork);

        await service.SetActiveAsync(1, false, default);

        Assert.False(division.Active);
        Assert.Equal(1, unitOfWork.SaveCount);
    }
}
