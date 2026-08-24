using LigaVolley.Domain.TeamEntries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LigaVolley.Infrastructure.Persistence.Configurations;

internal sealed class TeamEntryConfiguration : IEntityTypeConfiguration<TeamEntry>
{
    public void Configure(EntityTypeBuilder<TeamEntry> builder)
    {
        builder.ToTable("TEAM_ENTRY", "dbo", table =>
        {
            table.HasCheckConstraint("CK_TEAM_ENTRY_seed", "[seed] IS NULL OR [seed] > 0");
            table.HasCheckConstraint("CK_TEAM_ENTRY_status", "[status] IN ('REGISTERED','ACTIVE','WITHDRAWN','DISQUALIFIED')");
        });
        builder.HasKey(x => x.TeamEntryId).HasName("PK_TEAM_ENTRY");
        builder.Property(x => x.TeamEntryId).HasColumnName("team_entry_id").UseIdentityColumn();
        builder.Property(x => x.CompetitionId).HasColumnName("competition_id");
        builder.Property(x => x.TeamId).HasColumnName("team_id");
        builder.Property(x => x.Seed).HasColumnName("seed").HasColumnType("smallint");
        builder.Property(x => x.Status).AsSql("status", 20).HasDefaultValue(TeamEntryStatus.Registered);
        builder.Ignore(x => x.IsValid);
        builder.HasOne(x => x.Competition).WithMany().HasForeignKey(x => x.CompetitionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_TEAM_ENTRY_COMPETITION");
        builder.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_TEAM_ENTRY_TEAM");
        builder.HasIndex(x => new { x.CompetitionId, x.TeamId }).IsUnique().HasDatabaseName("UQ_TEAM_ENTRY");
        builder.HasAlternateKey(x => new { x.TeamEntryId, x.CompetitionId }).HasName("UQ_TEAM_ENTRY_id_comp");
        builder.HasIndex(x => new { x.CompetitionId, x.Status }).HasDatabaseName("IX_TEAM_ENTRY_competition");
    }
}
