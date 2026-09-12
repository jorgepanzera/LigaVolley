# 05 — Acta electrónica y Scorer

## Responsabilidad y apertura

`LigaVolley.Scorer` es la consola operacional del partido. Admin prepara planteles y oficiales y supervisa en lectura; Public sólo proyecta estado canónico. Scorer puede trabajar sin conectividad, pero no decide reglas estructurales de competición.

`GET /api/scorer/matches/{matchId}/open-context` prepara la apertura sin persistir. `POST /open` exige un Match SCHEDULED dentro de una Competition SCHEDULED o IN_PROGRESS, rosters ACTIVE, tres oficiales y una convocatoria válida; crea una única `MATCH_SHEET` OPEN de forma transaccional e idempotente. Abrir el acta no inicia el Match ni la Competition.

La convocatoria congela `MATCH_PLAYER`, dorsal, capitanía, staff, declaración de líberos y `RulesSnapshot`. Debe haber al menos seis jugadores regulares por lado tras la declaración. `GET /sheet` devuelve el snapshot canónico para reentrada y reconciliación.

## Reglas congeladas y asistencia deportiva

El formato aporta por defecto seis sustituciones y dos timeouts por equipo/set. La Competition puede configurar overrides; al abrir el acta se congelan los valores efectivos, habilitación y máximo de líberos, saque de líbero, punto de cambio de campo y versiones de protocolo/snapshot. Las actas históricas no se reinterpretan.

Scorer separa evaluación de aplicación. HARD protege lifecycle, referencias, representación determinista, autoridad e integridad; una irregularidad deportiva representable es WARNING y requiere confirmación explícita antes de crear el único evento deportivo. Cancelar no consume UUID ni secuencia. Una confirmación forma parte del payload original; no existe `force` ni evento override separado.

El backend es autoridad de integridad, sync y persistencia canónica. Una decisión deportiva ya aceptada localmente se conserva durante sync aunque la evaluación deportiva del backend difiera. BLOCKED se reserva para autoridad, causalidad, integridad o problemas técnicos.

Las advertencias cubren, entre otras, límites o reingresos de sustitución, uso de líbero, servidor inesperado y exceso de timeout. Son HARD las referencias inválidas, jugadores duplicados en cancha, tracking deshabilitado, alineación inválida, líbero no declarado, lifecycle inválido y cualquier estado no representable. La respuesta para una confirmación faltante es `409 rule_confirmation_required` con advertencias y contexto concreto.

## Motor de partido

El partido es mejor de cinco: sets 1..4 a 25, set 5 a 15, siempre con diferencia de dos. Las alineaciones P1..P6 sólo se editan en READY. P1 determina el servidor inicial; un equipo que recupera saque rota, el que conserva saque no rota. Un punto cierra el set automáticamente cuando corresponde. El tercer set ganado decide el resultado; sólo `CloseMatch` deja MatchSheet CLOSED y Match FINISHED.

`CorrectLastPoint` anula el último evento deportivo efectivo y reconstruye el estado. CLOSED es definitivo. El cambio de campo al llegar a ocho en el set decisivo es un recordatorio no bloqueante; HOME permanece a la izquierda.

## Sustituciones, timeouts y líberos

Una sustitución modifica el jugador regular lógico aunque esté cubierto por un líbero; cuando éste sale, vuelve el regular vigente. Las solicitudes múltiples son atómicas y cada pareja cuenta para el límite. Timeout siempre se registra; un exceso representable requiere confirmación.

`CompetitionRosterPlayer.Role` es sólo una función habitual informativa. `MATCH_LIBERO`, creado desde `liberoCompetitionRosterPlayerIds` al abrir el acta, es la única declaración reglamentaria del partido. Una persona con función habitual `LIBERO` puede jugar regular y cualquier otra función puede ser declarada líbero.

La apertura valida que cada declarado pertenezca al lado, esté ACTIVE y convocado, que no haya duplicados ni más del máximo efectivo y que queden seis regulares. Ausencia, `null` o lista vacía declara cero líberos. Un retry idempotente con distinta declaración entra en conflicto.

La cancha efectiva combina alineación regular, sustituciones, offset de rotación y coberturas observadas. `PrepareSet` puede conservar un plan de sugerencias, pero no cambia la cancha. En actas v2, `StartSet`, `Point` y `CorrectLastPoint` tampoco crean ni eliminan coberturas: `LIBERO_ENTER`, `LIBERO_EXIT` e intercambio se registran de forma explícita. Sólo un líbero puede ser efectivo por lado; dos efectivos, referencias inválidas, lifecycle inválido y tracking deshabilitado son HARD. Frente, saque no permitido o reemplazo sin rally completado son warnings confirmables.

## Offline, sync e historial

Scorer es una PWA React/TypeScript/Vite. Dexie conserva exactamente cinco stores: `appMeta`, `matchSheets`, `sessions`, `snapshots` y `events`. Toda mutación válida aplica primero MatchEngine local y persiste evento PENDING, snapshot y secuencia en una transacción.

Sync usa UUID idempotente y secuencia local contigua por sesión. La reconciliación parte de un snapshot canónico y reaplica pendientes. `TakeOverMatchSheet` abandona la sesión esperada y crea la única ACTIVE sin alterar el estado deportivo. BLOCKED preserva cola y estado para recuperación; no es una sanción deportiva.

`MATCH_LIBERO`, las confirmaciones, las coberturas observadas y las sanciones disciplinarias se conservan en `GET /sheet`, IndexedDB, replay, sync y takeover; nunca se recalculan desde el roster. Una sanción identifica lado y sujeto (jugador, staff o equipo); las penalidades de conducta o demora otorgan exactamente un punto y saque al rival sin crear un evento `POINT` adicional. Actas y eventos v0/v1 mantienen su semántica histórica durante replay.

## Interfaces y límites

La consola mantiene HOME a la izquierda y AWAY a la derecha, con marcador y cancha efectiva P1..P6 como centro. Puntos son acciones primarias; drawers y modals agrupan decisiones secundarias. Los estados de apertura, READY, set en curso, fin de set, partido decidido y CLOSED son explícitos.

Admin muestra convocatoria congelada, declaraciones, cancha efectiva e historial. Public no publica planteles, funciones habituales ni oficiales y no ejecuta MatchEngine, IndexedDB o estado deportivo local.

No cubre obligatoriedad de uno o dos líberos, detección de posiciones físicas reales, corrección histórica general, autenticación nueva ni branching/rebase offline.
