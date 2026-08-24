using LigaVolley.Domain.CompetitionFormats;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LigaVolley.Infrastructure.Persistence.Configurations;

internal static class FormatEnum
{
    public static string ToSql<T>(T value) where T : Enum => value.ToString() switch
    {
        "RoundRobin" => "ROUND_ROBIN", "GroupStage" => "GROUP_STAGE", "Playoff" => "PLAYOFF",
        "ThirdPlace" => "THIRD_PLACE", "MirroredHomeAway" => "MIRRORED_HOME_AWAY", "BalancedRandom" => "BALANCED_RANDOM",
        "QualifiedOnly" => "QUALIFIED_ONLY", "PositionRange" => "POSITION_RANGE", "TopHalf" => "TOP_HALF", "BottomHalf" => "BOTTOM_HALF",
        "SeriesWinner" => "SERIES_WINNER", "SeriesLoser" => "SERIES_LOSER", "TablePoints" => "TABLE_POINTS", "MatchWins" => "MATCH_WINS",
        "SetRatio" => "SET_RATIO", "PointRatio" => "POINT_RATIO", "HeadToHead" => "HEAD_TO_HEAD", "PhasePosition" => "PHASE_POSITION",
        "GroupPosition" => "GROUP_POSITION", "SeriesResult" => "SERIES_RESULT", "PhaseLastN" => "PHASE_LAST_N", "GroupLastN" => "GROUP_LAST_N",
        _ => value.ToString().ToUpperInvariant()
    };
    public static T FromSql<T>(string value) where T : struct, Enum => Enum.Parse<T>(string.Concat(value.ToLowerInvariant().Split('_').Select(x => char.ToUpperInvariant(x[0]) + x[1..])));
    public static PropertyBuilder<T> AsSql<T>(this PropertyBuilder<T> property, string column, int length) where T : struct, Enum
        => property.HasColumnName(column).HasColumnType($"varchar({length})").HasConversion(v => ToSql(v), v => FromSql<T>(v));
    public static PropertyBuilder<T?> AsNullableSql<T>(this PropertyBuilder<T?> property, string column, int length) where T : struct, Enum
        => property.HasColumnName(column).HasColumnType($"varchar({length})").HasConversion(v => v.HasValue ? ToSql(v.Value) : null, v => v == null ? null : FromSql<T>(v));
}

internal sealed class CompetitionFormatConfiguration : IEntityTypeConfiguration<CompetitionFormat>
{
    public void Configure(EntityTypeBuilder<CompetitionFormat> b)
    {
        b.ToTable("COMPETITION_FORMAT", "dbo", t => t.HasCheckConstraint("CK_COMPETITION_FORMAT_team_range", "[min_teams] > 1 AND [max_teams] >= [min_teams]"));
        b.HasKey(x => x.CompetitionFormatId).HasName("PK_COMPETITION_FORMAT"); b.Property(x => x.CompetitionFormatId).HasColumnName("competition_format_id").UseIdentityColumn();
        b.Property(x => x.Code).HasColumnName("code").HasColumnType("varchar(30)").IsRequired(); b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_COMPETITION_FORMAT_code");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired(); b.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
        b.Property(x => x.MinTeams).HasColumnName("min_teams").HasColumnType("smallint"); b.Property(x => x.MaxTeams).HasColumnName("max_teams").HasColumnType("smallint"); b.Property(x => x.Active).HasColumnName("active").HasDefaultValue(true);
        b.HasMany(x => x.Phases).WithOne().HasForeignKey(x => x.CompetitionFormatId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.QualificationRules).WithOne().HasForeignKey(x => x.CompetitionFormatId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.ScoringRules).WithOne().HasForeignKey(x => x.CompetitionFormatId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.TiebreakRules).WithOne().HasForeignKey(x => x.CompetitionFormatId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.MovementRules).WithOne().HasForeignKey(x => x.CompetitionFormatId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FormatPhaseConfiguration : IEntityTypeConfiguration<FormatPhase>
{
    public void Configure(EntityTypeBuilder<FormatPhase> b)
    {
        b.ToTable("FORMAT_PHASE", "dbo", t => { t.HasCheckConstraint("CK_FORMAT_PHASE_type", "[phase_type] IN ('ROUND_ROBIN','GROUP_STAGE','PLAYOFF')"); t.HasCheckConstraint("CK_FORMAT_PHASE_role", "[phase_role] IN ('REGULAR','CHAMPIONSHIP','RELEGATION','SEMIFINAL','THIRD_PLACE','FINAL')"); t.HasCheckConstraint("CK_FORMAT_PHASE_sequence", "[sequence] > 0"); t.HasCheckConstraint("CK_FORMAT_PHASE_rounds", "[rounds] IS NULL OR [rounds] > 0"); t.HasCheckConstraint("CK_FORMAT_PHASE_fixture_mode", "[fixture_mode] IS NULL OR [fixture_mode] IN ('MIRRORED_HOME_AWAY','BALANCED_RANDOM','PLAYOFF')"); t.HasCheckConstraint("CK_FORMAT_PHASE_round_robin", "[phase_type] <> 'ROUND_ROBIN' OR ([rounds] IS NOT NULL AND [fixture_mode] IS NOT NULL)"); }); b.HasKey(x => x.FormatPhaseId); b.Property(x => x.FormatPhaseId).HasColumnName("format_phase_id").UseIdentityColumn(); b.Property(x => x.CompetitionFormatId).HasColumnName("competition_format_id");
        b.HasAlternateKey(x => new { x.FormatPhaseId, x.CompetitionFormatId }).HasName("UQ_FORMAT_PHASE_id_format"); b.HasIndex(x => new { x.CompetitionFormatId, x.Code }).IsUnique().HasDatabaseName("UQ_FORMAT_PHASE_code");
        b.Property(x => x.Code).HasColumnName("code").HasColumnType("varchar(30)"); b.Property(x => x.Name).HasColumnName("name").HasMaxLength(100); b.Property(x => x.PhaseType).AsSql("phase_type",20); b.Property(x => x.PhaseRole).AsSql("phase_role",20); b.Property(x => x.Sequence).HasColumnName("sequence").HasColumnType("smallint"); b.Property(x => x.Rounds).HasColumnName("rounds").HasColumnType("smallint"); b.Property(x => x.FixtureMode).AsNullableSql("fixture_mode",30); b.Property("Active").HasColumnName("active").HasDefaultValue(true);
        b.HasMany(x => x.Groups).WithOne().HasForeignKey(x => new { x.FormatPhaseId, x.CompetitionFormatId }).HasPrincipalKey(x => new { x.FormatPhaseId, x.CompetitionFormatId }).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Series).WithOne().HasForeignKey(x => new { x.FormatPhaseId, x.CompetitionFormatId }).HasPrincipalKey(x => new { x.FormatPhaseId, x.CompetitionFormatId }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FormatGroupConfiguration : IEntityTypeConfiguration<FormatGroup>
{
    public void Configure(EntityTypeBuilder<FormatGroup> b)
    {
        b.ToTable("FORMAT_GROUP", "dbo", t => { t.HasCheckConstraint("CK_FORMAT_GROUP_role", "[group_role] IN ('CHAMPIONSHIP','RELEGATION','OTHER')"); t.HasCheckConstraint("CK_FORMAT_GROUP_sequence", "[sequence] > 0"); t.HasCheckConstraint("CK_FORMAT_GROUP_rounds", "[rounds] > 0"); t.HasCheckConstraint("CK_FORMAT_GROUP_fixture_mode", "[fixture_mode] IN ('MIRRORED_HOME_AWAY','BALANCED_RANDOM')"); t.HasCheckConstraint("CK_FORMAT_GROUP_carry_over", "[carry_over_mode] IN ('NONE','ALL','QUALIFIED_ONLY')"); }); b.HasKey(x => x.FormatGroupId); b.Property(x => x.FormatGroupId).HasColumnName("format_group_id").UseIdentityColumn(); b.Property(x => x.CompetitionFormatId).HasColumnName("competition_format_id"); b.Property(x => x.FormatPhaseId).HasColumnName("format_phase_id"); b.HasAlternateKey(x => new { x.FormatGroupId, x.CompetitionFormatId }).HasName("UQ_FORMAT_GROUP_id_format"); b.HasIndex(x => new { x.FormatPhaseId, x.Code }).IsUnique().HasDatabaseName("UQ_FORMAT_GROUP_code");
        b.Property(x => x.Code).HasColumnName("code").HasColumnType("varchar(30)"); b.Property(x => x.Name).HasColumnName("name").HasMaxLength(100); b.Property(x => x.GroupRole).AsSql("group_role",20); b.Property(x => x.Sequence).HasColumnName("sequence").HasColumnType("smallint"); b.Property(x => x.Rounds).HasColumnName("rounds").HasColumnType("smallint"); b.Property(x => x.FixtureMode).AsSql("fixture_mode",30); b.Property(x => x.CarryOverMode).AsSql("carry_over_mode",20); b.Property("Active").HasColumnName("active").HasDefaultValue(true);
    }
}

internal sealed class FormatPlayoffSeriesConfiguration : IEntityTypeConfiguration<FormatPlayoffSeries>
{
    public void Configure(EntityTypeBuilder<FormatPlayoffSeries> b)
    {
        b.ToTable("FORMAT_PLAYOFF_SERIES", "dbo", t => { t.HasCheckConstraint("CK_FORMAT_PLAYOFF_SERIES_sequence", "[sequence] > 0"); t.HasCheckConstraint("CK_FORMAT_PLAYOFF_SERIES_wins_required", "[wins_required] > 0"); t.HasCheckConstraint("CK_FORMAT_PLAYOFF_SERIES_initial_wins", "[team1_initial_wins] >= 0 AND [team2_initial_wins] >= 0 AND [team1_initial_wins] < [wins_required] AND [team2_initial_wins] < [wins_required]"); }); b.HasKey(x => x.FormatSeriesId); b.Property(x => x.FormatSeriesId).HasColumnName("format_series_id").UseIdentityColumn(); b.Property(x => x.CompetitionFormatId).HasColumnName("competition_format_id"); b.Property(x => x.FormatPhaseId).HasColumnName("format_phase_id"); b.HasAlternateKey(x => new { x.FormatSeriesId, x.CompetitionFormatId }).HasName("UQ_FORMAT_PLAYOFF_SERIES_id_format"); b.HasIndex(x => new { x.CompetitionFormatId, x.Code }).IsUnique().HasDatabaseName("UQ_FORMAT_PLAYOFF_SERIES_format_code");
        b.Property(x => x.Code).HasColumnName("code").HasColumnType("varchar(30)"); b.Property(x => x.Name).HasColumnName("name").HasMaxLength(100); b.Property(x => x.Sequence).HasColumnName("sequence").HasColumnType("smallint"); b.Property(x => x.WinsRequired).HasColumnName("wins_required").HasColumnType("smallint"); b.Property(x => x.Team1InitialWins).HasColumnName("team1_initial_wins").HasColumnType("smallint"); b.Property(x => x.Team2InitialWins).HasColumnName("team2_initial_wins").HasColumnType("smallint"); b.Property("Active").HasColumnName("active").HasDefaultValue(true);
        b.HasMany(x => x.ParticipantSources).WithOne().HasForeignKey(x => new { x.TargetFormatSeriesId, x.CompetitionFormatId }).HasPrincipalKey(x => new { x.FormatSeriesId, x.CompetitionFormatId }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FormatSeriesParticipantSourceConfiguration : IEntityTypeConfiguration<FormatSeriesParticipantSource>
{
    public void Configure(EntityTypeBuilder<FormatSeriesParticipantSource> b)
    {
        b.ToTable("FORMAT_SERIES_PARTICIPANT_SOURCE", "dbo", t => { t.HasCheckConstraint("CK_FORMAT_SERIES_SOURCE_side", "[target_side] IN (1,2)"); t.HasCheckConstraint("CK_FORMAT_SERIES_SOURCE_type", "[source_type] IN ('SERIES_WINNER','SERIES_LOSER')"); t.HasCheckConstraint("CK_FORMAT_SERIES_SOURCE_not_same", "[target_format_series_id] <> [source_format_series_id]"); }); b.HasKey(x => x.FormatSeriesParticipantSourceId); b.Property(x => x.FormatSeriesParticipantSourceId).HasColumnName("format_series_participant_source_id").UseIdentityColumn(); b.Property(x => x.CompetitionFormatId).HasColumnName("competition_format_id"); b.Property(x => x.TargetFormatSeriesId).HasColumnName("target_format_series_id"); b.Property(x => x.TargetSide).HasColumnName("target_side").HasColumnType("tinyint"); b.Property(x => x.SourceType).AsSql("source_type",20); b.Property(x => x.SourceFormatSeriesId).HasColumnName("source_format_series_id"); b.HasIndex(x => new { x.TargetFormatSeriesId, x.TargetSide }).IsUnique().HasDatabaseName("UQ_FORMAT_SERIES_SOURCE_target_side");
        b.HasOne(x => x.SourceSeries).WithMany().HasForeignKey(x => new { x.SourceFormatSeriesId, x.CompetitionFormatId }).HasPrincipalKey(x => new { x.FormatSeriesId, x.CompetitionFormatId }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FormatQualificationRuleConfiguration : IEntityTypeConfiguration<FormatQualificationRule>
{
    public void Configure(EntityTypeBuilder<FormatQualificationRule> b)
    {
        b.ToTable("FORMAT_QUALIFICATION_RULE", "dbo"); b.HasKey(x => x.QualificationRuleId); b.Property(x => x.QualificationRuleId).HasColumnName("qualification_rule_id").UseIdentityColumn(); b.Property(x => x.CompetitionFormatId).HasColumnName("competition_format_id"); b.Property(x => x.SourceFormatPhaseId).HasColumnName("source_format_phase_id"); b.Property(x => x.SourceFormatGroupId).HasColumnName("source_format_group_id"); b.Property(x => x.SelectionMode).AsSql("selection_mode",30); b.Property(x => x.FromPosition).HasColumnName("from_position"); b.Property(x => x.ToPosition).HasColumnName("to_position"); b.Property(x => x.TargetType).AsSql("target_type",20); b.Property(x => x.TargetFormatPhaseId).HasColumnName("target_format_phase_id"); b.Property(x => x.TargetFormatGroupId).HasColumnName("target_format_group_id"); b.Property(x => x.TargetFormatSeriesId).HasColumnName("target_format_series_id"); b.Property(x => x.TargetSide).HasColumnName("target_side"); b.Property(x => x.Sequence).HasColumnName("sequence");
        b.HasOne(x => x.SourcePhase).WithMany().HasForeignKey(x => new { x.SourceFormatPhaseId, x.CompetitionFormatId }).HasPrincipalKey(x => new { x.FormatPhaseId, x.CompetitionFormatId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SourceGroup).WithMany().HasForeignKey(x => new { x.SourceFormatGroupId, x.CompetitionFormatId }).HasPrincipalKey(x => new { x.FormatGroupId, x.CompetitionFormatId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.TargetPhase).WithMany().HasForeignKey(x => new { x.TargetFormatPhaseId, x.CompetitionFormatId }).HasPrincipalKey(x => new { x.FormatPhaseId, x.CompetitionFormatId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.TargetGroup).WithMany().HasForeignKey(x => new { x.TargetFormatGroupId, x.CompetitionFormatId }).HasPrincipalKey(x => new { x.FormatGroupId, x.CompetitionFormatId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.TargetSeries).WithMany().HasForeignKey(x => new { x.TargetFormatSeriesId, x.CompetitionFormatId }).HasPrincipalKey(x => new { x.FormatSeriesId, x.CompetitionFormatId }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FormatScoringRuleConfiguration : IEntityTypeConfiguration<FormatScoringRule> { public void Configure(EntityTypeBuilder<FormatScoringRule> b) { b.ToTable("FORMAT_SCORING_RULE","dbo"); b.HasKey(x=>x.FormatScoringRuleId); b.Property(x=>x.FormatScoringRuleId).HasColumnName("format_scoring_rule_id").UseIdentityColumn(); b.Property(x=>x.CompetitionFormatId).HasColumnName("competition_format_id"); b.Property(x=>x.WinnerSets).HasColumnName("winner_sets"); b.Property(x=>x.LoserSets).HasColumnName("loser_sets"); b.Property(x=>x.WinnerTablePoints).HasColumnName("winner_table_points"); b.Property(x=>x.LoserTablePoints).HasColumnName("loser_table_points"); b.HasIndex(x=>new{x.CompetitionFormatId,x.WinnerSets,x.LoserSets}).IsUnique().HasDatabaseName("UQ_FORMAT_SCORING_RULE_score"); } }
internal sealed class FormatTiebreakRuleConfiguration : IEntityTypeConfiguration<FormatTiebreakRule> { public void Configure(EntityTypeBuilder<FormatTiebreakRule> b) { b.ToTable("FORMAT_TIEBREAK_RULE","dbo"); b.HasKey(x=>x.FormatTiebreakRuleId); b.Property(x=>x.FormatTiebreakRuleId).HasColumnName("format_tiebreak_rule_id").UseIdentityColumn(); b.Property(x=>x.CompetitionFormatId).HasColumnName("competition_format_id"); b.Property(x=>x.Sequence).HasColumnName("sequence"); b.Property(x=>x.Criterion).AsSql("criterion",30); b.Property(x=>x.SortDirection).AsSql("sort_direction",4); b.HasIndex(x=>new{x.CompetitionFormatId,x.Sequence}).IsUnique().HasDatabaseName("UQ_FORMAT_TIEBREAK_RULE_sequence"); } }
internal sealed class FormatMovementRuleConfiguration : IEntityTypeConfiguration<FormatMovementRule> { public void Configure(EntityTypeBuilder<FormatMovementRule> b) { b.ToTable("FORMAT_MOVEMENT_RULE","dbo"); b.HasKey(x=>x.FormatMovementRuleId); b.Property(x=>x.FormatMovementRuleId).HasColumnName("format_movement_rule_id").UseIdentityColumn(); b.Property(x=>x.CompetitionFormatId).HasColumnName("competition_format_id"); b.Property(x=>x.MovementType).AsSql("movement_type",20); b.Property(x=>x.SourceType).AsSql("source_type",20); b.Property(x=>x.SourceFormatPhaseId).HasColumnName("source_format_phase_id"); b.Property(x=>x.SourceFormatGroupId).HasColumnName("source_format_group_id"); b.Property(x=>x.SourceFormatSeriesId).HasColumnName("source_format_series_id"); b.Property(x=>x.FromPosition).HasColumnName("from_position"); b.Property(x=>x.ToPosition).HasColumnName("to_position"); b.Property(x=>x.TargetLevelDelta).HasColumnName("target_level_delta"); b.Property(x=>x.AppliesIfTargetExists).HasColumnName("applies_if_target_exists").HasDefaultValue(true); b.HasOne(x=>x.SourcePhase).WithMany().HasForeignKey(x=>new{x.SourceFormatPhaseId,x.CompetitionFormatId}).HasPrincipalKey(x=>new{x.FormatPhaseId,x.CompetitionFormatId}).OnDelete(DeleteBehavior.Restrict); b.HasOne(x=>x.SourceGroup).WithMany().HasForeignKey(x=>new{x.SourceFormatGroupId,x.CompetitionFormatId}).HasPrincipalKey(x=>new{x.FormatGroupId,x.CompetitionFormatId}).OnDelete(DeleteBehavior.Restrict); b.HasOne(x=>x.SourceSeries).WithMany().HasForeignKey(x=>new{x.SourceFormatSeriesId,x.CompetitionFormatId}).HasPrincipalKey(x=>new{x.FormatSeriesId,x.CompetitionFormatId}).OnDelete(DeleteBehavior.Restrict); } }
