# 07 — Decisiones y pendientes

## Decisiones cerradas

- Backend único.
- Base SQL Server única.
- Arquitectura backend Modular Monolith.
- Tres frontends: Admin, Scorer y Public.
- Prefijos de API obligatorios por consumidor: `/api/admin`, `/api/scorer` y `/api/public`.
- Flujo inicial del Scorer: seleccionar/validar planteles → abrir acta → asignar oficiales → alineación inicial.
- `Season` y `Divisional` son entidades maestras.
- Toda `Competition` referencia obligatoriamente una `Season`, una `Divisional` y un `CompetitionFormat`.
- No existe una `Competition` válida sin formato.
- Los modos técnicos iniciales de creación de Competition son `FROM_FORMAT` y `FROM_COMPETITION`.
- La opción funcional "desde cero" significa crear o seleccionar previamente un `CompetitionFormat`.
- Crear una Competition basada en otra Competition reutiliza el `CompetitionFormat` de la competición modelo y crea nuevas instancias de fases/grupos/series.
- Crear una Competition basada en otra no duplica físicamente el `CompetitionFormat`.
- Clonar físicamente un `CompetitionFormat` es un caso de uso independiente.
- Formatos de competición parametrizables.
- Un `CompetitionFormat` utilizado por una competición que ya dejó `DRAFT` queda estructuralmente bloqueado; para introducir una variante se debe clonar.
- La API de CompetitionFormat trabaja con el agregado completo y no expone por defecto CRUD directo para cada tabla `FORMAT_*`.
- Las referencias internas de una definición nueva de CompetitionFormat deben usar códigos lógicos (`code`) y no depender de IDs persistentes todavía inexistentes.
- No copiar equipos, `TEAM_ENTRY`, fixture, partidos, resultados, fechas ni planteles al crear una competición basada en otra.
- `Team` representa identidad permanente y `TeamEntry` la inscripción de un equipo en una Competition.
- Durante inscripción de equipos puede existir temporalmente menos de `minTeams`; nunca se puede superar `maxTeams`.
- Antes de generar fixture debe cumplirse el rango de equipos definido por el formato.
- La estructura de fases/grupos/series se instancia al crear Competition; no se requiere un endpoint administrativo separado de "instantiate structure".
- `PHASE_GROUP_ENTRY` se poblará cuando la clasificación/reglas de progresión determinen los participantes concretos de un grupo.
- El frontend no vuelve a enviar reglas estructurales de fixture que ya pertenecen al CompetitionFormat.
- El generador de fixture puede aceptar `randomSeed` opcional para reproducibilidad.
- No se crearán por defecto partidos de playoff con participantes nulos; se generan cuando los participantes estén resueltos.
- Estados iniciales de Competition: `DRAFT`, `SCHEDULED`, `IN_PROGRESS`, `FINISHED`, `CANCELLED`.
- Transición base: `DRAFT → SCHEDULED → IN_PROGRESS → FINISHED`; cancelación inicial desde `DRAFT` o `SCHEDULED`.
- La API debe validar transiciones de estado; el frontend no puede establecer estados arbitrariamente.
- La API utiliza `ProblemDetails` para errores HTTP y puede agregar un `code` de error estable.
- No usar entidades EF/Domain como contratos HTTP.
- Endpoints orientados a casos de uso/Application; no lógica de negocio en controllers.
- `PLAYER_ROLE` es contextual al ámbito competitivo/plantel, no una clasificación global inmutable del jugador.
- Una `PERSON` puede ser simultáneamente `PLAYER`, `COACH` y/o `REFEREE`; por ahora no se modelan vigencias ni exclusividad.
- No se adopta event sourcing: se persiste estado operacional actual y se registran eventos/auditoría relevantes.
- Public expone solo información explícitamente habilitada para publicación.
- Public es la aplicación de consulta pública de competiciones, fixture, resultados, tablas de posiciones e información pública de partidos.
- Scorer es una consola del partido.
- `PERSON` como raíz de jugador/técnico/árbitro.
- Planteles contextualizados por competición.
- Hasta dos líberos.
- Estado de seis jugadores efectivos derivado de alineación + sustituciones + rotación + líbero.
- Se contempla tercer puesto además de semifinales y final.
- Scorer debe contemplar offline/sincronización.

- La progresión deportiva se separa en resultados → standings → progresión → generación incremental de fixture.
- El cierre de fases de liga/grupos es un caso de uso administrativo explícito `CompletePhase`; no se expone un cambio libre de status para finalizar una fase.
- Antes de completar una fase existe un `completion-preview` sin efectos persistentes.
- `CompletePhase` debe ser transaccional e idempotente y no puede duplicar clasificados ni partidos ante reintentos.
- Una fase de liga/grupos sólo se completa cuando todos sus partidos requeridos están resueltos; `CANCELLED` no cuenta automáticamente como resultado deportivo resuelto.
- Una fase con varios grupos se completa como unidad en v1; no existe `CompletePhaseGroup`.
- `TOP_HALF`/`BOTTOM_HALF`: con N par se divide N/2 y N/2; con N impar Championship/TOP_HALF recibe `(N+1)/2` y Relegation/BOTTOM_HALF `(N-1)/2`.
- En v1 `CarryOverMode` soportado operativamente es sólo `NONE`; `ALL` y `QUALIFIED_ONLY` permanecen sin semántica implementable.
- `PHASE_GROUP_ENTRY.source_position` conserva la posición de clasificación en el origen y es independiente de `seed`.
- El fixture se genera incrementalmente cuando se resuelven los participantes reales de cada fase/grupo/serie.
- Semifinal, Final y Tercer Puesto se modelan siempre como `PLAYOFF_SERIES`.
- Un playoff a partido único se representa con `winsRequired = 1` e `initialWins = 0/0`.
- En una serie se genera sólo el siguiente partido real requerido; no se crean anticipadamente partidos que quizá no deban jugarse.
- Las victorias de una serie se calculan como `initialWins + realMatchWins`.
- Estados de `PLAYOFF_SERIES`: `PENDING`, `READY`, `IN_PROGRESS`, `FINISHED`, `CANCELLED` con la semántica documentada en `03` y `06`.
- Los participantes de Final y Tercer Puesto se resuelven automáticamente mediante `SERIES_WINNER`/`SERIES_LOSER`; el orden de finalización de semifinales no afecta la resolución.
- La progresión entre series playoff es automática al quedar una serie `FINISHED`; no requiere `CompletePhase` por cada serie.
- `SCHEDULED → IN_PROGRESS` ocurre automáticamente al comenzar el primer partido oficial.
- `IN_PROGRESS → FINISHED` requiere `CompleteCompetition`; no se cierra automáticamente al terminar la Final.
- Existe `completion-preview` de Competition antes del cierre.
- `FORMAT_MOVEMENT_RULE` puede calcular movimientos resultantes, pero v1 no crea automáticamente inscripciones en competiciones futuras.
- `CompetitionProgression` informa contadores de partidos actualmente materializados y victorias de serie derivadas; no mezcla blockers ni movimientos.
- `CompleteCompetition` es el único camino a `Competition.FINISHED`, persiste `completed_at`, es transaccional/idempotente y no modifica fases, series, partidos ni TeamEntry.
- Los movimientos usan posiciones de fase/grupo, resultado de serie o LAST_N y resuelven la Division destino por nivel exacto más mismo género, sin saltar niveles.
- Regeneración de fixture: se permite sólo mientras ningún partido del ámbito afectado esté `IN_PROGRESS` o `FINISHED`; puede invalidar programación de fecha/sede.
## Endpoints Admin cerrados en esta etapa

## Decisiones People v1 cerradas

- `PERSON` es la única raíz de identidad.
- `PLAYER`, `COACH` y `REFEREE` son perfiles 1:1 opcionales, simultáneos y sin vigencias.
- Los perfiles no se crean implícitamente y People no tiene DELETE físico.
- Los documentos adicionales contienen Health Card y League Card; no existe League en v1.
- Health Card es warning para Player/Referee y nunca bloqueo; Coach no la requiere.

## Decisiones Competition Rosters v1 cerradas

## Decisiones Match Officials v1 cerradas

- `MATCH_OFFICIAL` referencia `REFEREE`; roles v1: `FIRST_REFEREE`, `SECOND_REFEREE`, `SCORER`.
- Hay un oficial por rol y un Referee no ocupa dos roles del mismo Match.
- Admin designa inicialmente y edita sólo antes del inicio; Scorer reemplaza durante `IN_PROGRESS` sin dejar el rol vacío.
- La asignación vigente vive en `MATCH_OFFICIAL`; el reemplazo deberá auditarse en el futuro MatchSheet.
- Health Card es warning; no se validan conflictos de agenda.
- OpenMatchSheet deberá exigir los tres roles.
- Pendientes: auditoría definitiva `OFFICIAL_REPLACEMENT`, conflictos horarios, roles adicionales, ausencias excepcionales y permisos exactos de Scorer.

## Decisiones MatchSheet Opening v1 cerradas

- Un `MATCH_SHEET` por Match; abrir requiere Match SCHEDULED y no inicia Match/Competition.
- HOME/AWAY se resuelven desde Match. Ambos rosters deben estar ACTIVE y se seleccionan al menos seis jugadores ACTIVE por lado.
- Convocatoria, dorsal, staff y líberos se materializan y no siguen dinámicamente cambios del roster.
- Futuros lineups, sustituciones y eventos referencian `MATCH_PLAYER`.
- Se requieren FirstReferee, SecondReferee y Scorer; Health Card nunca bloquea.
- Apertura atómica/idempotente, sesión ACTIVE única, auditoría `MATCH_SHEET_OPENED` y UUID operativos para futuro offline.
- Pendientes: edición explícita de convocatoria mientras OPEN, inicio de partido/set, motor deportivo y protocolo de sync.

- Un roster por TeamEntry, con creación explícita y estados `DRAFT`, `ACTIVE`, `CLOSED`.
- Activación sin mínimos; máximos de 15 jugadores, dos técnicos y dos líberos activos.
- Miembros inactivos permanecen históricos y no existe DELETE físico.
- Un roster activo sigue editable durante una Competition operativa; uno cerrado no es editable.
- `PLAYER_ROLE` y dorsal son contextuales; Health Card es warning y nunca bloqueo.
- Competition `FINISHED` o `CANCELLED` y TeamEntry no operativo rechazan mutaciones.

### Season

- `GET /api/admin/seasons`
- `GET /api/admin/seasons/{id}`
- `POST /api/admin/seasons`
- `PUT /api/admin/seasons/{id}`
- `PATCH /api/admin/seasons/{id}/active`

### Divisional

- `GET /api/admin/divisions`
- `GET /api/admin/divisions/{id}`
- `POST /api/admin/divisions`
- `PUT /api/admin/divisions/{id}`
- `PATCH /api/admin/divisions/{id}/active`

### CompetitionFormat

- `GET /api/admin/competition-formats`
- `GET /api/admin/competition-formats/{id}`
- `POST /api/admin/competition-formats`
- `PUT /api/admin/competition-formats/{id}`
- `POST /api/admin/competition-formats/{id}/clone`
- `PATCH /api/admin/competition-formats/{id}/active`
- `POST /api/admin/competition-formats/validate`

### Competition

- `GET /api/admin/competitions`
- `GET /api/admin/competitions/{id}`
- `POST /api/admin/competitions`
- `PUT /api/admin/competitions/{id}`
- `PATCH /api/admin/competitions/{id}/status`
- `GET /api/admin/competitions/{id}/structure`

### TeamEntry

- `GET /api/admin/competitions/{id}/entries`
- `POST /api/admin/competitions/{id}/entries`
- `PATCH /api/admin/competitions/{id}/entries/{entryId}/seed`
- `PATCH /api/admin/competitions/{id}/entries/{entryId}/status`
- `DELETE /api/admin/competitions/{id}/entries/{entryId}`

### Fixture / Scheduling inicial y avanzado

- `POST /api/admin/competitions/{id}/fixture/generate`
- `POST /api/admin/competitions/{id}/fixture/regenerate`
- `GET /api/admin/competitions/{id}/fixture`
- `GET /api/admin/matches/{matchId}`
- `PUT /api/admin/matches/{matchId}/schedule`

### Progresión de fases

- `GET /api/admin/competitions/{competitionId}/phases/{phaseId}/completion-preview`
- `POST /api/admin/competitions/{competitionId}/phases/{phaseId}/complete`
- `GET /api/admin/competitions/{competitionId}/progression`

### Cierre de Competition

- `GET /api/admin/competitions/{competitionId}/completion-preview`
- `POST /api/admin/competitions/{competitionId}/complete`

## Pendientes que NO deben inventarse durante implementación

1. Tecnología exacta de los frontends.
2. Proveedor y esquema de autenticación/autorización.
3. Reglas finas de permisos.
4. Catálogo completo de endpoints de módulos aún no diseñados.
5. DTOs finales de People/Rosters/Match Officials/Scorer/Public.
6. Algoritmo exacto de generación de fixtures para cada `FixtureMode`; el contrato y las reglas de progresión ya están cerrados, pero no debe inventarse el algoritmo interno.
7. Alcance exacto de la regeneración cuando existan partidos programados pero todavía no iniciados y UX de confirmación de pérdida de fecha/sede.
8. Reglas exactas adicionales para considerar una Competition preparada para pasar de `DRAFT` a `SCHEDULED`, además de rango de equipos y fixture inicial válido.
9. Política de edición de metadatos de Competition según estado.
10. Política exacta de eliminación/retiro de TeamEntry cuando ya existen partidos o fases dependientes.
11. Consecuencia deportiva exacta de `MATCH`/serie `CANCELLED`, suspendida o no disputada.
12. Semántica deportiva futura de `CarryOverMode.ALL` y `CarryOverMode.QUALIFIED_ONLY`; v1 sólo implementa `NONE`.
13. Reglas funcionales finas de ascensos/descensos entre Apertura/Clausura o temporadas y creación futura de inscripciones.
14. Estrategia concreta de persistencia local/offline del Scorer.
15. Protocolo de sincronización y resolución de conflictos.
16. Mecanismo exacto de corrección/anulación/compensación/versionado de eventos.
17. Límite exacto entre estado canónico, estado derivado y snapshots para offline.
18. Reglas reglamentarias finas sobre uso/habilitación de uno o dos líberos.
19. Reglas adicionales de publicación para planteles, personas, oficiales u otros datos públicos.
20. Reglas reglamentarias adicionales que aún no se hayan validado explícitamente.
21. Observabilidad, logging, tracing y despliegue.

## Próximo paso recomendado

El bloque de contratos de `Competitions + CompetitionFormat + Scheduling` queda funcionalmente cerrado para una primera implementación, con excepción de los pendientes explícitos anteriores.

Para implementación incremental con Codex:

1. `Season` + `Divisional`;
2. `CompetitionFormat`;
3. `Competition` y creación de estructura;
4. `TeamEntry`;
5. fixture inicial;
6. standings necesarios para progresión;
7. `completion-preview` + `CompletePhase`;
8. poblamiento de `PHASE_GROUP_ENTRY` y fixture incremental;
9. resolución de `PLAYOFF_SERIES`;
10. `CompetitionProgression` + `CompleteCompetition`.

Antes de implementar Scorer, diseñar en detalle su contrato `open → estado/eventos locales → sync → close`.
