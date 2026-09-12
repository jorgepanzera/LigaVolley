using LigaVolley.Domain.CompetitionFormats;
using LigaVolley.Domain.Common;
using LigaVolley.Domain.Divisions;
using LigaVolley.Domain.TeamEntries;

namespace LigaVolley.Domain.Competitions;

public sealed class CompetitionMovement
{
    private CompetitionMovement() { }

    public CompetitionMovement(int competitionId, int movementRuleId, MovementType movementType, TeamEntry teamEntry,
        MovementSourceType sourceType, int sourcePhaseId, int? sourcePhaseGroupId, int? sourceSeriesId,
        short sourcePosition, short? standingPosition, int sourceDivisionId, int targetDivisionId,
        short targetLevelDelta, DateTimeOffset achievedAt)
    {
        if (competitionId <= 0 || movementRuleId <= 0 || sourcePhaseId <= 0 || sourcePosition <= 0 || targetDivisionId <= 0)
            throw new DomainValidationException("Competition movement references are required.");
        CompetitionId = competitionId; MovementRuleId = movementRuleId; MovementType = movementType;
        TeamEntry = teamEntry ?? throw new DomainValidationException("TeamEntry is required."); TeamEntryId = teamEntry.TeamEntryId; TeamId = teamEntry.TeamId;
        SourceType = sourceType; SourcePhaseId = sourcePhaseId; SourcePhaseGroupId = sourcePhaseGroupId; SourceSeriesId = sourceSeriesId;
        SourcePosition = sourcePosition; StandingPosition = standingPosition; SourceDivisionId = sourceDivisionId;
        TargetDivisionId = targetDivisionId; TargetLevelDelta = targetLevelDelta; AchievedAt = achievedAt;
    }

    public int CompetitionMovementId { get; private set; }
    public int CompetitionId { get; private set; }
    public int MovementRuleId { get; private set; }
    public MovementType MovementType { get; private set; }
    public int TeamEntryId { get; private set; }
    public TeamEntry TeamEntry { get; private set; } = null!;
    public int TeamId { get; private set; }
    public MovementSourceType SourceType { get; private set; }
    public int SourcePhaseId { get; private set; }
    public int? SourcePhaseGroupId { get; private set; }
    public int? SourceSeriesId { get; private set; }
    public short SourcePosition { get; private set; }
    public short? StandingPosition { get; private set; }
    public int SourceDivisionId { get; private set; }
    public Division? SourceDivision { get; private set; }
    public int TargetDivisionId { get; private set; }
    public Division TargetDivision { get; private set; } = null!;
    public short TargetLevelDelta { get; private set; }
    public DateTimeOffset AchievedAt { get; private set; }
}
