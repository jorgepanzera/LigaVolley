using LigaVolley.Domain.Venues;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LigaVolley.Infrastructure.Persistence.Configurations;

internal sealed class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> builder)
    {
        builder.ToTable("VENUE", "dbo");
        builder.HasKey(x => x.VenueId).HasName("PK_VENUE");
        builder.Property(x => x.VenueId).HasColumnName("venue_id").UseIdentityColumn();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(x => x.Address).HasColumnName("address").HasMaxLength(250);
        builder.Property(x => x.Active).HasColumnName("active").HasDefaultValue(true);
        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("UQ_VENUE_name");
    }
}
