export function matchSheetPollingInterval(status:string):number|false{
  if(status==='InProgress')return 5_000;
  if(status==='Suspended')return 15_000;
  return false;
}
