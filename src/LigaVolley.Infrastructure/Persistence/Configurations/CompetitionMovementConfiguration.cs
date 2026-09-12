using LigaVolley.Domain.Competitions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LigaVolley.Infrastructure.Persistence.Configurations;

internal sealed class CompetitionMovementConfiguration : IEntityTypeConfiguration<CompetitionMovement>
{
    public void Configure(EntityTypeBuilder<CompetitionMovement> b)
    {
        b.ToTable("COMPETITION_MOVEMENT", "dbo", t =>
        {
            t.HasCheckConstraint("CK_COMPETITION_MOVEMENT_source_position", "[source_position] > 0");
            t.HasCheckConstraint("CK_COMPETITION_MOVEMENT_standing_position", "[standing_position] IS NULL OR [standing_position] > 0");
        });
        b.HasKey(x => x.CompetitionMovementId); b.Property(x => x.CompetitionMovementId).HasColumnName("competition_movement_id").UseIdentityColumn();
        b.Property(x => x.CompetitionId).HasColumnName("competition_id"); b.Property(x => x.MovementRuleId).HasColumnName("movement_rule_id"); b.Property(x => x.MovementType).AsSql("movement_type", 20);
        b.Property(x => x.TeamEntryId).HasColumnName("team_entry_id"); b.Property(x => x.TeamId).HasColumnName("team_id"); b.Property(x => x.SourceType).AsSql("source_type", 20);
        b.Property(x => x.SourcePhaseId).HasColumnName("source_phase_id"); b.Property(x => x.SourcePhaseGroupId).HasColumnName("source_phase_group_id"); b.Property(x => x.SourceSeriesId).HasColumnName("source_series_id");
        b.Property(x => x.SourcePosition).HasColumnName("source_position"); b.Property(x => x.StandingPosition).HasColumnName("standing_position"); b.Property(x => x.SourceDivisionId).HasColumnName("source_division_id"); b.Property(x => x.TargetDivisionId).HasColumnName("target_division_id"); b.Property(x => x.TargetLevelDelta).HasColumnName("target_level_delta"); b.Property(x => x.AchievedAt).HasColumnName("achieved_at").HasColumnType("datetimeoffset");
        b.HasOne<Competition>().WithMany().HasForeignKey(x => x.CompetitionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.TeamEntry).WithMany().HasForeignKey(x => new { x.TeamEntryId, x.CompetitionId }).HasPrincipalKey(x => new { x.TeamEntryId, x.CompetitionId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SourceDivision).WithMany().HasForeignKey(x => x.SourceDivisionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.TargetDivision).WithMany().HasForeignKey(x => x.TargetDivisionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.CompetitionId, x.MovementRuleId, x.TeamEntryId }).IsUnique().HasDatabaseName("UQ_COMPETITION_MOVEMENT_rule_team");
        b.HasIndex(x => x.CompetitionId).HasDatabaseName("IX_COMPETITION_MOVEMENT_competition");
    }
}
