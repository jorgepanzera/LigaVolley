using LigaVolley.Domain.MatchSheets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LigaVolley.Infrastructure.Persistence.Configurations;

internal sealed class MatchSheetOperationalUpdateConfiguration : IEntityTypeConfiguration<MatchSheet>
{
    public void Configure(EntityTypeBuilder<MatchSheet> builder)
        => builder.Property(x => x.LastOperationalUpdateAt).HasColumnName("last_operational_update_at");
}
