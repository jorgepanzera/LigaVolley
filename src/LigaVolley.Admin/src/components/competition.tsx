import { NavLink } from 'react-router-dom';
import type { Competition, Status } from '../types/admin';
const labels:Record<Status,string>={Draft:'Borrador',Scheduled:'Programada',InProgress:'En juego',Finished:'Finalizada',Cancelled:'Cancelada'};
export function StatusBadge({status}:{status:Status}){return <span className={`badge ${status.toLowerCase()}`}>{labels[status]}</span>}
export function CompetitionHeader({competition:c}:{competition:Competition}){const tabs=[['Resumen','overview'],['Participantes','entries'],['Fixture','fixture'],['Planteles','rosters'],['Progresión','progression'],...(c.status==='Finished'?[['Movimientos','movements']]:[])];return <><div className="competition-header"><div><h2>{c.name}</h2><p>{c.season.name} · {c.division.name} · {c.format.name}</p></div><StatusBadge status={c.status}/></div><nav className="tabs">{tabs.map(([name,path])=><NavLink key={path} to={`/admin/competitions/${c.competitionId}/${path}`}>{name}</NavLink>)}</nav></>}
