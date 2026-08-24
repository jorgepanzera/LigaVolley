using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LigaVolley.Infrastructure.Persistence.Configurations;

internal sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("TEAM", "dbo", table => table.HasCheckConstraint("CK_TEAM_gender", "[gender] IN ('M','F')"));
        builder.HasKey(x => x.TeamId).HasName("PK_TEAM");
        builder.Property(x => x.TeamId).HasColumnName("team_id").UseIdentityColumn();
        builder.Property(x => x.ClubId).HasColumnName("club_id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(x => x.Gender).HasColumnName("gender").HasColumnType("char(1)")
            .HasConversion(x => x == Gender.Male ? "M" : "F", x => x == "M" ? Gender.Male : Gender.Female);
        builder.Property(x => x.Active).HasColumnName("active").HasDefaultValue(true);
        builder.HasOne(x => x.Club).WithMany().HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_TEAM_CLUB");
        builder.HasIndex(x => new { x.Name, x.Gender }).IsUnique().HasDatabaseName("UQ_TEAM_name_gender");
    }
}
