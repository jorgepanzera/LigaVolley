using LigaVolley.Application.Common;
using LigaVolley.Application.Standings;
using LigaVolley.Domain.CompetitionFormats;

namespace LigaVolley.Application.PhaseCompletion;

public static class QualificationSelector
{
    public static IReadOnlyList<StandingPositionDto> Select(
        QualificationSelectionMode mode,
        short? fromPosition,
        short? toPosition,
        int qualificationRuleId,
        IReadOnlyList<StandingPositionDto> positions,
        out PhaseCompletionBlockerDto? blocker)
    {
        blocker = null;
        var participantCount = positions.Count;
        int start;
        int count;

        switch (mode)
        {
            case QualificationSelectionMode.PositionRange:
                start = (fromPosition ?? 0) - 1;
                count = (toPosition ?? 0) - (fromPosition ?? 0) + 1;
                break;
            case QualificationSelectionMode.TopHalf:
                start = 0;
                count = (participantCount + 1) / 2;
                break;
            case QualificationSelectionMode.BottomHalf:
                count = participantCount / 2;
                start = participantCount - count;
                break;
            default:
                throw new ResourceConflictException("qualification_configuration_invalid", "Qualification selection mode is invalid.");
        }

        if (start < 0 || count < 0 || start + count > participantCount)
            throw new ResourceConflictException("qualification_configuration_invalid", "Qualification selection range is outside standings.");

        var boundaryIds = new HashSet<int>();
        AddBoundaryTie(positions, start, boundaryIds);
        AddBoundaryTie(positions, start + count, boundaryIds);

        if (boundaryIds.Count > 0)
        {
            var tiedPositions = positions.Where(x => boundaryIds.Contains(x.TeamEntryId)).Select(x => x.Position).Distinct().ToHashSet();
            foreach (var position in positions.Where(x => tiedPositions.Contains(x.Position)))
                boundaryIds.Add(position.TeamEntryId);

            blocker = new(
                "qualification_boundary_tie",
                "An unresolved sporting tie crosses a qualification boundary.",
                null,
                boundaryIds.OrderBy(x => x).ToArray(),
                qualificationRuleId);
            return [];
        }

        return positions.Skip(start).Take(count).ToArray();
    }

    private static void AddBoundaryTie(IReadOnlyList<StandingPositionDto> positions, int boundary, HashSet<int> ids)
    {
        if (boundary <= 0 || boundary >= positions.Count || positions[boundary - 1].Position != positions[boundary].Position)
            return;

        ids.Add(positions[boundary - 1].TeamEntryId);
        ids.Add(positions[boundary].TeamEntryId);
    }
}
