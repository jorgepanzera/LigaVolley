using LigaVolley.Application.Common;
using LigaVolley.Domain.MatchSheets;

namespace LigaVolley.Application.MatchSheets;

internal static class SanctionEventTypes
{
    public static MatchEventType From(SanctionType type) => type switch
    {
        SanctionType.MisconductWarning => MatchEventType.MisconductWarning,
        SanctionType.MisconductPenalty => MatchEventType.MisconductPenalty,
        SanctionType.Expulsion => MatchEventType.Expulsion,
        SanctionType.Disqualification => MatchEventType.Disqualification,
        SanctionType.ImproperRequest => MatchEventType.ImproperRequest,
        SanctionType.DelayWarning => MatchEventType.DelayWarning,
        SanctionType.DelayPenalty => MatchEventType.DelayPenalty,
        _ => throw new RequestValidationException("sanction_type_invalid", "Invalid sanction type.")
    };

    public static bool IsPenalty(SanctionType type) => type is SanctionType.MisconductPenalty or SanctionType.DelayPenalty;
}
