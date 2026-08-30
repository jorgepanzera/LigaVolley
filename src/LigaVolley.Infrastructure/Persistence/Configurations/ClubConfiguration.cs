using LigaVolley.Domain.Clubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LigaVolley.Infrastructure.Persistence.Configurations;

internal sealed class ClubConfiguration : IEntityTypeConfiguration<Club>
{
    public void Configure(EntityTypeBuilder<Club> builder)
    {
        builder.ToTable("CLUB", "dbo");
        builder.HasKey(x => x.ClubId).HasName("PK_CLUB");
        builder.Property(x => x.ClubId).HasColumnName("club_id").UseIdentityColumn();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(x => x.ShortName).HasColumnName("short_name").HasMaxLength(50);
        builder.Property(x => x.Active).HasColumnName("active").HasDefaultValue(true);
        builder.Property(x => x.LogoStorageKey).HasColumnName("logo_storage_key").HasMaxLength(300);
        builder.Property(x => x.LogoContentType).HasColumnName("logo_content_type").HasMaxLength(50);
        builder.Property(x => x.LogoVersion).HasColumnName("logo_version").HasDefaultValue(0);
        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("UQ_CLUB_name");
    }
}
