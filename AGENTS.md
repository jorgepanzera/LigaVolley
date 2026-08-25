# AGENTS.md — LigaVolley

## Objetivo

LigaVolley es una plataforma para administrar competiciones de voleibol, operar el acta electrónica de los partidos y ofrecer consulta pública de la información deportiva.

El sistema tendrá:

- un único backend ASP.NET Core/.NET;
- una única base SQL Server;
- tres frontends:
  - `LigaVolley.Admin`: administración de clubes, equipos, personas, planteles, competiciones, formatos, fixture y configuración;
  - `LigaVolley.Scorer`: consola operativa del partido y acta electrónica;
  - `LigaVolley.Public`: aplicación de consulta pública de competiciones, fixture, resultados, tablas de posiciones e información pública de partidos.

## Arquitectura acordada

El backend se implementará como **Modular Monolith**. No introducir microservicios salvo una decisión de arquitectura explícita posterior.

Capas/proyectos de referencia:

- `LigaVolley.Domain`: entidades, value objects, reglas e invariantes del dominio.
- `LigaVolley.Application`: casos de uso, comandos/queries, DTOs de aplicación, validaciones y puertos.
- `LigaVolley.Infrastructure`: persistencia SQL Server, integraciones y adaptadores.
- `LigaVolley.Api`: endpoints HTTP y composición de la aplicación.
- `LigaVolley.Admin`: frontend administrativo.
- `LigaVolley.Scorer`: frontend de scoring/acta electrónica.
- `LigaVolley.Public`: frontend de consulta pública.

## Convenciones de API

Los prefijos por consumidor son una convención **obligatoria** de la API:

- `/api/admin/...`
- `/api/scorer/...`
- `/api/public/...`

No reutilizar automáticamente el mismo DTO para Admin, Scorer y Public. Cada superficie puede requerir contratos distintos aunque opere sobre el mismo dominio.

## Módulos funcionales previstos

1. Security (transversal; proveedor de identidad y permisos finos aún no definidos).
2. Clubs / Teams / Venues.
3. People / Players / Coaches / Referees.
4. Competition Rosters.
5. Competitions.
6. Competition Formats / Phases / Groups / Qualification / Playoffs.
7. Fixture / Matches.
8. Match Officials.
9. Electronic Scoresheet / Scorer.
10. Public Query.

## Reglas clave de competición

Toda `Competition` debe estar asociada obligatoriamente a:

- una `Season`;
- una `Divisional`;
- un `CompetitionFormat`.

Una competición puede crearse de dos formas técnicas:

- `FROM_FORMAT`: seleccionando un `CompetitionFormat` existente;
- `FROM_COMPETITION`: tomando otra competición como referencia de estructura.

La opción funcional "desde cero" significa crear o seleccionar previamente un `CompetitionFormat`; no se crea una `Competition` sin formato.

Cuando una competición se crea basada en otra competición:

- se reutiliza el `CompetitionFormat` de la competición modelo;
- se crean nuevas instancias de fases, grupos y series para la nueva competición;
- no se duplica físicamente el `CompetitionFormat` como parte de ese caso de uso.

La clonación física de un formato es un caso de uso diferente (`CloneCompetitionFormat`).

Al crear una nueva competición basada en otra no se deben copiar:

- equipos;
- `TEAM_ENTRY`;
- fixture;
- partidos;
- resultados;
- fechas concretas;
- jugadores;
- planteles.

Un `CompetitionFormat` que ya haya sido utilizado por una competición operativa no debe modificarse estructuralmente. Para introducir una variante estructural se debe clonar el formato y modificar el clon. Los cambios descriptivos o de activación podrán permitirse según el caso de uso.

### Reglas resumidas de progresión y Scheduling

- La progresión de fases de liga/grupos se ejecuta mediante un caso de uso explícito `CompletePhase`; no finalizar fases cambiando libremente un status.
- Antes de completar una fase debe existir un preview sin persistencia y `CompletePhase` debe ser transaccional e idempotente.
- `TOP_HALF`/`BOTTOM_HALF`: si `N` es impar, la mitad superior/Championship recibe `(N+1)/2` y Relegation `(N-1)/2`.
- En v1 sólo se implementa `CarryOverMode.NONE`. No inventar semántica para `ALL` ni `QUALIFIED_ONLY`.
- `PHASE_GROUP_ENTRY` se materializa a partir de QualificationRules, no mediante CRUD libre.
- El fixture se genera incrementalmente cuando se conocen los participantes reales; no crear por defecto partidos futuros con participantes nulos.
- Semifinal, Final y Tercer Puesto son `PLAYOFF_SERIES`. Partido único equivale a `winsRequired = 1`.
- En playoffs se genera sólo el siguiente partido real necesario; las victorias de serie son `initialWins + realMatchWins`.
- Final y Tercer Puesto resuelven participantes mediante ganador/perdedor de semifinales.
- La Competition pasa automáticamente de `SCHEDULED` a `IN_PROGRESS` cuando comienza el primer partido oficial.
- El paso a `FINISHED` requiere `CompleteCompetition`; no cerrar automáticamente al terminar la Final.
- La regeneración de fixture debe rechazarse si algún partido del ámbito afectado ya está `IN_PROGRESS` o `FINISHED`.


## Modelo de personas y planteles

Entidades acordadas para este bloque:

- `PERSON`
- `PLAYER`
- `COACH`
- `REFEREE`
- `PLAYER_ROLE`
- `COMPETITION_ROSTER`
- `COMPETITION_ROSTER_PLAYER`
- `COMPETITION_ROSTER_STAFF`
- `MATCH_OFFICIAL`

Una misma `PERSON` puede tener simultáneamente registros en `PLAYER`, `COACH` y `REFEREE`. En esta etapa no se modelan vigencias temporales ni exclusividad entre esos roles salvo que aparezca un requisito explícito. Evitar modelar jugador, técnico y árbitro como personas independientes sin una raíz común `PERSON`.

`PLAYER_ROLE` representa el rol deportivo del jugador dentro del contexto competitivo/plantel, no una clasificación global e inmutable de la persona. Puede incluir, entre otros, la identificación de líbero.

Se reutilizan las entidades ya existentes `TEAM_ENTRY` y `MATCH` donde corresponda.

## Modelo del partido y Scorer

`LigaVolley.Scorer` debe comportarse como una **consola del partido**, no como una pantalla CRUD convencional.

El modelo debe soportar al menos este recorrido completo:

1. seleccionar/cargar y validar los planteles habilitados;
2. abrir acta;
3. asignar oficiales;
4. registrar alineación inicial P1..P6 de cada set;
5. iniciar set/partido;
6. registrar puntos;
7. cambio de saque;
8. rotación;
9. sustituciones normales;
10. entrada/salida/reemplazo de líbero;
11. timeout;
12. fin de set;
13. corrección/anulación de un punto o evento;
14. cierre del partido.

Debe soportarse un máximo de **dos líberos registrados/habilitados por equipo**. Las validaciones reglamentarias finas que determinen cuándo corresponde uno o dos líberos se definirán de forma explícita antes de implementarlas.

Los seis jugadores físicamente en cancha en cualquier instante se obtienen conceptualmente mediante:

`alineación inicial P1..P6 + sustituciones normales + rotation_offset + reemplazo de líbero activo`

No se adopta event sourcing. El estado operacional actual necesario para responder y operar eficientemente se persiste, y los cambios relevantes se registran además como eventos/auditoría para trazabilidad, correcciones y reconstrucción cuando corresponda. Evitar duplicar estado derivable sin una necesidad operacional clara.

El sistema debe poder responder en cualquier momento:

- marcador actual;
- set actual;
- equipo al saque;
- jugador servidor;
- rotación vigente;
- seis jugadores efectivos en cancha por equipo;
- sustituciones y líberos activos;
- timeouts utilizados;
- historial/correcciones relevantes.

## Offline y sincronización

El Scorer debe diseñarse contemplando funcionamiento offline/intermitente y sincronización posterior. No asumir conectividad permanente en decisiones de dominio o UI.

Las decisiones concretas de tecnología y protocolo de sincronización quedan abiertas hasta que se diseñe ese módulo.

## Reglas de implementación para Codex

- Antes de implementar un caso de uso, leer `AGENTS.md` y los documentos de `docs/` relacionados.
- No inventar reglas de voleibol no documentadas. Si una regla impacta el modelo o la persistencia y no está definida, dejarla explícita como pendiente.
- Priorizar claridad de dominio sobre abstracciones prematuras.
- Mantener el backend como Modular Monolith.
- No introducir mensajería, microservicios, CQRS distribuido o event sourcing como requisito arquitectónico salvo decisión explícita posterior.
- Escribir tests para reglas de dominio y casos de uso relevantes.
- Considerar cada vertical slice terminado únicamente cuando código, tests automatizados, documentación Swagger/OpenAPI y colección Postman estén implementados y sean consistentes entre sí.
- Al modificar el contrato HTTP de un slice existente, actualizar en el mismo cambio sus tests de integración, metadata/documentación Swagger/OpenAPI y requests/tests de Postman afectados.
- Las migraciones/esquemas SQL deben respetar las PK, FK y restricciones del modelo aprobado.
- Mantener nombres del dominio en inglés en código y base de datos, salvo que se acuerde lo contrario.
- Evitar que controllers/endpoints contengan reglas de negocio.
- En cada clase de endpoints implementada con Minimal API, agregar inmediatamente antes de cada `MapGet`, `MapPost`, `MapPut`, `MapPatch` o `MapDelete` un comentario con un ejemplo completo de método y URL; cuando el endpoint reciba body, agregar también un ejemplo JSON del body.
- Evitar dependencias de Infrastructure desde Domain/Application.
- No modificar documentos de arquitectura para justificar una implementación distinta: si hay contradicción, detener el cambio de código y señalarla.
- No exponer tablas internas de formato como CRUD HTTP por defecto; priorizar casos de uso sobre el agregado `CompetitionFormat`.
- No permitir que el frontend determine reglas estructurales que ya pertenecen al `CompetitionFormat` al generar fixture.

## Fuente de verdad

People v1 está cerrado: Player/Coach/Referee son perfiles 1:1 opcionales, sin
vigencias y nunca implícitos. Health Card y League Card son documentos históricos;
Health Card es warning para Player/Referee, no bloqueo, y Coach no la requiere.
No existe entidad League ni DELETE físico en People v1.

Competition Rosters v1 está cerrado: existe un roster explícito por TeamEntry con estados DRAFT/ACTIVE/CLOSED, sin mínimos de activación; máximos de 15 jugadores, 2 técnicos y 2 líberos ACTIVE. Los miembros INACTIVE permanecen históricos, ACTIVE sigue editable durante la Competition operativa, PLAYER_ROLE/dorsal son contextuales, Health Card nunca bloquea y no existe DELETE físico. Un roster CLOSED no es editable.

Match Officials v1 está cerrado: `MATCH_OFFICIAL` referencia `REFEREE`; los roles son FIRST_REFEREE, SECOND_REFEREE y SCORER, únicos por Match, y un Referee no puede ocupar dos roles. Admin edita en PENDING/SCHEDULED; Scorer reemplaza sin vaciar roles en IN_PROGRESS. Health Card nunca bloquea y los reemplazos deberán auditarse en el futuro MatchSheet.

En orden de prioridad:

1. decisiones explícitas más recientes del proyecto;
2. `AGENTS.md`;
3. documentos de `docs/`;
4. código y tests existentes.

Si dos documentos contradicen una decisión más reciente, actualizar la documentación antes de continuar implementando.

## Publicación pública

`LigaVolley.Public` y `/api/public` solo exponen información explícitamente habilitada para publicación; no equivalen a exponer todo lo almacenado. Como mínimo se contempla: competiciones visibles/publicadas, equipos participantes, fixture, resultados confirmados, tablas de posiciones e información pública de partidos. Planteles, personas, oficiales y otros datos requieren una regla explícita de publicación antes de exponerse.
