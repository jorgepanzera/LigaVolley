using LigaVolley.Application.Abstractions.Persistence;
using LigaVolley.Application.Common;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Seasons;
using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Clubs;
using LigaVolley.Domain.Teams;
using LigaVolley.Domain.Venues;
using LigaVolley.Domain.TeamEntries;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.People;
using LigaVolley.Domain.CompetitionRosters;
using LigaVolley.Domain.MatchOfficials;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LigaVolley.Infrastructure.Persistence;

public sealed class LigaVolleyDbContext(DbContextOptions<LigaVolleyDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Division> Divisions => Set<Division>();
    public DbSet<CompetitionFormat> CompetitionFormats => Set<CompetitionFormat>();
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<TeamEntry> TeamEntries => Set<TeamEntry>();
    public DbSet<FixtureGeneration> FixtureGenerations => Set<FixtureGeneration>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchSet> MatchSets => Set<MatchSet>();
    public DbSet<PhaseGroupEntry> PhaseGroupEntries => Set<PhaseGroupEntry>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<PersonAdditionalDocument> PersonAdditionalDocuments => Set<PersonAdditionalDocument>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Coach> Coaches => Set<Coach>();
    public DbSet<Referee> Referees => Set<Referee>();
    public DbSet<CompetitionRoster> CompetitionRosters => Set<CompetitionRoster>();
    public DbSet<MatchOfficial> MatchOfficials => Set<MatchOfficial>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(LigaVolleyDbContext).Assembly);

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            var message = exception.InnerException?.Message ?? exception.Message;
            var (code, detail) = message switch
            {
                var x when x.Contains("UX_PERSON_document", StringComparison.OrdinalIgnoreCase) => ("person_document_already_exists", "A person with this document already exists."),
                var x when x.Contains("UQ_PLAYER_person", StringComparison.OrdinalIgnoreCase) => ("player_profile_already_exists", "Player profile already exists."),
                var x when x.Contains("UQ_COACH_person", StringComparison.OrdinalIgnoreCase) => ("coach_profile_already_exists", "Coach profile already exists."),
                var x when x.Contains("UQ_REFEREE_person", StringComparison.OrdinalIgnoreCase) => ("referee_profile_already_exists", "Referee profile already exists."),
                var x when x.Contains("UQ_COMPETITION_ROSTER_team_entry", StringComparison.OrdinalIgnoreCase) => ("competition_roster_already_exists", "A roster already exists for this TeamEntry."),
                var x when x.Contains("UQ_COMPETITION_ROSTER_PLAYER_player", StringComparison.OrdinalIgnoreCase) => ("competition_roster_player_already_exists", "Player already belongs to this roster."),
                var x when x.Contains("UQ_COMPETITION_ROSTER_STAFF_coach", StringComparison.OrdinalIgnoreCase) => ("competition_roster_staff_already_exists", "Coach already belongs to this roster."),
                var x when x.Contains("UX_COMPETITION_ROSTER_PLAYER_active_jersey", StringComparison.OrdinalIgnoreCase) => ("competition_roster_duplicate_jersey_number", "Jersey number must be unique among active players."),
                var x when x.Contains("UQ_MATCH_OFFICIAL_role",StringComparison.OrdinalIgnoreCase)=>("match_official_role_already_assigned","Role is already assigned."),
                var x when x.Contains("UQ_MATCH_OFFICIAL_referee",StringComparison.OrdinalIgnoreCase)=>("match_official_referee_already_assigned","Referee is already assigned."),
                _ => ("unique_constraint_conflict", "The operation conflicts with an existing resource.")
            };
            throw new ResourceConflictException(
                code,
                detail);
        }
    }

    async Task<IApplicationTransaction> IUnitOfWork.BeginSerializableTransactionAsync(CancellationToken cancellationToken)
        => new EfApplicationTransaction(await Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,cancellationToken));

    private sealed class EfApplicationTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction):IApplicationTransaction
    { public Task CommitAsync(CancellationToken cancellationToken=default)=>transaction.CommitAsync(cancellationToken); public ValueTask DisposeAsync()=>transaction.DisposeAsync(); }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        => exception.InnerException is SqlException { Number: 2601 or 2627 };
}
