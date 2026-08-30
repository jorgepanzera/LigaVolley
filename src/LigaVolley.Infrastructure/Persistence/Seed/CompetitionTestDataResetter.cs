using LigaVolley.Application.CompetitionFormats;
using LigaVolley.Domain.CompetitionFormats;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LigaVolley.Infrastructure.Persistence.Seed;

public sealed class CompetitionTestDataResetException(string message) : Exception(message);

public sealed record CompetitionTestDataResetResult(
    IReadOnlyDictionary<string, int> DeletedRows,
    IReadOnlyDictionary<int, IReadOnlyList<CompetitionFormatValidationErrorDto>> Warnings);

public sealed class CompetitionTestDataResetter(
    LigaVolleyDbContext db,
    ILogger<CompetitionTestDataResetter> logger)
{
    private const int LastPreservedCompetitionId = 24;

    public async Task<CompetitionTestDataResetResult> ResetAsync(bool isDevelopment, CancellationToken ct = default)
    {
        if (!isDevelopment)
            throw new CompetitionTestDataResetException("Competition test data reset is available only in Development.");

        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            await VerifyPreconditions(ct);
            logger.LogWarning("Resetting competition test data. Competition 1..24 and CompetitionFormat 1,2 will be preserved; Competition >24 and CompetitionFormat >=3 will be deleted.");

            var deleted = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var command in CompetitionDeletionCommands())
                deleted[command.Table] = await db.Database.ExecuteSqlRawAsync(command.Sql, ct);
            foreach (var command in FormatDeletionCommands())
                deleted[command.Table] = deleted.GetValueOrDefault(command.Table) + await db.Database.ExecuteSqlRawAsync(command.Sql, ct);

            db.ChangeTracker.Clear();
            var warnings = await RebuildCanonicalFormats(ct);
            await VerifyFinalState(ct);
            await transaction.CommitAsync(ct);

            foreach (var row in deleted.Where(x => x.Value > 0)) logger.LogInformation("Deleted {Rows} rows from {Table}.", row.Value, row.Key);
            foreach (var format in warnings)
                foreach (var warning in format.Value)
                    logger.LogWarning("CompetitionFormat {FormatId}: {Code} at {Path}: {Message}", format.Key, warning.Code, warning.Path, warning.Message);
            logger.LogInformation("Competition test data reset completed. Preserved Competition 1..24 and rebuilt active CompetitionFormat 1 and 2.");
            return new(deleted, warnings);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task VerifyPreconditions(CancellationToken ct)
    {
        var formatIds = await db.CompetitionFormats.AsNoTracking().OrderBy(x => x.CompetitionFormatId).Select(x => x.CompetitionFormatId).ToArrayAsync(ct);
        if (!formatIds.Contains(1) || !formatIds.Contains(2))
            throw new CompetitionTestDataResetException("Precondition failed: CompetitionFormat 1 and 2 must both exist.");

        var preserved = await db.Competitions.AsNoTracking().Where(x => x.CompetitionId <= LastPreservedCompetitionId)
            .OrderBy(x => x.CompetitionId).Select(x => new { x.CompetitionId, x.CompetitionFormatId }).ToArrayAsync(ct);
        var expectedIds = Enumerable.Range(1, LastPreservedCompetitionId).ToArray();
        if (!preserved.Select(x => x.CompetitionId).SequenceEqual(expectedIds))
            throw new CompetitionTestDataResetException("Precondition failed: Competition IDs 1 through 24 must all exist, without gaps.");
        var invalid = preserved.Where(x => x.CompetitionFormatId is not (1 or 2)).ToArray();
        if (invalid.Length > 0)
            throw new CompetitionTestDataResetException($"Precondition failed: preserved Competition {string.Join(", ", invalid.Select(x => x.CompetitionId))} references a format other than 1 or 2.");
    }

    private async Task<IReadOnlyDictionary<int, IReadOnlyList<CompetitionFormatValidationErrorDto>>> RebuildCanonicalFormats(CancellationToken ct)
    {
        var roots = await db.CompetitionFormats.Where(x => x.CompetitionFormatId == 1 || x.CompetitionFormatId == 2).ToDictionaryAsync(x => x.CompetitionFormatId, ct);
        var warnings = new Dictionary<int, IReadOnlyList<CompetitionFormatValidationErrorDto>>();
        foreach (var canonical in CanonicalCompetitionFormats.All)
        {
            var validation = CompetitionFormatDefinitionFactory.Validate(canonical.MinTeams, canonical.MaxTeams, canonical.Definition);
            if (!validation.IsValid || validation.TeamCounts.Any(x => !x.IsValid))
                throw new CompetitionTestDataResetException($"Canonical CompetitionFormat {canonical.Id} is invalid: {string.Join("; ", validation.Errors.Select(x => $"{x.Code} {x.Path}: {x.Message}"))}");
            var replacement = CompetitionFormatDefinitionFactory.Build(canonical.Code, canonical.Name, canonical.Description, canonical.MinTeams, canonical.MaxTeams, canonical.Definition);
            roots[canonical.Id].ReplaceWith(replacement);
            roots[canonical.Id].SetActive(true);
            warnings[canonical.Id] = validation.Warnings;
        }
        await db.SaveChangesAsync(ct);

        foreach (var canonical in CanonicalCompetitionFormats.All)
        {
            var validation = CompetitionFormatDefinitionFactory.Validate(canonical.MinTeams, canonical.MaxTeams, canonical.Definition);
            if (!validation.IsValid || validation.TeamCounts.Count != canonical.MaxTeams - canonical.MinTeams + 1 || validation.TeamCounts.Any(x => !x.IsValid))
                throw new CompetitionTestDataResetException($"Post-rebuild validation failed for CompetitionFormat {canonical.Id}.");
        }
        return warnings;
    }

    private async Task VerifyFinalState(CancellationToken ct)
    {
        var competitions = await db.Competitions.AsNoTracking().OrderBy(x => x.CompetitionId).Select(x => new { x.CompetitionId, x.CompetitionFormatId }).ToArrayAsync(ct);
        if (competitions.Length != 24 || !competitions.Select(x => x.CompetitionId).SequenceEqual(Enumerable.Range(1, 24)) || competitions.Any(x => x.CompetitionFormatId is not (1 or 2)))
            throw new CompetitionTestDataResetException("Final verification failed: Competition must contain exactly IDs 1 through 24 and reference only formats 1 or 2.");
        var formats = await db.CompetitionFormats.AsNoTracking().OrderBy(x => x.CompetitionFormatId).Select(x => new { x.CompetitionFormatId, x.Active }).ToArrayAsync(ct);
        if (formats.Length != 2 || formats[0].CompetitionFormatId != 1 || formats[1].CompetitionFormatId != 2 || formats.Any(x => !x.Active))
            throw new CompetitionTestDataResetException("Final verification failed: CompetitionFormat must contain exactly active IDs 1 and 2.");

        foreach (var canonical in CanonicalCompetitionFormats.All)
        {
            var format = await db.CompetitionFormats.AsNoTracking().AsSplitQuery()
                .Include(x => x.Phases).ThenInclude(x => x.Groups)
                .Include(x => x.Phases).ThenInclude(x => x.Series).ThenInclude(x => x.ParticipantSources)
                .Include(x => x.Phases).ThenInclude(x => x.Series).ThenInclude(x => x.ParticipantSources).ThenInclude(x => x.SourceSeries)
                .Include(x => x.QualificationRules).ThenInclude(x => x.SourcePhase)
                .Include(x => x.QualificationRules).ThenInclude(x => x.SourceGroup)
                .Include(x => x.QualificationRules).ThenInclude(x => x.TargetPhase)
                .Include(x => x.QualificationRules).ThenInclude(x => x.TargetGroup)
                .Include(x => x.QualificationRules).ThenInclude(x => x.TargetSeries)
                .Include(x => x.ScoringRules).Include(x => x.TiebreakRules)
                .Include(x => x.MovementRules).ThenInclude(x => x.SourcePhase)
                .Include(x => x.MovementRules).ThenInclude(x => x.SourceGroup)
                .Include(x => x.MovementRules).ThenInclude(x => x.SourceSeries)
                .SingleAsync(x => x.CompetitionFormatId == canonical.Id, ct);
            var actualDefinition = CompetitionFormatService.ToDefinition(format);
            var actualJson = JsonSerializer.Serialize(actualDefinition);
            var expectedJson = JsonSerializer.Serialize(canonical.Definition);
            if (format.Code != canonical.Code || format.Name != canonical.Name || format.Description != canonical.Description ||
                format.MinTeams != canonical.MinTeams || format.MaxTeams != canonical.MaxTeams || !format.Active ||
                actualJson != expectedJson)
                throw new CompetitionTestDataResetException($"Final verification failed: CompetitionFormat {canonical.Id} does not exactly match its canonical definition.");
        }
    }

    private static IReadOnlyList<DeleteCommand> CompetitionDeletionCommands() =>
    [
        D("MATCH_LINEUP_POSITION", "DELETE p FROM dbo.MATCH_LINEUP_POSITION p JOIN dbo.MATCH_LINEUP l ON l.match_lineup_id=p.match_lineup_id JOIN dbo.MATCH_SET s ON s.match_set_id=l.match_set_id JOIN dbo.[MATCH] m ON m.match_id=s.match_id WHERE m.competition_id>24"),
        D("MATCH_SET_LIBERO_PLAN", "DELETE p FROM dbo.MATCH_SET_LIBERO_PLAN p JOIN dbo.MATCH_SET s ON s.match_set_id=p.match_set_id JOIN dbo.[MATCH] m ON m.match_id=s.match_id WHERE m.competition_id>24"),
        D("MATCH_EVENT", "DELETE e FROM dbo.MATCH_EVENT e JOIN dbo.MATCH_SHEET sh ON sh.match_sheet_id=e.match_sheet_id JOIN dbo.[MATCH] m ON m.match_id=sh.match_id WHERE m.competition_id>24"),
        D("MATCH_SUBSTITUTION", "DELETE x FROM dbo.MATCH_SUBSTITUTION x JOIN dbo.MATCH_SET s ON s.match_set_id=x.match_set_id JOIN dbo.[MATCH] m ON m.match_id=s.match_id WHERE m.competition_id>24"),
        D("MATCH_LIBERO_REPLACEMENT", "DELETE x FROM dbo.MATCH_LIBERO_REPLACEMENT x JOIN dbo.MATCH_SET s ON s.match_set_id=x.match_set_id JOIN dbo.[MATCH] m ON m.match_id=s.match_id WHERE m.competition_id>24"),
        D("MATCH_TIMEOUT", "DELETE x FROM dbo.MATCH_TIMEOUT x JOIN dbo.MATCH_SET s ON s.match_set_id=x.match_set_id JOIN dbo.[MATCH] m ON m.match_id=s.match_id WHERE m.competition_id>24"),
        D("MATCH_LINEUP", "DELETE l FROM dbo.MATCH_LINEUP l JOIN dbo.MATCH_SET s ON s.match_set_id=l.match_set_id JOIN dbo.[MATCH] m ON m.match_id=s.match_id WHERE m.competition_id>24"),
        D("MATCH_LIBERO", "DELETE x FROM dbo.MATCH_LIBERO x JOIN dbo.MATCH_TEAM t ON t.match_team_id=x.match_team_id JOIN dbo.MATCH_SHEET sh ON sh.match_sheet_id=t.match_sheet_id JOIN dbo.[MATCH] m ON m.match_id=sh.match_id WHERE m.competition_id>24"),
        D("MATCH_TEAM_STAFF", "DELETE x FROM dbo.MATCH_TEAM_STAFF x JOIN dbo.MATCH_TEAM t ON t.match_team_id=x.match_team_id JOIN dbo.MATCH_SHEET sh ON sh.match_sheet_id=t.match_sheet_id JOIN dbo.[MATCH] m ON m.match_id=sh.match_id WHERE m.competition_id>24"),
        D("MATCH_PLAYER", "DELETE x FROM dbo.MATCH_PLAYER x JOIN dbo.MATCH_TEAM t ON t.match_team_id=x.match_team_id JOIN dbo.MATCH_SHEET sh ON sh.match_sheet_id=t.match_sheet_id JOIN dbo.[MATCH] m ON m.match_id=sh.match_id WHERE m.competition_id>24"),
        D("MATCH_SHEET_AUDIT", "DELETE x FROM dbo.MATCH_SHEET_AUDIT x JOIN dbo.MATCH_SHEET sh ON sh.match_sheet_id=x.match_sheet_id JOIN dbo.[MATCH] m ON m.match_id=sh.match_id WHERE m.competition_id>24"),
        D("MATCH_SHEET_SESSION", "DELETE x FROM dbo.MATCH_SHEET_SESSION x JOIN dbo.MATCH_SHEET sh ON sh.match_sheet_id=x.match_sheet_id JOIN dbo.[MATCH] m ON m.match_id=sh.match_id WHERE m.competition_id>24"),
        D("MATCH_SET", "DELETE s FROM dbo.MATCH_SET s JOIN dbo.[MATCH] m ON m.match_id=s.match_id WHERE m.competition_id>24"),
        D("MATCH_TEAM", "DELETE t FROM dbo.MATCH_TEAM t JOIN dbo.MATCH_SHEET sh ON sh.match_sheet_id=t.match_sheet_id JOIN dbo.[MATCH] m ON m.match_id=sh.match_id WHERE m.competition_id>24"),
        D("MATCH_SHEET", "DELETE sh FROM dbo.MATCH_SHEET sh JOIN dbo.[MATCH] m ON m.match_id=sh.match_id WHERE m.competition_id>24"),
        D("MATCH_OFFICIAL", "DELETE x FROM dbo.MATCH_OFFICIAL x JOIN dbo.[MATCH] m ON m.match_id=x.match_id WHERE m.competition_id>24"),
        D("SERIES_PARTICIPANT_SOURCE", "DELETE x FROM dbo.SERIES_PARTICIPANT_SOURCE x JOIN dbo.PLAYOFF_SERIES s ON s.playoff_series_id=x.target_playoff_series_id WHERE s.competition_id>24"),
        D("FIXTURE_GENERATION", "DELETE FROM dbo.FIXTURE_GENERATION WHERE competition_id>24"),
        D("MATCH", "DELETE FROM dbo.[MATCH] WHERE competition_id>24"),
        D("PHASE_GROUP_ENTRY", "DELETE FROM dbo.PHASE_GROUP_ENTRY WHERE competition_id>24"),
        D("COMPETITION_ROSTER_PLAYER", "DELETE p FROM dbo.COMPETITION_ROSTER_PLAYER p JOIN dbo.COMPETITION_ROSTER r ON r.competition_roster_id=p.competition_roster_id JOIN dbo.TEAM_ENTRY e ON e.team_entry_id=r.team_entry_id WHERE e.competition_id>24"),
        D("COMPETITION_ROSTER_STAFF", "DELETE p FROM dbo.COMPETITION_ROSTER_STAFF p JOIN dbo.COMPETITION_ROSTER r ON r.competition_roster_id=p.competition_roster_id JOIN dbo.TEAM_ENTRY e ON e.team_entry_id=r.team_entry_id WHERE e.competition_id>24"),
        D("COMPETITION_ROSTER", "DELETE r FROM dbo.COMPETITION_ROSTER r JOIN dbo.TEAM_ENTRY e ON e.team_entry_id=r.team_entry_id WHERE e.competition_id>24"),
        D("PLAYOFF_SERIES", "DELETE FROM dbo.PLAYOFF_SERIES WHERE competition_id>24"),
        D("PHASE_GROUP", "DELETE g FROM dbo.PHASE_GROUP g JOIN dbo.COMPETITION_PHASE p ON p.competition_phase_id=g.competition_phase_id WHERE p.competition_id>24"),
        D("COMPETITION_PHASE", "DELETE FROM dbo.COMPETITION_PHASE WHERE competition_id>24"),
        D("TEAM_ENTRY", "DELETE FROM dbo.TEAM_ENTRY WHERE competition_id>24"),
        D("COMPETITION", "DELETE FROM dbo.COMPETITION WHERE competition_id>24")
    ];

    private static IReadOnlyList<DeleteCommand> FormatDeletionCommands() =>
    [
        D("FORMAT_SERIES_PARTICIPANT_SOURCE", "DELETE FROM dbo.FORMAT_SERIES_PARTICIPANT_SOURCE WHERE competition_format_id>=1"),
        D("FORMAT_QUALIFICATION_RULE", "DELETE FROM dbo.FORMAT_QUALIFICATION_RULE WHERE competition_format_id>=1"),
        D("FORMAT_MOVEMENT_RULE", "DELETE FROM dbo.FORMAT_MOVEMENT_RULE WHERE competition_format_id>=1"),
        D("FORMAT_SCORING_RULE", "DELETE FROM dbo.FORMAT_SCORING_RULE WHERE competition_format_id>=1"),
        D("FORMAT_TIEBREAK_RULE", "DELETE FROM dbo.FORMAT_TIEBREAK_RULE WHERE competition_format_id>=1"),
        D("FORMAT_PLAYOFF_SERIES", "DELETE FROM dbo.FORMAT_PLAYOFF_SERIES WHERE competition_format_id>=1"),
        D("FORMAT_GROUP", "DELETE FROM dbo.FORMAT_GROUP WHERE competition_format_id>=1"),
        D("FORMAT_PHASE", "DELETE FROM dbo.FORMAT_PHASE WHERE competition_format_id>=1"),
        D("COMPETITION_FORMAT", "DELETE FROM dbo.COMPETITION_FORMAT WHERE competition_format_id>=3")
    ];

    private static DeleteCommand D(string table, string sql) => new(table, sql);
    private sealed record DeleteCommand(string Table, string Sql);
}
