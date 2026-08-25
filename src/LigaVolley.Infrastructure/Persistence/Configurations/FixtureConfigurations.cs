using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.Fixtures;
using LigaVolley.Domain.TeamEntries;
using LigaVolley.Domain.Venues;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LigaVolley.Infrastructure.Persistence.Configurations;

internal sealed class FixtureGenerationConfiguration : IEntityTypeConfiguration<FixtureGeneration>
{
    public void Configure(EntityTypeBuilder<FixtureGeneration> b)
    {
        b.ToTable("FIXTURE_GENERATION","dbo"); b.HasKey(x=>x.FixtureGenerationId).HasName("PK_FIXTURE_GENERATION"); b.Property(x=>x.FixtureGenerationId).HasColumnName("fixture_generation_id").UseIdentityColumn();
        b.Property(x=>x.CompetitionId).HasColumnName("competition_id"); b.Property(x=>x.PhaseId).HasColumnName("phase_id"); b.Property(x=>x.PhaseGroupId).HasColumnName("phase_group_id"); b.Property(x=>x.RandomSeed).HasColumnName("random_seed"); b.Property(x=>x.GeneratedAt).HasColumnName("generated_at").HasColumnType("datetime2(0)");
        b.HasOne(x=>x.Competition).WithMany().HasForeignKey(x=>x.CompetitionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_FIXTURE_GENERATION_COMPETITION");
        b.HasOne(x=>x.Phase).WithMany().HasForeignKey(x=>new{x.PhaseId,x.CompetitionId}).HasPrincipalKey(x=>new{x.CompetitionPhaseId,x.CompetitionId}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_FIXTURE_GENERATION_PHASE");
        b.HasOne(x=>x.PhaseGroup).WithMany().HasForeignKey(x=>new{x.PhaseGroupId,x.PhaseId}).HasPrincipalKey(x=>new{x.PhaseGroupId,x.CompetitionPhaseId}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_FIXTURE_GENERATION_GROUP");
        b.HasIndex(x=>new{x.CompetitionId,x.PhaseId}).IsUnique().HasFilter("[phase_group_id] IS NULL").HasDatabaseName("UQ_FIXTURE_GENERATION_phase_scope");
        b.HasIndex(x=>new{x.CompetitionId,x.PhaseId,x.PhaseGroupId}).IsUnique().HasFilter("[phase_group_id] IS NOT NULL").HasDatabaseName("UQ_FIXTURE_GENERATION_group_scope");
    }
}

internal sealed class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> b)
    {
        b.ToTable("MATCH","dbo",t=>{t.HasCheckConstraint("CK_MATCH_round_number","[round_number] > 0");t.HasCheckConstraint("CK_MATCH_match_number","[match_number] > 0");t.HasCheckConstraint("CK_MATCH_status","[status] IN ('PENDING','SCHEDULED','IN_PROGRESS','FINISHED','SUSPENDED','CANCELLED')");t.HasCheckConstraint("CK_MATCH_different_teams","[home_team_entry_id] IS NULL OR [away_team_entry_id] IS NULL OR [home_team_entry_id] <> [away_team_entry_id]");t.HasCheckConstraint("CK_MATCH_group_or_series","NOT ([phase_group_id] IS NOT NULL AND [series_id] IS NOT NULL)");t.HasCheckConstraint("CK_MATCH_sets","([home_sets] IS NULL AND [away_sets] IS NULL) OR ([home_sets] BETWEEN 0 AND 3 AND [away_sets] BETWEEN 0 AND 3 AND NOT ([home_sets] = 3 AND [away_sets] = 3))");});
        b.HasKey(x=>x.MatchId).HasName("PK_MATCH");b.Property(x=>x.MatchId).HasColumnName("match_id").UseIdentityColumn();b.Property(x=>x.CompetitionId).HasColumnName("competition_id");b.Property(x=>x.PhaseId).HasColumnName("phase_id");b.Property(x=>x.PhaseGroupId).HasColumnName("phase_group_id");b.Property(x=>x.SeriesId).HasColumnName("series_id");b.Property(x=>x.HomeTeamEntryId).HasColumnName("home_team_entry_id");b.Property(x=>x.AwayTeamEntryId).HasColumnName("away_team_entry_id");b.Property(x=>x.MatchDate).HasColumnName("match_date").HasColumnType("datetime2(0)");b.Property(x=>x.VenueId).HasColumnName("venue_id");b.Property(x=>x.RoundNumber).HasColumnName("round_number").HasColumnType("smallint");b.Property(x=>x.MatchNumber).HasColumnName("match_number").HasColumnType("smallint");b.Property(x=>x.Status).AsSql("status",20).HasDefaultValue(MatchStatus.Pending);b.Property(x=>x.HomeSets).HasColumnName("home_sets");b.Property(x=>x.AwaySets).HasColumnName("away_sets");b.Property(x=>x.WinnerTeamEntryId).HasColumnName("winner_team_entry_id");
        b.HasAlternateKey(x=>new{x.MatchId,x.CompetitionId}).HasName("UQ_MATCH_id_comp");
        b.HasOne(x=>x.Competition).WithMany().HasForeignKey(x=>x.CompetitionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x=>x.Phase).WithMany().HasForeignKey(x=>new{x.PhaseId,x.CompetitionId}).HasPrincipalKey(x=>new{x.CompetitionPhaseId,x.CompetitionId}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_MATCH_PHASE");
        b.HasOne(x=>x.PhaseGroup).WithMany().HasForeignKey(x=>new{x.PhaseGroupId,x.PhaseId}).HasPrincipalKey(x=>new{x.PhaseGroupId,x.CompetitionPhaseId}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_MATCH_PHASE_GROUP");
        b.HasOne(x=>x.Series).WithMany().HasForeignKey(x=>new{x.SeriesId,x.PhaseId}).HasPrincipalKey(x=>new{x.PlayoffSeriesId,x.CompetitionPhaseId}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_MATCH_SERIES");
        b.HasOne(x=>x.HomeTeamEntry).WithMany().HasForeignKey(x=>new{x.HomeTeamEntryId,x.CompetitionId}).HasPrincipalKey(x=>new{x.TeamEntryId,x.CompetitionId}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_MATCH_HOME_TEAM");
        b.HasOne(x=>x.AwayTeamEntry).WithMany().HasForeignKey(x=>new{x.AwayTeamEntryId,x.CompetitionId}).HasPrincipalKey(x=>new{x.TeamEntryId,x.CompetitionId}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_MATCH_AWAY_TEAM");
        b.HasOne<TeamEntry>().WithMany().HasForeignKey(x=>new{x.WinnerTeamEntryId,x.CompetitionId}).HasPrincipalKey(x=>new{x.TeamEntryId,x.CompetitionId}).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_MATCH_WINNER");
        b.HasOne(x=>x.Venue).WithMany().HasForeignKey(x=>x.VenueId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_MATCH_VENUE");
        b.HasIndex(x=>new{x.CompetitionId,x.PhaseId,x.MatchNumber}).IsUnique().HasFilter("[phase_group_id] IS NULL AND [series_id] IS NULL").HasDatabaseName("UQ_MATCH_phase_scope_number");
        b.HasIndex(x=>new{x.CompetitionId,x.PhaseId,x.PhaseGroupId,x.MatchNumber}).IsUnique().HasFilter("[phase_group_id] IS NOT NULL").HasDatabaseName("UQ_MATCH_group_scope_number");
        b.HasIndex(x=>new{x.SeriesId,x.MatchNumber}).IsUnique().HasFilter("[series_id] IS NOT NULL").HasDatabaseName("UQ_MATCH_series_number");
        b.HasIndex(x=>new{x.CompetitionId,x.MatchDate}).HasDatabaseName("IX_MATCH_competition_date"); b.HasIndex(x=>new{x.PhaseId,x.RoundNumber}).HasDatabaseName("IX_MATCH_phase_round");
        b.HasMany(x=>x.Sets).WithOne().HasForeignKey(x=>x.MatchId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_MATCH_SET_MATCH");
    }
}

internal sealed class MatchSetConfiguration : IEntityTypeConfiguration<MatchSet>
{
    public void Configure(EntityTypeBuilder<MatchSet> b)
    {
        b.ToTable("MATCH_SET", "dbo", t => { t.HasCheckConstraint("CK_MATCH_SET_number", "[set_number] BETWEEN 1 AND 5"); t.HasCheckConstraint("CK_MATCH_SET_points", "[home_points] >= 0 AND [away_points] >= 0"); });
        b.HasKey(x => x.MatchSetId).HasName("PK_MATCH_SET"); b.Property(x => x.MatchSetId).HasColumnName("match_set_id").UseIdentityColumn();
        b.Property(x => x.MatchId).HasColumnName("match_id"); b.Property(x => x.SetNumber).HasColumnName("set_number"); b.Property(x => x.HomePoints).HasColumnName("home_points"); b.Property(x => x.AwayPoints).HasColumnName("away_points");
        b.HasIndex(x => new { x.MatchId, x.SetNumber }).IsUnique().HasDatabaseName("UQ_MATCH_SET");
    }
}
