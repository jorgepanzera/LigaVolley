using LigaVolley.Application.Common;using LigaVolley.Application.MatchOfficials;using LigaVolley.Domain.Fixtures;
namespace LigaVolley.Application.Tests;
public sealed class MatchOfficialAssignmentValidatorTests
{
 [Theory][InlineData(MatchStatus.Pending)][InlineData(MatchStatus.Scheduled)]public void Admin_accepts_pre_start_states(MatchStatus status)=>MatchOfficialAssignmentValidator.EnsureAdminEditable(status);
 [Theory][InlineData(MatchStatus.InProgress)][InlineData(MatchStatus.Finished)][InlineData(MatchStatus.Cancelled)][InlineData(MatchStatus.Suspended)]public void Admin_rejects_non_editable_states(MatchStatus status){var x=Assert.Throws<ResourceConflictException>(()=>MatchOfficialAssignmentValidator.EnsureAdminEditable(status));Assert.Equal("match_official_match_not_editable",x.Code);}
 [Fact]public void Scorer_replacement_is_only_allowed_in_progress(){MatchOfficialAssignmentValidator.EnsureScorerReplacementAllowed(MatchStatus.InProgress);var x=Assert.Throws<ResourceConflictException>(()=>MatchOfficialAssignmentValidator.EnsureScorerReplacementAllowed(MatchStatus.Finished));Assert.Equal("match_official_replacement_not_allowed",x.Code);}
}
