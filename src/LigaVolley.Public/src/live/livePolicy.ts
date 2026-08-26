import type{Live,Status}from'../api/types';
export function freshness(live:Live){if(live.status==='Finished')return{label:'FINALIZADO',tone:'final'};const age=live.lastUpdatedAt?Math.max(0,Math.floor((Date.parse(live.serverTime)-Date.parse(live.lastUpdatedAt))/1000)):null;if(live.status==='Suspended')return{label:`PARTIDO SUSPENDIDO${age===null?'':` · hace ${age}s`}`,tone:'suspended'};if(age===null||age<=30)return{label:'EN VIVO',tone:'live'};if(age<=120)return{label:`Actualizado hace ${age}s`,tone:'recent'};return{label:`Actualización demorada · hace ${age}s`,tone:'delayed'}}
export const normalDelay=(status:Status)=>status==='Suspended'?15000:status==='InProgress'?5000:null;
export const retryDelay=(failures:number)=>Math.min(30000,5000*2**Math.max(0,failures-1));
