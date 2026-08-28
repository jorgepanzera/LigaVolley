import type {ReactNode} from 'react';import {ApiProblemError} from '../api/adminApiClient';
export const LoadingState=()=> <div className="state">Cargando…</div>;
export const EmptyState=({children='No hay datos.'}:{children?:ReactNode})=><div className="state">{children}</div>;
export function ProblemDetailsAlert({error}:{error:unknown}){const p=error instanceof ApiProblemError?error.problem:{status:0,title:'Error',detail:error instanceof Error?error.message:'Ocurrió un error.'};return <div className="alert" role="alert"><strong>{p.title??'No se pudo completar la operación'}</strong><div>{p.detail}</div>{p.code&&<code>{p.code}</code>}</div>}
export const ErrorState=ProblemDetailsAlert;
export function ConfirmDialog({open,title,children,onConfirm,onClose,danger=false}:{open:boolean;title:string;children:ReactNode;onConfirm:()=>void;onClose:()=>void;danger?:boolean}){if(!open)return null;return <div className="modal"><div className="dialog"><h2>{title}</h2>{children}<div className="actions"><button onClick={onClose}>Cancelar</button><button className={danger?'danger':''} onClick={onConfirm}>Confirmar</button></div></div></div>}
export const DangerConfirmation=ConfirmDialog;
export function Toast({message}:{message?:string}){return message?<div className="toast">{message}</div>:null}
