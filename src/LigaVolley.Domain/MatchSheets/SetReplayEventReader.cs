using System.Text.Json;

namespace LigaVolley.Domain.MatchSheets;

/// <summary>Translates persisted sporting event payloads into the reducer's small internal vocabulary.</summary>
public static class SetReplayEventReader
{
    public static bool TryRead(MatchEvent source, out SetReplayEvent? replayEvent)
    {
        replayEvent = source.EventType switch
        {
            MatchEventType.Point when source.Side.HasValue => new ReplayPoint(source.Side.Value),
            MatchEventType.Timeout when source.Side.HasValue => new ReplayTimeout(source.Side.Value),
            MatchEventType.MisconductWarning or MatchEventType.MisconductPenalty or MatchEventType.Expulsion or
                MatchEventType.Disqualification or MatchEventType.ImproperRequest or MatchEventType.DelayWarning or MatchEventType.DelayPenalty
                when source.Side.HasValue => new ReplaySanction(source.Side.Value, source.EventType, source.MatchPlayerId, source.MatchTeamStaffId),
            _ => ReadPayload(source)
        };
        return replayEvent is not null;
    }

    private static SetReplayEvent? ReadPayload(MatchEvent source)
    {
        if (string.IsNullOrWhiteSpace(source.CommandPayload)) return null;
        try
        {
            using var doc = JsonDocument.Parse(source.CommandPayload);
            var root = doc.RootElement;
            return source.EventType switch
            {
                MatchEventType.Substitution or MatchEventType.SubstitutionRequest when source.Side.HasValue => ReadSubstitution(source.Side.Value, root),
                MatchEventType.LiberoEnter when source.Side.HasValue => new ReplayLiberoEnter(source.Side.Value, Int(root, "liberoMatchPlayerId"), Int(root, "replacedMatchPlayerId")),
                MatchEventType.LiberoExit when source.Side.HasValue => new ReplayLiberoExit(source.Side.Value, Int(root, "liberoMatchPlayerId")),
                _ => null
            };
        }
        catch (JsonException) { return null; }
    }

    private static ReplaySubstitution ReadSubstitution(MatchSide side, JsonElement root)
    {
        if (Property(root, "replacements", out var replacements))
            return new(side, replacements.EnumerateArray().Select(x => new RulePair(Int(x, "playerOutMatchPlayerId"), Int(x, "playerInMatchPlayerId"))).ToArray());
        return new(side, [new(Int(root, "playerOutMatchPlayerId"), Int(root, "playerInMatchPlayerId"))]);
    }
    private static int Int(JsonElement value, string name) => Property(value, name, out var property) && property.TryGetInt32(out var result) ? result : throw new JsonException($"'{name}' is required.");
    private static bool Property(JsonElement value, string name, out JsonElement property)
    {
        foreach (var candidate in value.EnumerateObject()) if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)) { property = candidate.Value; return true; }
        property = default; return false;
    }
}
