import type{Competition,CompetitionSummary,Fixture,Live,MatchDetail,Season,Standings}from'./types';
export class PublicApiError extends Error {
  constructor(public status: number, public code?: string) { super(`Public API: ${status}`); }
}
async function get<T>(path:string,signal?:AbortSignal):Promise<T>{const r=await fetch(`/api/public${path}`,{signal});if(!r.ok){const problem=await r.json().catch(()=>null);throw new PublicApiError(r.status,problem?.code);}return r.json() as Promise<T>}
export const publicApi={seasons:()=>get<Season[]>('/seasons'),competitions:(seasonId?:number)=>get<CompetitionSummary[]>(`/competitions${seasonId?`?seasonId=${seasonId}`:''}`),competition:(id:number)=>get<Competition>(`/competitions/${id}`),fixture:(id:number)=>get<Fixture>(`/competitions/${id}/fixture`),standings:(id:number)=>get<Standings>(`/competitions/${id}/standings`),match:(id:number)=>get<MatchDetail>(`/matches/${id}`),live:(id:number,signal?:AbortSignal)=>get<Live>(`/matches/${id}/live`,signal)};
