import{describe,expect,it}from'vitest';import{matchSheetPollingInterval}from'./matchSheetPolling';
describe('Admin MatchSheet polling policy',()=>{
  it('polls in progress every five seconds',()=>expect(matchSheetPollingInterval('InProgress')).toBe(5000));
  it('polls suspended every fifteen seconds',()=>expect(matchSheetPollingInterval('Suspended')).toBe(15000));
  it('does not poll stable states',()=>expect(matchSheetPollingInterval('Finished')).toBe(false));
});
