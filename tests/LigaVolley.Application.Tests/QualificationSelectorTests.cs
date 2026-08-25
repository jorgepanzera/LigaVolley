using LigaVolley.Application.PhaseCompletion;
using LigaVolley.Application.Standings;
using LigaVolley.Domain.CompetitionFormats;

namespace LigaVolley.Application.Tests;

public sealed class QualificationSelectorTests
{
    [Theory]
    [InlineData(QualificationSelectionMode.TopHalf, 10, 5, 1, 5)]
    [InlineData(QualificationSelectionMode.TopHalf, 9, 5, 1, 5)]
    [InlineData(QualificationSelectionMode.BottomHalf, 10, 5, 6, 10)]
    [InlineData(QualificationSelectionMode.BottomHalf, 9, 4, 6, 9)]
    public void HalvesFollowEvenAndOddRules(QualificationSelectionMode mode, int total, int expectedCount, int first, int last)
    {
        var result = QualificationSelector.Select(mode, null, null, 7, Positions(total), out var blocker);

        Assert.Null(blocker);
        Assert.Equal(expectedCount, result.Count);
        Assert.Equal(first, result[0].TeamEntryId);
        Assert.Equal(last, result[^1].TeamEntryId);
    }

    [Fact]
    public void InternalTieDoesNotBlockWhenBothTeamsAreSelected()
    {
        var positions = Positions(5).ToArray();
        positions[2] = positions[2] with { Position = 3, IsTied = true };
        positions[3] = positions[3] with { Position = 3, IsTied = true };

        var result = QualificationSelector.Select(QualificationSelectionMode.PositionRange, 1, 4, 11, positions, out var blocker);

        Assert.Null(blocker);
        Assert.Equal(new[] { 1, 2, 3, 4 }, result.Select(x => x.TeamEntryId));
    }

    [Fact]
    public void BoundaryTieReturnsStableBlockerWithAllTiedTeams()
    {
        var positions = Positions(5).ToArray();
        positions[2] = positions[2] with { Position = 3, IsTied = true };
        positions[3] = positions[3] with { Position = 3, IsTied = true };

        var result = QualificationSelector.Select(QualificationSelectionMode.PositionRange, 1, 3, 19, positions, out var blocker);

        Assert.Empty(result);
        Assert.Equal("qualification_boundary_tie", blocker!.Code);
        Assert.Equal(19, blocker.QualificationRuleId);
        Assert.Equal(new[] { 3, 4 }, blocker.TeamEntryIds);
    }

    private static IReadOnlyList<StandingPositionDto> Positions(int count) => Enumerable.Range(1, count)
        .Select(x => new StandingPositionDto(x, x, x, $"Team {x}", 0, 0, 0, 0, 0, null, 0, 0, null, 0, false))
        .ToArray();
}
