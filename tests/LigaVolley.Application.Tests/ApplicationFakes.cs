using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Seasons;
using LigaVolley.Domain.CompetitionFormats;

namespace LigaVolley.Application.Tests;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }
    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeSeasonRepository : ISeasonRepository
{
    private readonly Dictionary<int, Season> seasons = [];
    public Season? Added { get; private set; }
    public void Seed(int id, Season season) => seasons[id] = season;
    public void Add(Season season) => Added = season;
    public Task<Season?> GetAsync(int id, bool tracking, CancellationToken cancellationToken)
        => Task.FromResult(seasons.GetValueOrDefault(id));
    public Task<IReadOnlyList<Season>> ListAsync(bool? active, short? year, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Season>>(seasons.Values.Where(x => (!active.HasValue || x.Active == active) && (!year.HasValue || x.Year == year)).ToArray());
    public Task<bool> YearExistsAsync(short year, int? excludingId, CancellationToken cancellationToken)
        => Task.FromResult(seasons.Any(x => x.Key != excludingId && x.Value.Year == year));
}

internal sealed class FakeDivisionRepository : IDivisionRepository
{
    private readonly Dictionary<int, Division> divisions = [];
    public Division? Added { get; private set; }
    public void Seed(int id, Division division) => divisions[id] = division;
    public void Add(Division division) => Added = division;
    public Task<Division?> GetAsync(int id, bool tracking, CancellationToken cancellationToken)
        => Task.FromResult(divisions.GetValueOrDefault(id));
    public Task<IReadOnlyList<Division>> ListAsync(Gender? gender, bool? active, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Division>>(divisions.Values.Where(x => (!gender.HasValue || x.Gender == gender) && (!active.HasValue || x.Active == active)).ToArray());
    public Task<bool> NameExistsAsync(string name, Gender gender, int? excludingId, CancellationToken cancellationToken)
        => Task.FromResult(divisions.Any(x => x.Key != excludingId && x.Value.Name == name && x.Value.Gender == gender));
    public Task<bool> LevelExistsAsync(short levelOrder, Gender gender, int? excludingId, CancellationToken cancellationToken)
        => Task.FromResult(divisions.Any(x => x.Key != excludingId && x.Value.LevelOrder == levelOrder && x.Value.Gender == gender));
}

internal sealed class FakeCompetitionFormatRepository : ICompetitionFormatRepository
{
    private readonly Dictionary<int, CompetitionFormat> formats = [];
    public CompetitionFormat? Added { get; private set; }
    public void Seed(int id, CompetitionFormat format) => formats[id] = format;
    public Task<IReadOnlyList<CompetitionFormat>> ListAsync(bool? active, short? teamCount, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<CompetitionFormat>>(formats.Values.Where(x => (!active.HasValue || x.Active == active) && (!teamCount.HasValue || x.MinTeams <= teamCount && teamCount <= x.MaxTeams)).ToArray());
    public Task<CompetitionFormat?> GetAsync(int id, bool tracking, CancellationToken cancellationToken) => Task.FromResult(formats.GetValueOrDefault(id));
    public Task<bool> CodeExistsAsync(string code, int? excludingId, CancellationToken cancellationToken) => Task.FromResult(formats.Any(x => x.Key != excludingId && x.Value.Code == code));
    public void Add(CompetitionFormat format) => Added = format;
    public void PrepareReplacement(CompetitionFormat format) { }
}
