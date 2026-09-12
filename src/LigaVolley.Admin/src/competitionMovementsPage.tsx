import { useParams } from 'react-router-dom';
import { adminApi } from './api/adminApiClient';
import { EmptyState, LoadingState, ProblemDetailsAlert } from './components/feedback';
import { PageHeader } from './components/layout';
import { useQuery } from '@tanstack/react-query';

type Movement = { movementRuleId:number; movementType:'Promotion'|'Relegation'; teamEntryId:number; teamName:string; sourcePosition:number; sourceDivisionName:string; targetDivisionName:string };

export function CompetitionMovementsPage(){const{id=''}=useParams();const query=useQuery({queryKey:['movements',id],queryFn:()=>adminApi.get<Movement[]>(`/competitions/${id}/movements`)});if(query.isLoading)return <LoadingState/>;if(query.error)return <ProblemDetailsAlert error={query.error}/>;const movements=query.data??[];return <section><PageHeader title="Movimientos deportivos"/><p className="hint">Los movimientos registran el resultado deportivo de esta Competition. La participación en futuras competiciones se administra de forma independiente.</p>{movements.length?['Promotion','Relegation'].map(type=>{const rows=movements.filter(x=>x.movementType===type);return rows.length?<section className="card" key={type}><h3>{type==='Promotion'?'Ascensos':'Descensos'}</h3>{rows.map(row=><p key={`${row.movementRuleId}-${row.teamEntryId}`}><strong>{row.sourcePosition}º {row.teamName}</strong> · {row.sourceDivisionName} → {row.targetDivisionName}</p>)}</section>:null}):<EmptyState/>}</section>}
