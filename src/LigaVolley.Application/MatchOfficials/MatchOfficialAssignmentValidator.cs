using LigaVolley.Application.Common;using LigaVolley.Domain.Fixtures;using LigaVolley.Domain.MatchOfficials;
namespace LigaVolley.Application.MatchOfficials;
public static class MatchOfficialAssignmentValidator
{
 public static void EnsureAdminEditable(MatchStatus status){if(status is not MatchStatus.Pending and not MatchStatus.Scheduled)throw new ResourceConflictException("match_official_match_not_editable","Match officials are no longer editable from Admin.");}
 public static void EnsureUnique(IEnumerable<MatchOfficial> assignments,MatchOfficialRole role,int refereeId,MatchOfficial? current=null){if(assignments.Any(x=>x!=current&&x.Role==role))throw new ResourceConflictException("match_official_role_already_assigned","Role is already assigned.");if(assignments.Any(x=>x!=current&&x.RefereeId==refereeId))throw new ResourceConflictException("match_official_referee_already_assigned","Referee is already assigned.");}
 public static void EnsureScorerReplacementAllowed(MatchStatus status){if(status!=MatchStatus.InProgress)throw new ResourceConflictException("match_official_replacement_not_allowed","Replacement is only allowed while Match is InProgress.");}
}
