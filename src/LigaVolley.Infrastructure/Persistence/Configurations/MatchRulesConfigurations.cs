using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Competitions;
using LigaVolley.Domain.MatchSheets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LigaVolley.Infrastructure.Persistence.Configurations;

internal sealed class FormatMatchRulesConfiguration : IEntityTypeConfiguration<CompetitionFormat>
{
    public void Configure(EntityTypeBuilder<CompetitionFormat> b)
    {
        b.Property(x => x.MaxSubstitutionsPerSet).HasColumnName("max_substitutions_per_set").HasDefaultValue(6);
        b.Property(x => x.MaxTimeoutsPerSet).HasColumnName("max_timeouts_per_set").HasDefaultValue(2);
        b.ToTable("COMPETITION_FORMAT", "dbo", t => t.HasCheckConstraint("CK_COMPETITION_FORMAT_match_rules",
            "[max_substitutions_per_set] BETWEEN 1 AND 99 AND [max_timeouts_per_set] BETWEEN 1 AND 99"));
    }
}

internal sealed class CompetitionMatchRulesConfiguration : IEntityTypeConfiguration<Competition>
{
    public void Configure(EntityTypeBuilder<Competition> b)
    {
        b.Property(x => x.MaxSubstitutionsPerSetOverride).HasColumnName("max_substitutions_per_set_override");
        b.Property(x => x.MaxTimeoutsPerSetOverride).HasColumnName("max_timeouts_per_set_override");
        b.ToTable("COMPETITION", "dbo", t => t.HasCheckConstraint("CK_COMPETITION_match_rules",
            "([max_substitutions_per_set_override] IS NULL OR [max_substitutions_per_set_override] BETWEEN 1 AND 99) AND ([max_timeouts_per_set_override] IS NULL OR [max_timeouts_per_set_override] BETWEEN 1 AND 99)"));
    }
}

internal sealed class SheetMatchRulesConfiguration : IEntityTypeConfiguration<MatchSheet>
{
    public void Configure(EntityTypeBuilder<MatchSheet> b)
    {
        b.OwnsOne(x => x.RulesSnapshot, r =>
        {
            r.Property(x => x.RulesProtocolVersion).HasColumnName("rules_protocol_version").HasDefaultValue(1);
            r.Property(x => x.RulesSnapshotVersion).HasColumnName("rules_snapshot_version").HasDefaultValue(0);
            r.Property(x => x.MaxSubstitutionsPerSet).HasColumnName("max_substitutions_per_set");
            r.Property(x => x.MaxTimeoutsPerSet).HasColumnName("max_timeouts_per_set").HasDefaultValue(2);
            r.Property(x => x.LiberoEnabled).HasColumnName("libero_enabled").HasDefaultValue(true);
            r.Property(x => x.MaxLiberos).HasColumnName("max_liberos").HasDefaultValue(2);
            r.Property(x => x.LiberoCanServe).HasColumnName("libero_can_serve").HasDefaultValue(false);
            r.Property(x => x.DecidingSetCourtChangePoint).HasColumnName("deciding_set_court_change_point").HasDefaultValue(8);
        });
        b.Navigation(x => x.RulesSnapshot).IsRequired();
        b.ToTable("MATCH_SHEET", "dbo", t => t.HasCheckConstraint("CK_MATCH_SHEET_rules_snapshot",
            "[rules_snapshot_version] IN (0,1) AND [rules_protocol_version] = 1 AND ([rules_snapshot_version] = 0 OR [max_substitutions_per_set] BETWEEN 1 AND 99 AND [max_substitutions_per_set] IS NOT NULL) AND [max_timeouts_per_set] BETWEEN 1 AND 99"));
    }
}
