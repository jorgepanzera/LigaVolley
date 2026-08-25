# 05 — Partido en vivo y Scorer

## Objetivo

Modelar un partido completo desde la apertura del acta hasta el cierre, preservando suficiente información para reconstruir el estado reglamentario y permitir correcciones.

## Apertura implementada

`GET /api/scorer/matches/{matchId}/open-context` prepara sin persistencia Match, Competition, rosters activos, miembros activos, oficiales, warnings y acta existente. `POST /open` exige Match SCHEDULED, ambos rosters ACTIVE, tres oficiales y al menos seis jugadores por lado; materializa el universo del partido en una transacción y deja `MATCH_SHEET=OPEN` sin iniciar Match ni Competition. `GET /sheet` recupera el mismo snapshot para reentrada.

La apertura es idempotente por Match, respaldada por `UNIQUE(match_id)` y bloqueo serializable. La convocatoria, dorsales y UUID quedan congelados; no existen todavía set, alineación, saque, servidor ni líbero activo.

## Flujo mínimo validado conceptualmente

1. Cargar/seleccionar los planteles habilitados.
2. Abrir acta.
3. Asignar oficiales.
4. Definir alineación inicial del primer set.
5. Iniciar set.
6. Registrar puntos.
7. Gestionar cambio de saque y rotación.
8. Registrar sustituciones.
9. Registrar reemplazos de líbero.
10. Registrar timeouts.
11. Finalizar set.
12. Comenzar sets siguientes.
13. Corregir/anular un punto o evento cuando corresponda.
14. Cerrar partido.

## Jugadores efectivos en cancha

La definición acordada es:

`alineación inicial P1..P6 + sustituciones normales + rotation_offset + reemplazo de líbero activo = 6 jugadores físicamente en cancha`

Esta fórmula conceptual es central para el diseño.

### Alineación

Cada set comienza con seis posiciones reglamentarias P1..P6 por equipo.

### Rotación

La rotación se modela mediante un desplazamiento/estado (`rotation_offset` o equivalente) sobre la alineación vigente, evitando reescribir innecesariamente seis filas ante cada cambio de saque.

### Sustituciones

Las sustituciones normales modifican qué jugador ocupa la plaza lógica correspondiente para ese set.

### Líbero

El sistema debe soportar un máximo de dos líberos registrados/habilitados y registrar el reemplazo activo de líbero de manera diferenciada de una sustitución normal. Las reglas reglamentarias finas que determinen cuándo corresponde registrar uno o dos líberos quedan pendientes de definición explícita.

## Estado que debe poder obtenerse

Para cualquier instante relevante del partido:

- marcador por set y partido;
- equipo al saque;
- jugador servidor;
- rotación;
- seis jugadores efectivos en cancha por equipo;
- sustituciones realizadas;
- reemplazo de líbero activo;
- timeouts;
- secuencia de eventos;
- correcciones.

## Persistencia de estado y eventos

### Reemplazo de oficiales

Los tres oficiales se designan inicialmente desde Admin. Durante `IN_PROGRESS`, Scorer puede reemplazar el Referee vigente de un rol mediante un caso de uso específico, sin convertir Scorer en CRUD administrativo. `MATCH_OFFICIAL` conserva el estado canónico actual. El futuro MatchSheet deberá auditar un evento `OFFICIAL_REPLACEMENT` con rol, Referee anterior, Referee nuevo y fecha. Antes de `OpenMatchSheet` deberán existir los tres roles; esa precondición no forma parte de este slice.

No se adopta event sourcing como arquitectura. El estado operacional actual necesario para operar y consultar eficientemente el partido se persiste. Los cambios relevantes se registran además como eventos/auditoría para mantener trazabilidad y permitir correcciones o reconstrucción cuando corresponda.

El servidor persiste estado operacional canónico y eventos de trazabilidad; no usa event sourcing. `GET /sheet` devuelve el snapshot para reconciliación, la sesión vigente o última, el dispositivo y `LastAcceptedSequence`.

## Correcciones

Una corrección no debe destruir la trazabilidad necesaria del partido. El mecanismo exacto —anulación, compensación, versionado u otro equivalente— queda abierto hasta diseñar los casos de uso de corrección y sincronización del Scorer, pero debe preservar la consistencia del estado resultante y la historia relevante.

## Motor online v1

- Match best-of-5; primero a tres sets.
- Sets 1..4: 25 puntos y diferencia 2. Set 5: 15 puntos y diferencia 2. No hay máximo.
- `PrepareSet` crea solamente el siguiente set. Lineups P1..P6 son reemplazables en READY y definitivas en IN_PROGRESS.
- P1 es el servidor inicial; un receptor que gana rota `(offset + 1) % 6`. El equipo que conserva saque no rota.
- Point calcula marcador, saque, rotación, servidor y fin automático.
- `CorrectLastPoint` cancela únicamente el último evento deportivo efectivo y reconstruye el estado; nunca borra el POINT.
- Sustituciones conservan pareja titular/suplente y permiten reingreso del titular; deliberadamente no hay máximo global de seis.
- `TrackSubstitutions` y `TrackLiberoReplacements` pertenecen a MatchSheet. Si están deshabilitados no bloquean puntos.
- Un líbero declarado puede entrar por P1/P5/P6; sale restaurando la plaza lógica. Se admiten hasta dos declarados.
- Timeouts siempre se registran y tienen máximo dos por equipo/set.
- Tres sets ganados sólo marcan `MatchDecided`; `CloseMatch` explícito deja MatchSheet CLOSED y Match FINISHED. CLOSED no se reabre.
- CloseMatch reutiliza la progresión de playoffs dentro de la misma transacción; los partidos de liga quedan disponibles para standings.

## Offline

Scorer tolera pérdida temporal de conectividad mediante eventos locales con UUID y secuencia por sesión. Al reentrar, el cliente obtiene `GET /sheet`, confirma hasta `LastAcceptedSequence`, reconstruye desde el snapshot, reaplica sus eventos Pending y llama `/sync`. El batch tolera UUID ya aceptados y aplica atómicamente sólo una continuación contigua. IndexedDB, Service Worker y background sync quedan fuera del backend v1.

`TakeOverMatchSheet` requiere la sesión activa esperada para resolver concurrencia: la anterior pasa a ABANDONED y la nueva queda ACTIVE con secuencia cero. No reinicia marcador, set, saque, rotación, cancha, sustituciones, líbero ni timeouts. Una sesión abandonada puede reintentar eventos conocidos, pero sus eventos inéditos son rechazados; la recuperación manual futura no forma parte de v1.

## PWA Core v1

El frontend usa React, TypeScript, Vite, Dexie e IndexedDB. Los cinco stores son `appMeta`, `matchSheets`, `sessions`, `snapshots` y `events`; `deviceId` se genera una vez. Una acción aplica primero el motor local y guarda evento, snapshot y `nextLocalSequence` atómicamente. La UI se actualiza sin esperar HTTP.

Los eventos pasan por PENDING → SYNCING → ACCEPTED. Un cierre/reinicio devuelve SYNCING a PENDING. Ante timeout, red o 5xx se preserva operación offline; pérdida de sesión o conflictos de secuencia/UUID dejan BLOCKED sin borrar eventos. La reconciliación toma el snapshot canónico completo —incluidas alineaciones, sustituciones, líberos y puntos activos— y reaplica pendientes posteriores, por lo que eventos creados durante un request no desaparecen.

El Service Worker precachea exclusivamente App Shell y assets. No cachea `/api/scorer` como fuente deportiva. La reentrada busca primero IndexedDB y sólo después intenta reconciliar en segundo plano.
