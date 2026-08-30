# 07 — Decisiones cerradas y pendientes

## Propósito

Este documento resume qué decisiones son fuente de verdad y qué aspectos siguen abiertos. El detalle estructural vive en `03-competition-formats.md`, los contratos HTTP en `06-api-design.md` y las reglas específicas en los documentos temáticos.

## Decisiones cerradas

### Arquitectura y superficies

- Backend único, SQL Server único y arquitectura Modular Monolith.
- Frontends separados: Admin, Scorer y Public.
- Prefijos obligatorios: `/api/admin`, `/api/scorer` y `/api/public`.
- Contratos HTTP diferenciados por consumidor.
- Estado operacional persistido más auditoría; no event sourcing.

### Admin y Competition Scheduling v1

- Admin usa React 18, TypeScript y Vite; es server-centric y consume sólo `/api/admin`.
- No usa PWA, Service Worker, IndexedDB, Dexie ni MatchEngine local.
- `DRAFT → SCHEDULED` se ejecuta mediante `schedule-preview` y `ScheduleCompetition`, nunca mediante cambio libre de status.
- Sólo TeamEntry ACTIVE cuenta para scheduling; el fixture inicial debe coincidir exactamente con ellos.
- Fecha, Venue, rosters y oficiales no bloquean Competition Schedule Readiness.
- `scheduled_at` registra la transición administrativa y un retry idempotente lo conserva.
- Al quedar SCHEDULED se congela el cuadro inicial; MatchDate y Venue continúan editables según las reglas del Match.

### Competitions, formatos y fixture

- Toda Competition referencia Season, Divisional y CompetitionFormat.
- Los modos de creación son `FROM_FORMAT` y `FROM_COMPETITION`.
- Crear desde otra Competition reutiliza el formato e instancia nueva estructura; no copia equipos, TeamEntry, fixture, resultados, fechas ni planteles.
- Clonar físicamente un CompetitionFormat es un caso de uso independiente.
- Un formato usado por una Competition fuera de DRAFT queda estructuralmente bloqueado; las variantes se crean mediante clonación.
- Team representa identidad permanente y TeamEntry la inscripción en una Competition.
- La estructura se instancia al crear la Competition. El frontend no reenvía reglas estructurales al generar fixture.
- El fixture posterior se genera incrementalmente cuando los participantes reales están resueltos; no se crean por defecto partidos con participantes nulos.
- La regeneración se rechaza si algún Match afectado está IN_PROGRESS o FINISHED.

### Competition Format Editor v1

- CompetitionFormat es un agregado editable sin autosave; Create y Clone nacen inactivos y Activate revalida todo.
- Active controla únicamente su disponibilidad para nuevas Competition, incluyendo FROM_COMPETITION.
- Uso y bloqueo son derivados. Una Competition fuera de DRAFT bloquea la estructura; sólo Name, Description y Active siguen editables.
- Modificar un formato nunca sincroniza estructuras ya instanciadas, tampoco en Competition DRAFT.
- La validación separa errores/warnings y simula cada cantidad entre MinTeams y MaxTeams.

### Progresión y cierre

- El cierre de una fase de liga/grupos usa `completion-preview` y `CompletePhase`; es transaccional e idempotente.
- Una fase con varios grupos se completa como unidad; no existe `CompletePhaseGroup` en v1.
- `TOP_HALF` recibe `(N+1)/2` cuando N es impar y `BOTTOM_HALF` recibe `(N-1)/2`.
- Sólo `CarryOverMode.NONE` tiene semántica implementada en v1.
- `PHASE_GROUP_ENTRY` se materializa desde QualificationRules y conserva `source_position` separado de `seed`.
- Semifinal, Final y Tercer Puesto son PLAYOFF_SERIES; partido único equivale a `winsRequired = 1`.
- Una serie genera sólo el siguiente partido real necesario y calcula victorias como `initialWins + realMatchWins`.
- Final y Tercer Puesto resuelven participantes mediante ganador o perdedor de semifinales.
- El primer partido oficial iniciado mueve Competition de SCHEDULED a IN_PROGRESS.
- Sólo `CompleteCompetition` mueve Competition a FINISHED y persiste `completed_at`.
- Los movimientos pueden calcularse, pero v1 no crea TeamEntry en futuras competiciones.

### People v1

- PERSON es la única raíz de identidad.
- PLAYER, COACH y REFEREE son perfiles 1:1 opcionales, simultáneos, sin vigencias y nunca implícitos.
- No existe DELETE físico de People ni entidad League.
- Health Card y League Card son documentos históricos.
- Health Card es warning para Player y Referee, nunca blocker; Coach no la requiere.

### Competition Rosters v1

- Existe como máximo un roster explícito por TeamEntry.
- Estados: DRAFT, ACTIVE y CLOSED; no hay mínimos para activar.
- Máximos ACTIVE: 15 jugadores, dos técnicos y dos líberos.
- Player y Coach son únicos dentro del roster; el dorsal es único entre jugadores ACTIVE.
- PLAYER_ROLE y dorsal son contextuales al roster.
- Los miembros INACTIVE permanecen históricos y no cuentan para máximos.
- Un roster ACTIVE sigue editable mientras Competition y TeamEntry sean operativos; CLOSED es read-only.
- Los cambios del roster no modifican una convocatoria ya materializada en MatchSheet.

### Match Officials v1

- MATCH_OFFICIAL referencia REFEREE.
- Roles exclusivos: FIRST_REFEREE, SECOND_REFEREE y SCORER.
- Existe como máximo un Referee por rol y un Referee no ocupa dos roles en el mismo Match.
- Admin edita en PENDING o SCHEDULED; desde IN_PROGRESS queda read-only.
- Scorer puede reemplazar un oficial vigente durante IN_PROGRESS sin dejar el rol vacío.
- Health Card es warning y no se validan conflictos de agenda.

### MatchSheet Opening y readiness

- Un Match SCHEDULED puede materializar un único MatchSheet OPEN.
- HOME y AWAY se resuelven desde Match.
- Ambos rosters deben estar ACTIVE, con al menos seis jugadores ACTIVE disponibles por lado, y deben existir los tres oficiales.
- La selección concreta de convocatoria pertenece a Scorer y se congela en MATCH_PLAYER y MATCH_TEAM_STAFF.
- Abrir el acta no inicia Match ni Competition; la apertura es transaccional e idempotente y crea sesión, auditoría y UUID.
- Match Scorer Readiness reutiliza las precondiciones comunes de OpenMatchSheet, es read-only y devuelve blockers dentro de una respuesta 200.
- MatchDate, Venue y Health Card son warnings para readiness.
- Un MatchSheet existente se proyecta normalmente y bloquea una nueva apertura.

### Electronic Scoresheet y Offline Sync v1

- Match best-of-5, primero a tres sets; sets 1..4 a 25 y set 5 a 15, siempre con diferencia mínima de dos.
- Los sets se preparan secuencialmente; P1..P6 sólo se edita en READY.
- Saque, servidor, rotación y cancha efectiva son derivados.
- Point termina el set automáticamente; el tercer set ganado decide el resultado, pero sólo CloseMatch cierra MatchSheet y Match.
- CorrectLastPoint cancela sólo el último evento deportivo efectivo y reconstruye el estado.
- Tracking de sustituciones y líbero es configurable y estable por MatchSheet; timeout es obligatorio, con máximo dos por equipo y set.
- La clave idempotente es EventUuid y la secuencia causal es local, contigua y reinicia en 1 por sesión.
- Sync acepta retries conocidos y aplica atómicamente todos los eventos nuevos contiguos.
- TakeOver abandona la sesión esperada, crea la única sesión ACTIVE con secuencia cero y conserva el estado deportivo.
- Scorer usa exactamente cinco stores IndexedDB: `appMeta`, `matchSheets`, `sessions`, `snapshots` y `events`.
- Reconciliation usa snapshot canónico más replay de pendientes. La pérdida de autoridad deja el runtime BLOCKED sin borrar eventos.

### Admin People & Match Operations v1

- Admin ofrece People UI, directorios Player/Coach/Referee y gestión de CompetitionRoster.
- Competition Workspace incluye Planteles.
- Match Workspace incluye Resumen, Preparación, Oficiales y Acta.
- Admin prepara y supervisa; no abre actas ni ejecuta acciones de scoring.
- MatchSheet Oversight es read-only y consulta sólo el estado central.
- Polling de oversight: 5 segundos en IN_PROGRESS, 15 segundos en SUSPENDED y sin polling continuo en estados estables.

### Public Query + Web v1

- Public es anónimo, read-only y server-centric.
- Son publicables SCHEDULED, IN_PROGRESS, FINISHED y CANCELLED; DRAFT y sus recursos dependientes responden 404.
- Fixture y resultados comparten recurso; standings reutiliza el cálculo canónico y playoffs se presentan como bracket/series.
- Match Detail contiene contexto y resultado; Live contiene el último estado operacional central.
- `LastOperationalUpdateAt` cambia sólo con mutaciones aceptadas y Live agrega `ServerTime`.
- Live usa polling de 5 segundos en IN_PROGRESS, 15 segundos en SUSPENDED, backoff 5/10/20/30 y se detiene en FINISHED.
- No hay SignalR, WebSocket, PWA, IndexedDB ni MatchEngine en Public v1.

### Demo Match Seed

- Disponible sólo en Development mediante `--seed-demo-match`.
- Reutiliza LIVOSUR 2026 y deja un Match SCHEDULED con rosters ACTIVE y tres oficiales.
- No abre MatchSheet ni inicia Match o Competition.
- Usa documentos `DEMO/LV-DEMO-*` y es idempotente.

### Competition Test Data Reset

- Comando exclusivo de Development: `dotnet run --project src/LigaVolley.Api -- --reset-competition-test-data`.
- Es destructivo, transaccional e idempotente. Antes de borrar verifica que existan Competition 1..24 sin huecos, que todas referencien CompetitionFormat 1 o 2 y que ambos formatos raíz existan.
- Conserva Competition 1..24 y los maestros compartidos; elimina Competition mayores a 24 y todo su grafo deportivo dependiente.
- Conserva las raíces CompetitionFormat 1 y 2, elimina sus hijos y los reconstruye canónicamente; elimina formatos desde ID 3.
- No sincroniza ni recrea fases, grupos o series ya materializados en las Competition preservadas. Cualquier fallo revierte la transacción completa.

### Logos de Clubs LIVOSUR 2026

En Development, primero se cargan los datos base y luego, mediante un comando separado, los logos aprobados:

```powershell
dotnet run --project src/LigaVolley.Api -- --seed-livosur-2026
dotnet run --project src/LigaVolley.Api -- --seed-livosur-2026-club-logos
```

El segundo comando sólo procesa `seed-assets/club-logos/manifest.csv` y sus imágenes. No crea Clubs ni otros datos: omite los Clubs inexistentes, valida el SHA-256 del paquete y es idempotente respecto de la versión y el archivo normalizado almacenado. La ubicación puede sobrescribirse con `Seed:Livosur2026ClubLogos:Path`.

### Admin Master Data y Club Logo v1

- Club, Team y Venue son maestros administrables, sin DELETE físico.
- Team pertenece obligatoriamente a Club; `Team.ClubId` no cambia después del alta y TeamEntry continúa siendo la participación contextual.
- Venue no pertenece a Club ni Team y su desactivación no altera Matches históricos.
- Club puede tener cero o un logo institucional actual; Team no tiene logo propio y lo proyecta desde Club, también en históricos.
- El binario vive en filesystem configurable y SQL Server conserva storage key, content type y versión. Se aceptan PNG/JPEG/WebP, máximo 2 MB y 2048x2048, normalizados sin agrandar a un máximo de 512x512.
- La URL pública cambia con la versión. Scorer queda fuera de esta proyección en v1.

## Pendientes que no deben inventarse

1. Proveedor y esquema de autenticación/autorización, roles, claims y permisos finos.
2. Auditoría definitiva de reemplazos de oficiales y permisos exactos del Scorer para esa operación.
3. Conflictos horarios, disponibilidad y agenda de árbitros; roles adicionales y ausencias excepcionales.
4. Edición explícita de una convocatoria ya materializada mientras el MatchSheet está OPEN.
5. Correcciones históricas distintas de CorrectLastPoint y correcciones de sustitución, líbero o timeout.
6. Reglas reglamentarias finas para habilitar uno o dos líberos.
7. Semántica deportiva de `CarryOverMode.ALL` y `QUALIFIED_ONLY`.
8. Consecuencia deportiva exacta de Match o serie CANCELLED, suspendida o no disputada.
9. Política exacta de retiro o eliminación de TeamEntry cuando existen dependencias deportivas.
10. Alcance y UX de regeneración cuando hay partidos programados aún no iniciados.
11. Política de edición de metadatos de Competition según estado.
12. Reglas finas de ascensos y descensos entre torneos o temporadas y creación de futuras inscripciones.
13. Reglas adicionales de publicación para personas, planteles, oficiales u otros datos.
14. Algoritmos específicos aún no cerrados para cada FixtureMode.
15. Observabilidad, logging, tracing y despliegue.

## Prioridad documental

Ante diferencias, aplicar este orden:

1. decisiones explícitas más recientes;
2. `AGENTS.md`;
3. documentos de `docs/`;
4. código y tests existentes.

Una contradicción real debe resolverse documentalmente antes de cambiar el código; no se debe inventar una regla para justificar la implementación.
