using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Seasons;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Clubs;
using LigaVolley.Domain.Teams;
using LigaVolley.Domain.Venues;
using LigaVolley.Domain.TeamEntries;
using LigaVolley.Domain.Fixtures;

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

internal sealed class FakeFixtureRepository : IFixtureRepository
{
    public bool GenerationExists {get;set;} public List<FixtureGeneration> Generations {get;}=[]; public List<Match> Matches {get;}=[];
    public Task<bool> GenerationExistsAsync(int competitionId,int phaseId,int? phaseGroupId,CancellationToken ct)=>Task.FromResult(GenerationExists);
    public Task<IReadOnlyList<FixtureGeneration>> ListGenerationsAsync(int competitionId,CancellationToken ct)=>Task.FromResult<IReadOnlyList<FixtureGeneration>>(Generations);
    public Task<IReadOnlyList<Match>> ListMatchesAsync(int competitionId,CancellationToken ct)=>Task.FromResult<IReadOnlyList<Match>>(Matches);
    public void AddGeneration(FixtureGeneration generation)=>Generations.Add(generation);
    public void AddMatches(IEnumerable<Match> matches)=>Matches.AddRange(matches);
}

internal sealed class FailingUnitOfWork : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken=default)=>throw new InvalidOperationException("Simulated persistence failure.");
}

internal sealed class FakeTeamEntryRepository : ITeamEntryRepository
{
    private readonly Dictionary<(int CompetitionId,int EntryId),TeamEntry> values=[];
    public TeamEntry? Added {get;private set;} public bool TeamExists {get;set;} public int ValidCount {get;set;} public bool Removed {get;private set;}
    public void Seed(int competitionId,int entryId,TeamEntry value)=>values[(competitionId,entryId)]=value;
    public Task<IReadOnlyList<TeamEntry>> ListAsync(int competitionId,bool tracking,CancellationToken ct)=>Task.FromResult<IReadOnlyList<TeamEntry>>(values.Where(x=>x.Key.CompetitionId==competitionId).Select(x=>x.Value).ToArray());
    public Task<TeamEntry?> GetAsync(int competitionId,int entryId,bool tracking,CancellationToken ct)=>Task.FromResult(values.GetValueOrDefault((competitionId,entryId)));
    public Task<bool> TeamExistsAsync(int competitionId,int teamId,CancellationToken ct)=>Task.FromResult(TeamExists);
    public Task<int> CountValidAsync(int competitionId,CancellationToken ct)=>Task.FromResult(ValidCount);
    public void Add(TeamEntry entry)=>Added=entry;
    public void Remove(TeamEntry entry)=>Removed=true;
}

internal sealed class FakeClubRepository : IClubRepository
{
    private readonly Dictionary<int, Club> values=[]; public Club? Added {get;private set;} public void Seed(int id,Club value)=>values[id]=value; public void Add(Club value)=>Added=value;
    public Task<Club?> GetAsync(int id,bool tracking,CancellationToken ct)=>Task.FromResult(values.GetValueOrDefault(id));
    public Task<IReadOnlyList<Club>> ListAsync(bool? active,CancellationToken ct)=>Task.FromResult<IReadOnlyList<Club>>(values.Values.Where(x=>!active.HasValue||x.Active==active).ToArray());
    public Task<bool> NameExistsAsync(string name,int? excludingId,CancellationToken ct)=>Task.FromResult(values.Any(x=>x.Key!=excludingId&&x.Value.Name==name));
}
internal sealed class FakeTeamRepository : ITeamRepository
{
    private readonly Dictionary<int, Team> values=[]; public Team? Added {get;private set;} public void Seed(int id,Team value)=>values[id]=value; public void Add(Team value)=>Added=value;
    public Task<Team?> GetAsync(int id,bool tracking,CancellationToken ct)=>Task.FromResult(values.GetValueOrDefault(id));
    public Task<IReadOnlyList<Team>> ListAsync(int? clubId,Gender? gender,bool? active,CancellationToken ct)=>Task.FromResult<IReadOnlyList<Team>>(values.Values.Where(x=>(!clubId.HasValue||x.ClubId==clubId)&&(!gender.HasValue||x.Gender==gender)&&(!active.HasValue||x.Active==active)).ToArray());
    public Task<bool> NameGenderExistsAsync(string name,Gender gender,int? excludingId,CancellationToken ct)=>Task.FromResult(values.Any(x=>x.Key!=excludingId&&x.Value.Name==name&&x.Value.Gender==gender));
}
internal sealed class FakeVenueRepository : IVenueRepository
{
    private readonly Dictionary<int, Venue> values=[]; public Venue? Added {get;private set;} public void Seed(int id,Venue value)=>values[id]=value; public void Add(Venue value)=>Added=value;
    public Task<Venue?> GetAsync(int id,bool tracking,CancellationToken ct)=>Task.FromResult(values.GetValueOrDefault(id));
    public Task<IReadOnlyList<Venue>> ListAsync(bool? active,CancellationToken ct)=>Task.FromResult<IReadOnlyList<Venue>>(values.Values.Where(x=>!active.HasValue||x.Active==active).ToArray());
    public Task<bool> NameExistsAsync(string name,int? excludingId,CancellationToken ct)=>Task.FromResult(values.Any(x=>x.Key!=excludingId&&x.Value.Name==name));
}

internal sealed class FakeCompetitionRepository : ICompetitionRepository
{
    private readonly Dictionary<int, Competition> competitions = [];
    public Competition? Added { get; private set; }
    public void Seed(int id, Competition competition) => competitions[id] = competition;
    public void Add(Competition competition) => Added = competition;
    public Task<Competition?> GetAsync(int id, bool tracking, CancellationToken ct) => Task.FromResult(competitions.GetValueOrDefault(id));
    public Task<IReadOnlyList<Competition>> ListAsync(int? seasonId, int? divisionId, CompetitionStatus? status, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Competition>>(competitions.Values.Where(x => (!seasonId.HasValue || x.SeasonId == seasonId) && (!divisionId.HasValue || x.DivisionId == divisionId) && (!status.HasValue || x.Status == status)).ToArray());
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
