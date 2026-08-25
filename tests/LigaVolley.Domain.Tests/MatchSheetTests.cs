using LigaVolley.Domain.MatchSheets;
namespace LigaVolley.Domain.Tests;
public sealed class MatchSheetTests
{
 [Fact]public void Match_sheet_status_contract_contains_only_v1_states()=>Assert.Equal([MatchSheetStatus.Open,MatchSheetStatus.InProgress,MatchSheetStatus.Suspended,MatchSheetStatus.Closed,MatchSheetStatus.Cancelled],Enum.GetValues<MatchSheetStatus>());
}
