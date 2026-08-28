import{afterEach,describe,expect,it,vi}from'vitest';import{adminApi,ApiProblemError}from'./adminApiClient';
afterEach(()=>vi.unstubAllGlobals());
describe('AdminApiClient',()=>{
  for(const status of [400,404,409])it(`normaliza ProblemDetails ${status}`,async()=>{vi.stubGlobal('fetch',vi.fn().mockResolvedValue(new Response(JSON.stringify({title:'Problem',detail:'Detalle',code:`code_${status}`}),{status,headers:{'Content-Type':'application/problem+json'}})));await expect(adminApi.get('/test')).rejects.toMatchObject({problem:{status,code:`code_${status}`,detail:'Detalle'}})});
  it('expone una falla de red clara',async()=>{vi.stubGlobal('fetch',vi.fn().mockRejectedValue(new TypeError('offline')));await expect(adminApi.get('/test')).rejects.toMatchObject({problem:{status:0,title:'Sin conexión'}})});
  it('no simula éxito cuando POST falla',async()=>{vi.stubGlobal('fetch',vi.fn().mockResolvedValue(new Response(JSON.stringify({code:'competition_cannot_schedule'}),{status:409})));await expect(adminApi.post('/competitions/1/schedule')).rejects.toBeInstanceOf(ApiProblemError)});
});
