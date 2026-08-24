using LigaVolley.Domain.Seasons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LigaVolley.Infrastructure.Persistence.Configurations;

internal sealed class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> builder)
    {
        builder.ToTable("SEASON", "dbo", table =>
            table.HasCheckConstraint(
                "CK_SEASON_dates",
                "[end_date] IS NULL OR [start_date] IS NULL OR [end_date] >= [start_date]"));

        builder.HasKey(x => x.SeasonId).HasName("PK_SEASON");
        builder.Property(x => x.SeasonId).HasColumnName("season_id").UseIdentityColumn();
        builder.Property(x => x.Year).HasColumnName("year").HasColumnType("smallint").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.StartDate).HasColumnName("start_date").HasColumnType("date");
        builder.Property(x => x.EndDate).HasColumnName("end_date").HasColumnType("date");
        builder.Property(x => x.Active).HasColumnName("active").HasDefaultValue(true).IsRequired();
        builder.HasIndex(x => x.Year).IsUnique().HasDatabaseName("UQ_SEASON_year");
    }
}
