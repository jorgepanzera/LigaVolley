export type ApiProblem={status:number;title?:string;detail?:string;code?:string;errors?:Record<string,string[]>};
export class ApiProblemError extends Error{constructor(public problem:ApiProblem){super(problem.detail??problem.title??`HTTP ${problem.status}`)}}
async function request<T>(path:string,init?:RequestInit):Promise<T>{
  let response:Response;
  try{response=await fetch(`/api/admin${path}`,{...init,headers:{Accept:'application/json',...(init?.body?{'Content-Type':'application/json'}:{}),...init?.headers}})}catch{throw new ApiProblemError({status:0,title:'Sin conexión',detail:'No se pudo conectar con LigaVolley API.'})}
  if(!response.ok){let body:Partial<ApiProblem>={};try{body=await response.json()}catch{/* non-json error */}throw new ApiProblemError({status:response.status,...body})}
  if(response.status===204)return undefined as T;return response.json() as Promise<T>;
}
export const adminApi={get:<T>(p:string)=>request<T>(p),post:<T>(p:string,b?:unknown)=>request<T>(p,{method:'POST',body:b===undefined?undefined:JSON.stringify(b)}),put:<T>(p:string,b:unknown)=>request<T>(p,{method:'PUT',body:JSON.stringify(b)}),patch:<T>(p:string,b:unknown)=>request<T>(p,{method:'PATCH',body:JSON.stringify(b)}),delete:<T>(p:string)=>request<T>(p,{method:'DELETE'})};
export async function uploadClubLogo(clubId:number,file:File){const body=new FormData();body.append('file',file);const response=await fetch(`/api/admin/clubs/${clubId}/logo`,{method:'PUT',body});if(!response.ok)throw new ApiProblemError({status:response.status,...await response.json()});return response.json()}
