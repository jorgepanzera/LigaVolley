using LigaVolley.Domain.MatchSheets;

namespace LigaVolley.Application.MatchSheets;

public sealed record RuleWarningDto(string Code, IReadOnlyDictionary<string, int> Context, bool RequiresConfirmation);

public sealed record RulesSnapshotDto(int RulesProtocolVersion, int RulesSnapshotVersion,
    int? MaxSubstitutionsPerSet, int MaxTimeoutsPerSet, bool LiberoEnabled, int MaxLiberos,
    bool LiberoCanServe, int DecidingSetCourtChangePoint)
{
    public static RulesSnapshotDto From(RulesSnapshot rules) => new(rules.RulesProtocolVersion,
        rules.RulesSnapshotVersion, rules.MaxSubstitutionsPerSet, rules.MaxTimeoutsPerSet,
        rules.LiberoEnabled, rules.MaxLiberos, rules.LiberoCanServe, rules.DecidingSetCourtChangePoint);
}
