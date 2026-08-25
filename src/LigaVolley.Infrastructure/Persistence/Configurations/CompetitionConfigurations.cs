using LigaVolley.Domain.Competitions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LigaVolley.Infrastructure.Persistence.Configurations;

internal sealed class CompetitionConfiguration : IEntityTypeConfiguration<Competition>
{
    public void Configure(EntityTypeBuilder<Competition> b)
    {
        b.ToTable("COMPETITION", "dbo", t => { t.HasCheckConstraint("CK_COMPETITION_dates", "[end_date] IS NULL OR [start_date] IS NULL OR [end_date] >= [start_date]"); t.HasCheckConstraint("CK_COMPETITION_status", "[status] IN ('DRAFT','SCHEDULED','IN_PROGRESS','FINISHED','CANCELLED')"); });
        b.HasKey(x => x.CompetitionId); b.Property(x => x.CompetitionId).HasColumnName("competition_id").UseIdentityColumn();
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired(); b.Property(x => x.SeasonId).HasColumnName("season_id"); b.Property(x => x.DivisionId).HasColumnName("division_id"); b.Property(x => x.CompetitionFormatId).HasColumnName("competition_format_id");
        b.Property(x => x.PeriodType).AsSql("period_type", 20); b.Property(x => x.StartDate).HasColumnName("start_date").HasColumnType("date"); b.Property(x => x.EndDate).HasColumnName("end_date").HasColumnType("date"); b.Property(x => x.Status).AsSql("status", 20);
        b.HasOne(x => x.Season).WithMany().HasForeignKey(x => x.SeasonId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Division).WithMany().HasForeignKey(x => x.DivisionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CompetitionFormat).WithMany().HasForeignKey(x => x.CompetitionFormatId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Phases).WithOne().HasForeignKey(x => x.CompetitionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CompetitionPhaseConfiguration : IEntityTypeConfiguration<CompetitionPhase>
{
    public void Configure(EntityTypeBuilder<CompetitionPhase> b)
    {
        b.ToTable("COMPETITION_PHASE", "dbo"); b.HasKey(x => x.CompetitionPhaseId); b.Property(x => x.CompetitionPhaseId).HasColumnName("competition_phase_id").UseIdentityColumn(); b.Property(x => x.CompetitionId).HasColumnName("competition_id"); b.Property(x => x.FormatPhaseId).HasColumnName("format_phase_id");
        b.Property(x => x.Code).HasColumnName("code").HasColumnType("varchar(30)"); b.Property(x => x.Name).HasColumnName("name").HasMaxLength(100); b.Property(x => x.PhaseType).AsSql("phase_type",20); b.Property(x => x.PhaseRole).AsSql("phase_role",20); b.Property(x => x.Sequence).HasColumnName("sequence"); b.Property(x => x.Rounds).HasColumnName("rounds"); b.Property(x => x.FixtureMode).AsNullableSql("fixture_mode",30); b.Property(x => x.Status).AsSql("status",20);
        b.HasAlternateKey(x => new { x.CompetitionPhaseId, x.CompetitionId }).HasName("UQ_COMPETITION_PHASE_id_comp"); b.HasOne(x => x.FormatPhase).WithMany().HasForeignKey(x => x.FormatPhaseId).OnDelete(DeleteBehavior.Restrict); b.HasMany(x => x.Groups).WithOne().HasForeignKey(x => x.CompetitionPhaseId).OnDelete(DeleteBehavior.Restrict); b.HasMany(x => x.Series).WithOne().HasForeignKey(x => x.CompetitionPhaseId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.CompetitionId, x.Code }).IsUnique();
    }
}

internal sealed class CompetitionPhaseGroupConfiguration : IEntityTypeConfiguration<CompetitionPhaseGroup>
{
    public void Configure(EntityTypeBuilder<CompetitionPhaseGroup> b)
    {
        b.ToTable("PHASE_GROUP", "dbo"); b.HasKey(x => x.PhaseGroupId); b.Property(x => x.PhaseGroupId).HasColumnName("phase_group_id").UseIdentityColumn(); b.Property(x => x.CompetitionPhaseId).HasColumnName("competition_phase_id"); b.Property(x => x.FormatGroupId).HasColumnName("format_group_id"); b.Property(x => x.Code).HasColumnName("code").HasColumnType("varchar(30)"); b.Property(x => x.Name).HasColumnName("name").HasMaxLength(100); b.Property(x => x.GroupRole).AsSql("group_role",20); b.Property(x => x.Sequence).HasColumnName("sequence"); b.Property(x => x.Rounds).HasColumnName("rounds"); b.Property(x => x.FixtureMode).AsSql("fixture_mode",30); b.Property(x => x.CarryOverMode).AsSql("carry_over_mode",20); b.HasAlternateKey(x => new { x.PhaseGroupId, x.CompetitionPhaseId }).HasName("UQ_PHASE_GROUP_id_phase"); b.HasOne(x => x.FormatGroup).WithMany().HasForeignKey(x => x.FormatGroupId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(x => new { x.CompetitionPhaseId, x.Code }).IsUnique(); b.HasMany(x => x.Entries).WithOne(x => x.PhaseGroup).HasForeignKey(x => x.PhaseGroupId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PhaseGroupEntryConfiguration : IEntityTypeConfiguration<PhaseGroupEntry>
{
    public void Configure(EntityTypeBuilder<PhaseGroupEntry> b)
    {
        b.ToTable("PHASE_GROUP_ENTRY", "dbo", t => { t.HasCheckConstraint("CK_PHASE_GROUP_ENTRY_source_position", "[source_position] IS NULL OR [source_position] > 0"); t.HasCheckConstraint("CK_PHASE_GROUP_ENTRY_seed", "[seed] IS NULL OR [seed] > 0"); });
        b.HasKey(x => x.PhaseGroupEntryId).HasName("PK_PHASE_GROUP_ENTRY"); b.Property(x => x.PhaseGroupEntryId).HasColumnName("phase_group_entry_id").UseIdentityColumn();
        b.Property(x => x.CompetitionId).HasColumnName("competition_id"); b.Property(x => x.PhaseGroupId).HasColumnName("phase_group_id"); b.Property(x => x.TeamEntryId).HasColumnName("team_entry_id"); b.Property(x => x.SourcePosition).HasColumnName("source_position"); b.Property(x => x.Seed).HasColumnName("seed");
        b.HasOne(x => x.TeamEntry).WithMany().HasForeignKey(x => new { x.TeamEntryId, x.CompetitionId }).HasPrincipalKey(x => new { x.TeamEntryId, x.CompetitionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_PHASE_GROUP_ENTRY_TEAM");
        b.HasIndex(x => new { x.PhaseGroupId, x.TeamEntryId }).IsUnique().HasDatabaseName("UQ_PHASE_GROUP_ENTRY");
    }
}

internal sealed class CompetitionPlayoffSeriesConfiguration : IEntityTypeConfiguration<CompetitionPlayoffSeries>
{
    public void Configure(EntityTypeBuilder<CompetitionPlayoffSeries> b)
    {
        b.ToTable("PLAYOFF_SERIES", "dbo"); b.HasKey(x => x.PlayoffSeriesId); b.Property(x => x.PlayoffSeriesId).HasColumnName("playoff_series_id").UseIdentityColumn(); b.Property(x => x.CompetitionPhaseId).HasColumnName("competition_phase_id"); b.Property(x => x.FormatSeriesId).HasColumnName("format_series_id"); b.Property(x => x.Code).HasColumnName("code").HasColumnType("varchar(30)"); b.Property(x => x.Name).HasColumnName("name").HasMaxLength(100); b.Property(x => x.Sequence).HasColumnName("sequence"); b.Property(x => x.WinsRequired).HasColumnName("wins_required"); b.Property(x => x.Team1InitialWins).HasColumnName("team1_initial_wins"); b.Property(x => x.Team2InitialWins).HasColumnName("team2_initial_wins"); b.Property(x => x.Status).AsSql("status",20); b.HasAlternateKey(x => new { x.PlayoffSeriesId, x.CompetitionPhaseId }).HasName("UQ_PLAYOFF_SERIES_id_phase"); b.HasOne(x => x.FormatSeries).WithMany().HasForeignKey(x => x.FormatSeriesId).OnDelete(DeleteBehavior.Restrict); b.HasMany(x => x.ParticipantSources).WithOne().HasForeignKey(x => x.TargetPlayoffSeriesId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(x => new { x.CompetitionPhaseId, x.Code }).IsUnique();
    }
}

internal sealed class CompetitionSeriesParticipantSourceConfiguration : IEntityTypeConfiguration<CompetitionSeriesParticipantSource>
{
    public void Configure(EntityTypeBuilder<CompetitionSeriesParticipantSource> b)
    {
        b.ToTable("SERIES_PARTICIPANT_SOURCE", "dbo", t => t.HasCheckConstraint("CK_SERIES_PARTICIPANT_SOURCE_side", "[target_side] IN (1,2)")); b.HasKey(x => x.SeriesParticipantSourceId); b.Property(x => x.SeriesParticipantSourceId).HasColumnName("series_participant_source_id").UseIdentityColumn(); b.Property(x => x.TargetPlayoffSeriesId).HasColumnName("target_playoff_series_id"); b.Property(x => x.TargetSide).HasColumnName("target_side"); b.Property(x => x.SourceType).AsSql("source_type",20); b.Property(x => x.SourcePlayoffSeriesId).HasColumnName("source_playoff_series_id"); b.HasOne(x => x.SourceSeries).WithMany().HasForeignKey(x => x.SourcePlayoffSeriesId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(x => new { x.TargetPlayoffSeriesId, x.TargetSide }).IsUnique();
    }
}
