using System.Text.Json;
using LigaVolley.Domain.MatchSheets;

namespace LigaVolley.Domain.Tests;

public sealed class RulesAssistantParityTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static IEnumerable<object[]> Vectors() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "rules-assistant-v1.json")))
        .RootElement.EnumerateArray().Select(x => new object[] { x.GetProperty("name").GetString()!, x.GetRawText() });

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Evaluates_shared_vectors(string name, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var v = doc.RootElement;
        T? Read<T>(string key) => v.TryGetProperty(key, out var x) ? x.Deserialize<T>(Json) : default;
        var home = new RuleTeamState(Enumerable.Range(1, 15).ToArray(), [14,15], [1,2,3,4,5,6],
            Read<RuleSubstitution[]>("substitutions") ?? [], Read<RuleReplacement[]>("activeLiberos") ?? [],
            Read<int>("rotationOffset"), Read<int>("timeouts"), Read<int?>("lastLiberoRally"), Read<int?>("lastLiberoRegular"));
        var away = new RuleTeamState(Enumerable.Range(101, 15).ToArray(), [114,115], [101,102,103,104,105,106], [], [], 0, 0);
        var state = new RuleMatchState(Read<bool>("closed"), "IN_PROGRESS", "HOME", Read<int>("rallyCount"), true, true, home, away);
        var rules = v.TryGetProperty("maxSubstitutions", out var max) && max.ValueKind == JsonValueKind.Null ? RulesSnapshot.Legacy : RulesSnapshot.Create(Read<int?>("maxSubstitutions") ?? 6, 2);
        var evaluation = MatchRulesAssistant.Evaluate(state, rules, v.GetProperty("command").Deserialize<RuleCommand>(Json)!);
        Assert.Equal(Read<string[]>("hard")!, evaluation.HardViolations.Select(x => x.Code));
        Assert.Equal(Read<string[]>("warnings")!, evaluation.Warnings.Select(x => x.Code));
        Assert.All(evaluation.Warnings, warning => Assert.True(warning.RequiresConfirmation));
        foreach (var warning in evaluation.Warnings)
            Assert.Equal(v.GetProperty("warningContexts").GetProperty(warning.Code).Deserialize<Dictionary<string, int>>(Json)!.OrderBy(x => x.Key), warning.Context.OrderBy(x => x.Key));
        Assert.Equal(evaluation.HardViolations.Count > 0 ? "INVALID_STATE" : evaluation.Warnings.Count > 0 ? "WARNING_REQUIRES_CONFIRMATION" : "VALID", evaluation.Classification);
        Assert.False(string.IsNullOrWhiteSpace(name));
    }
}
