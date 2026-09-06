using LigaVolley.Domain.Common;

namespace LigaVolley.Domain.MatchSheets;

public sealed record RulesSnapshot(
    int RulesProtocolVersion,
    int RulesSnapshotVersion,
    int? MaxSubstitutionsPerSet,
    int MaxTimeoutsPerSet,
    bool LiberoEnabled,
    int MaxLiberos,
    bool LiberoCanServe,
    int DecidingSetCourtChangePoint)
{
    public static RulesSnapshot Legacy => new(1, 0, null, 2, true, 2, false, 8);
    public static RulesSnapshot Create(int substitutions, int timeouts)
    {
        ValidateLimit(substitutions);
        ValidateLimit(timeouts);
        return new(1, 1, substitutions, timeouts, true, 2, false, 8);
    }

    public static void ValidateLimit(int? value)
    {
        if (value is < 1 or > 99)
            throw new DomainValidationException("Match rule limits must be between 1 and 99.");
    }
}

public sealed record RuleIssue(string Code, IReadOnlyDictionary<string, int> Context, bool RequiresConfirmation);
public sealed record RuleEvaluation(IReadOnlyList<RuleIssue> HardViolations, IReadOnlyList<RuleIssue> Warnings)
{
    public string Classification => HardViolations.Count > 0 ? "INVALID_STATE" :
        Warnings.Count > 0 ? "WARNING_REQUIRES_CONFIRMATION" : "VALID";
}
