using LigaVolley.Domain.Divisions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LigaVolley.Infrastructure.Persistence.Configurations;

internal sealed class DivisionConfiguration : IEntityTypeConfiguration<Division>
{
    public void Configure(EntityTypeBuilder<Division> builder)
    {
        builder.ToTable("DIVISION", "dbo", table =>
        {
            table.HasCheckConstraint("CK_DIVISION_gender", "[gender] IN ('M','F')");
            table.HasCheckConstraint("CK_DIVISION_level_order", "[level_order] > 0");
        });

        builder.HasKey(x => x.DivisionId).HasName("PK_DIVISION");
        builder.Property(x => x.DivisionId).HasColumnName("division_id").UseIdentityColumn();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        builder.Property(x => x.LevelOrder).HasColumnName("level_order").HasColumnType("smallint").IsRequired();
        builder.Property(x => x.Gender)
            .HasColumnName("gender")
            .HasColumnType("char(1)")
            .HasConversion(value => value == Gender.Male ? "M" : "F", value => value == "M" ? Gender.Male : Gender.Female)
            .IsRequired();
        builder.Property(x => x.Active).HasColumnName("active").HasDefaultValue(true).IsRequired();
        builder.HasIndex(x => new { x.Name, x.Gender }).IsUnique().HasDatabaseName("UQ_DIVISION_name_gender");
        builder.HasIndex(x => new { x.LevelOrder, x.Gender }).IsUnique().HasDatabaseName("UQ_DIVISION_level_gender");
    }
}
