using LigaVolley.Domain.Common;
using LigaVolley.Domain.CompetitionFormats;

namespace LigaVolley.Application.CompetitionFormats;

internal static class CompetitionFormatDefinitionFactory
{
    public static CompetitionFormatValidationDto Validate(short minTeams, short maxTeams, CompetitionFormatDefinitionDto? definition)
    {
        var errors = new List<CompetitionFormatValidationErrorDto>();
        void Error(string code, string path, string message) => errors.Add(new(code, path, message));
        if (minTeams <= 1 || maxTeams < minTeams) Error("format.invalid_team_range", "minTeams", "MinTeams must be greater than one and MaxTeams cannot be lower than MinTeams.");
        if (definition is null) { Error("format.definition_required", "definition", "Definition is required."); return new(false, errors); }
        var phases = definition.Phases ?? [];
        if (phases.Count == 0) Error("format.phase_required", "definition.phases", "At least one phase is required.");
        ValidateCodes(phases.Select(x => x.Code), "definition.phases", "phase", Error);
        var allSeries = phases.SelectMany(x => x.Series ?? []).ToArray();
        ValidateCodes(allSeries.Select(x => x.Code), "definition.phases.series", "series", Error);
        var phaseByCode = phases.Where(x => !string.IsNullOrWhiteSpace(x.Code)).GroupBy(x => x.Code.Trim()).Where(x => x.Count() == 1).ToDictionary(x => x.Key, x => x.Single());
        var seriesByCode = allSeries.Where(x => !string.IsNullOrWhiteSpace(x.Code)).GroupBy(x => x.Code.Trim()).Where(x => x.Count() == 1).ToDictionary(x => x.Key, x => x.Single());

        for (var p = 0; p < phases.Count; p++)
        {
            var phase = phases[p]; var path = $"definition.phases[{p}]";
            Required(phase.Code, 30, path + ".code", Error); Required(phase.Name, 100, path + ".name", Error);
            if (phase.Sequence <= 0) Error("format.invalid_sequence", path + ".sequence", "Sequence must be positive.");
            if (phase.PhaseType == PhaseType.RoundRobin && (phase.Rounds is null or <= 0 || phase.FixtureMode is null or FixtureMode.Playoff)) Error("format.invalid_round_robin", path, "Round-robin phases require positive rounds and a non-playoff fixture mode.");
            if (phase.PhaseType == PhaseType.GroupStage && (phase.Groups?.Count ?? 0) == 0) Error("format.groups_required", path + ".groups", "Group-stage phases require at least one group.");
            if (phase.PhaseType == PhaseType.Playoff && (phase.Series?.Count ?? 0) == 0) Error("format.series_required", path + ".series", "Playoff phases require at least one series.");
            if (phase.PhaseType != PhaseType.Playoff && (phase.Series?.Count ?? 0) > 0) Error("format.series_phase_mismatch", path + ".series", "Series can only belong to playoff phases.");
            ValidateCodes((phase.Groups ?? []).Select(x => x.Code), path + ".groups", "group", Error);
            foreach (var (group, g) in (phase.Groups ?? []).Select((x, i) => (x, i)))
            {
                var gp = $"{path}.groups[{g}]"; Required(group.Code, 30, gp + ".code", Error); Required(group.Name, 100, gp + ".name", Error);
                if (group.Sequence <= 0 || group.Rounds <= 0) Error("format.invalid_group_values", gp, "Group sequence and rounds must be positive.");
                if (group.FixtureMode == FixtureMode.Playoff) Error("format.invalid_group_fixture_mode", gp + ".fixtureMode", "Groups cannot use playoff fixture mode.");
                if (group.CarryOverMode != CarryOverMode.None) Error("format.unsupported_carry_over", gp + ".carryOverMode", "Only CarryOverMode.None is supported in v1.");
            }
            foreach (var (series, s) in (phase.Series ?? []).Select((x, i) => (x, i)))
            {
                var sp = $"{path}.series[{s}]"; Required(series.Code, 30, sp + ".code", Error); Required(series.Name, 100, sp + ".name", Error);
                if (series.Sequence <= 0 || series.WinsRequired <= 0 || series.Team1InitialWins < 0 || series.Team2InitialWins < 0 || series.Team1InitialWins >= series.WinsRequired || series.Team2InitialWins >= series.WinsRequired) Error("format.invalid_series", sp, "Series sequence/wins are invalid.");
                foreach (var (source, i) in (series.ParticipantSources ?? []).Select((x, i) => (x, i)))
                {
                    var sourcePath = $"{sp}.participantSources[{i}]";
                    if (source.TargetSide is not 1 and not 2) Error("format.invalid_target_side", sourcePath + ".targetSide", "TargetSide must be 1 or 2.");
                    if (!seriesByCode.ContainsKey(source.SourceSeriesCode?.Trim() ?? "")) Error("format.series_not_found", sourcePath + ".sourceSeriesCode", "Source series was not found.");
                    if (source.SourceSeriesCode?.Trim() == series.Code?.Trim()) Error("format.same_series_source", sourcePath, "A series cannot source itself.");
                }
                if ((series.ParticipantSources ?? []).GroupBy(x => x.TargetSide).Any(x => x.Count() > 1)) Error("format.duplicate_series_side", sp + ".participantSources", "A series side can have only one series source.");
            }
        }

        foreach (var (rule, i) in (definition.QualificationRules ?? []).Select((x, i) => (x, i)))
        {
            var path = $"definition.qualificationRules[{i}]";
            if (!phaseByCode.TryGetValue(rule.SourcePhaseCode?.Trim() ?? "", out var sourcePhase)) Error("format.phase_not_found", path + ".sourcePhaseCode", "Source phase was not found.");
            if (!phaseByCode.TryGetValue(rule.TargetPhaseCode?.Trim() ?? "", out var targetPhase)) Error("format.phase_not_found", path + ".targetPhaseCode", "Target phase was not found.");
            if (rule.Sequence <= 0) Error("format.invalid_sequence", path + ".sequence", "Sequence must be positive.");
            var range = rule.SelectionMode == QualificationSelectionMode.PositionRange;
            if (range != (rule.FromPosition > 0 && rule.ToPosition >= rule.FromPosition)) Error("format.invalid_qualification_positions", path, "PositionRange requires a valid position interval; half selections require null positions.");
            if (!range && (rule.FromPosition.HasValue || rule.ToPosition.HasValue)) Error("format.invalid_qualification_positions", path, "Half selections cannot specify positions.");
            if (rule.ToPosition > maxTeams) Error("format.position_out_of_range", path + ".toPosition", "Position exceeds MaxTeams.");
            if (sourcePhase is not null && !GroupExists(sourcePhase, rule.SourceGroupCode)) Error("format.group_not_found", path + ".sourceGroupCode", "Source group was not found in source phase.");
            if (rule.TargetType == QualificationTargetType.Group)
            {
                if (targetPhase is null || !GroupExists(targetPhase, rule.TargetGroupCode) || rule.TargetSeriesCode is not null || rule.TargetSide is not null) Error("format.invalid_group_target", path, "Group target is inconsistent.");
            }
            else if (!range || rule.FromPosition != rule.ToPosition || rule.TargetGroupCode is not null || rule.TargetSide is not 1 and not 2 || !seriesByCode.TryGetValue(rule.TargetSeriesCode?.Trim() ?? "", out var targetSeries) || targetPhase is null || !(targetPhase.Series ?? []).Contains(targetSeries)) Error("format.invalid_series_target", path, "Series target is inconsistent.");
        }
        foreach (var group in (definition.ScoringRules ?? []).GroupBy(x => (x.WinnerSets, x.LoserSets))) if (group.Count() > 1) Error("format.duplicate_scoring_rule", "definition.scoringRules", "Duplicate scoring result.");
        foreach (var (rule, i) in (definition.ScoringRules ?? []).Select((x, i) => (x, i))) if (rule.WinnerSets != 3 || rule.LoserSets > 2 || rule.WinnerTablePoints < 0 || rule.LoserTablePoints < 0) Error("format.invalid_scoring_rule", $"definition.scoringRules[{i}]", "Scoring rule is outside SQL constraints.");
        if ((definition.TiebreakRules ?? []).GroupBy(x => x.Sequence).Any(x => x.Count() > 1)) Error("format.duplicate_tiebreak_sequence", "definition.tiebreakRules", "Tiebreak sequences must be unique.");

        foreach (var (rule, i) in (definition.MovementRules ?? []).Select((x, i) => (x, i)))
        {
            var path = $"definition.movementRules[{i}]"; phaseByCode.TryGetValue(rule.SourcePhaseCode?.Trim() ?? "", out var phase);
            if (phase is null || rule.FromPosition <= 0 || rule.ToPosition < rule.FromPosition || (rule.MovementType == MovementType.Promotion ? rule.TargetLevelDelta >= 0 : rule.TargetLevelDelta <= 0)) Error("format.invalid_movement_rule", path, "Movement rule values are inconsistent.");
            var hasGroup = phase is not null && GroupExists(phase, rule.SourceGroupCode); var hasSeries = phase is not null && seriesByCode.TryGetValue(rule.SourceSeriesCode?.Trim() ?? "", out var sr) && (phase.Series ?? []).Contains(sr);
            var sourceValid = rule.SourceType switch { MovementSourceType.PhasePosition or MovementSourceType.PhaseLastN => rule.SourceGroupCode is null && rule.SourceSeriesCode is null, MovementSourceType.GroupPosition or MovementSourceType.GroupLastN => hasGroup && rule.SourceSeriesCode is null, MovementSourceType.SeriesResult => rule.SourceGroupCode is null && hasSeries, _ => false };
            if (!sourceValid) Error("format.invalid_movement_source", path, "Movement source does not match its type.");
        }
        DetectCycles(phases, seriesByCode, Error);
        return new(errors.Count == 0, errors);
    }

    public static CompetitionFormat Build(string code, string name, string? description, short minTeams, short maxTeams, CompetitionFormatDefinitionDto definition)
    {
        var validation = Validate(minTeams, maxTeams, definition);
        if (!validation.IsValid) throw new DomainValidationException(string.Join(" ", validation.Errors.Select(x => $"{x.Path}: {x.Message}")));
        var format = new CompetitionFormat(code, name, description, minTeams, maxTeams);
        foreach (var input in definition.Phases)
        {
            var phase = new FormatPhase(input.Code, input.Name, input.PhaseType, input.PhaseRole, input.Sequence, input.Rounds, input.FixtureMode);
            foreach (var group in input.Groups ?? []) phase.Groups.Add(new(group.Code, group.Name, group.GroupRole, group.Sequence, group.Rounds, group.FixtureMode, group.CarryOverMode));
            foreach (var seriesInput in input.Series ?? []) phase.Series.Add(new(seriesInput.Code, seriesInput.Name, seriesInput.Sequence, seriesInput.WinsRequired, seriesInput.Team1InitialWins, seriesInput.Team2InitialWins));
            format.Phases.Add(phase);
        }
        var phases = format.Phases.ToDictionary(x => x.Code); var series = format.Phases.SelectMany(x => x.Series).ToDictionary(x => x.Code);
        foreach (var inputPhase in definition.Phases) foreach (var inputSeries in inputPhase.Series ?? []) foreach (var source in inputSeries.ParticipantSources ?? []) series[inputSeries.Code].ParticipantSources.Add(new(source.TargetSide, source.SourceType, series[source.SourceSeriesCode]));
        foreach (var r in definition.QualificationRules ?? []) { var sp = phases[r.SourcePhaseCode]; var tp = phases[r.TargetPhaseCode]; format.QualificationRules.Add(new(sp, FindGroup(sp, r.SourceGroupCode), r.SelectionMode, r.FromPosition, r.ToPosition, r.TargetType, tp, FindGroup(tp, r.TargetGroupCode), r.TargetSeriesCode is null ? null : series[r.TargetSeriesCode], r.TargetSide, r.Sequence)); }
        foreach (var r in definition.ScoringRules ?? []) format.ScoringRules.Add(new(r.WinnerSets, r.LoserSets, r.WinnerTablePoints, r.LoserTablePoints));
        foreach (var r in definition.TiebreakRules ?? []) format.TiebreakRules.Add(new(r.Sequence, r.Criterion, r.SortDirection));
        foreach (var r in definition.MovementRules ?? []) { var p = phases[r.SourcePhaseCode!]; format.MovementRules.Add(new(r.MovementType, r.SourceType, p, FindGroup(p, r.SourceGroupCode), r.SourceSeriesCode is null ? null : series[r.SourceSeriesCode], r.FromPosition, r.ToPosition, r.TargetLevelDelta, r.AppliesIfTargetExists)); }
        return format;
    }

    private static bool GroupExists(FormatPhaseInputDto phase, string? code) => code is null || (phase.Groups ?? []).Any(x => x.Code.Trim() == code.Trim());
    private static FormatGroup? FindGroup(FormatPhase phase, string? code) => code is null ? null : phase.Groups.Single(x => x.Code == code);
    private static void Required(string value, int max, string path, Action<string,string,string> error) { var length = value?.Trim().Length ?? 0; if (length == 0 || length > max) error("format.invalid_text", path, $"Value is required and cannot exceed {max} characters."); }
    private static void ValidateCodes(IEnumerable<string> codes, string path, string kind, Action<string,string,string> error) { var values = codes.Select(x => x?.Trim() ?? "").ToArray(); if (values.Where(x => x.Length > 0).GroupBy(x => x).Any(x => x.Count() > 1)) error($"format.duplicate_{kind}_code", path, $"{kind} codes must be unique."); }
    private static void DetectCycles(IReadOnlyList<FormatPhaseInputDto> phases, Dictionary<string, FormatPlayoffSeriesInputDto> series, Action<string,string,string> error)
    {
        var edges = phases.SelectMany(x => x.Series ?? []).Where(x => series.ContainsKey(x.Code.Trim())).GroupBy(x => x.Code.Trim()).ToDictionary(x => x.Key, x => (x.Single().ParticipantSources ?? []).Select(y => y.SourceSeriesCode.Trim()).Where(series.ContainsKey).ToArray());
        var visiting = new HashSet<string>(); var visited = new HashSet<string>();
        bool Visit(string node) { if (visiting.Contains(node)) return true; if (!visited.Add(node)) return false; visiting.Add(node); foreach (var next in edges[node]) if (Visit(next)) return true; visiting.Remove(node); return false; }
        if (edges.Keys.Any(Visit)) error("format.series_source_cycle", "definition.phases.series", "Series participant sources contain a cycle.");
    }
}
