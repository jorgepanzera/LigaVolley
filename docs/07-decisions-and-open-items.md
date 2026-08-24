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

## Endpoints Admin cerrados en esta etapa

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

### Fixture / Scheduling inicial

- `POST /api/admin/competitions/{id}/fixture/generate`
- `GET /api/admin/competitions/{id}/fixture`
- `GET /api/admin/matches/{matchId}`
- `PUT /api/admin/matches/{matchId}/schedule`

## Pendientes que NO deben inventarse durante implementación

1. Tecnología exacta de los frontends.
2. Proveedor y esquema de autenticación/autorización.
3. Reglas finas de permisos.
4. Catálogo completo de endpoints de módulos aún no diseñados.
5. DTOs finales de People/Rosters/Match Officials/Scorer/Public.
6. Progresión completa entre fases: cierre de fase, aplicación de QualificationRules, poblamiento de grupos y generación automática/semiautomática de fases siguientes.
7. Algoritmo exacto de generación de fixtures para cada `FixtureMode`, aunque el contrato inicial ya está definido.
8. Política exacta para regenerar/reemplazar un fixture ya existente.
9. Reglas exactas para considerar una Competition preparada para pasar de `DRAFT` a `SCHEDULED` además de la existencia del fixture inicial.
10. Momento exacto de transición automática o manual `SCHEDULED → IN_PROGRESS` y `IN_PROGRESS → FINISHED`.
11. Política de edición de metadatos de Competition según estado.
12. Política exacta de eliminación/retiro de TeamEntry cuando ya existen partidos o fases dependientes.
13. Reglas funcionales finas de ascensos/descensos entre Apertura/Clausura o temporadas.
14. Estrategia concreta de persistencia local/offline del Scorer.
15. Protocolo de sincronización y resolución de conflictos.
16. Mecanismo exacto de corrección/anulación/compensación/versionado de eventos.
17. Límite exacto entre estado canónico, estado derivado y snapshots para offline.
18. Reglas reglamentarias finas sobre uso/habilitación de uno o dos líberos.
19. Reglas adicionales de publicación para planteles, personas, oficiales u otros datos públicos.
20. Reglas reglamentarias adicionales que aún no se hayan validado explícitamente.
21. Observabilidad, logging, tracing y despliegue.

## Próximo paso recomendado

Antes de implementar el bloque completo de Scheduling avanzado, cerrar:

1. progresión de fases y grupos;
2. aplicación de `FORMAT_QUALIFICATION_RULE` sobre instancias reales;
3. generación de segunda fase y playoffs;
4. resolución de participantes de series;
5. política de regeneración y edición de fixture;
6. transición de estados de Competition asociada al avance deportivo.

Para implementación incremental con Codex, puede comenzarse por:

1. `Season` + `Divisional`;
2. `CompetitionFormat`;
3. `Competition` y creación de estructura;
4. `TeamEntry`;
5. fixture inicial.
