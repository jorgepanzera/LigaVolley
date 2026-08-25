# 06 — Diseño de API

## Estado

La arquitectura de la API está acordada a nivel de módulos y superficies. Los contratos se cierran módulo por módulo antes de implementar cada bloque.

Este documento incorpora el contrato inicial detallado para:

- `Season`;
- `Divisional`;
- `CompetitionFormat`;
- `Competition`;
- `TeamEntry`;
- generación y consulta inicial de fixture;
- programación administrativa de partidos;
- progresión de fases y grupos;
- aplicación de QualificationRules;
- generación incremental de segunda fase y playoffs;
- cierre deportivo de fases y Competition;
- regeneración controlada de fixture.

## Superficies

## Competition Rosters Admin

El agregado se expone bajo `/api/admin/competitions/{competitionId}/entries/{teamEntryId}/roster`: `GET`, `POST`, `PATCH /status`, `POST /players`, `PUT /players/{rosterPlayerId}`, `PATCH /players/{rosterPlayerId}/status`, `POST /staff` y `PATCH /staff/{rosterStaffId}/status`. Las respuestas usan los DTOs específicos de Admin, enums JSON expresivos y `ProblemDetails` con `code`; creación devuelve 201, mutaciones 200, faltantes 404 y conflictos 409.

Los códigos estables incluyen `competition_roster_not_found`, `competition_roster_already_exists`, `competition_roster_closed`, `competition_roster_invalid_transition`, límites/duplicados de player/staff/libero/dorsal, mismatch de TeamEntry y Competition no editable.

### Admin

Prefijo obligatorio:

`/api/admin`

Responsabilidades previstas:

- CRUD/consulta de entidades maestras;
- personas y roles;
- planteles;
- competiciones;
- formatos;
- creación de competición desde formato o basada en otra competición;
- inscripciones de equipos;
- fixture y configuración de partidos.

### Scorer

Prefijo obligatorio:

`/api/scorer`

Responsabilidades previstas:

- abrir/consultar acta;
- obtener contexto operativo del partido;
- alineaciones;
- puntos/eventos;
- rotaciones;
- sustituciones;
- líberos;
- timeouts;
- fin de set;
- correcciones;
- cierre del partido;
- sincronización futura.

### Public

Prefijo obligatorio:

`/api/public`

Responsabilidades previstas:

- consultar competiciones publicadas;
- consultar fixture y calendario;
- consultar resultados;
- consultar tablas de posiciones;
- consultar información pública de partidos;
- consultar únicamente otros datos deportivos que tengan una regla explícita de publicación.

Esta superficie será consumida por `LigaVolley.Public` y es de solo consulta. No expone automáticamente toda la información disponible en la base de datos. Como mínimo se contemplan competiciones visibles/publicadas, equipos participantes, fixture, resultados confirmados, tablas de posiciones e información pública de partidos. Planteles, personas, oficiales u otros datos requieren una decisión explícita de visibilidad/publicación antes de incorporarse.

## Principios generales de contratos HTTP

- No usar entidades EF/Domain directamente como contratos HTTP.
- Separar DTOs según caso de uso y consumidor.
- Un mismo concepto puede tener DTO de Admin, Scorer y Public.
- Los endpoints delegan en casos de uso de `Application`.
- Controllers/endpoints no contienen reglas de negocio.
- No reflejar automáticamente cada tabla SQL mediante CRUD HTTP.
- Priorizar endpoints orientados a casos de uso y agregados.
- Usar enums/nombres expresivos en el contrato y no exponer detalles físicos como `CHAR(1)` de SQL cuando no aportan valor al consumidor.

## Convenciones HTTP iniciales

- `GET` correcto → `200 OK`.
- `POST` de creación → `201 Created`.
- `PUT`/`PATCH` correcto → `200 OK`.
- `DELETE` correcto sin body → `204 No Content`.
- request inválido → `400 Bad Request`.
- no autenticado → `401 Unauthorized` cuando Security quede implementado.
- no autorizado → `403 Forbidden`.
- recurso inexistente → `404 Not Found`.
- conflicto de dominio/estado → `409 Conflict`.

Los errores deben utilizar `ProblemDetails` (`application/problem+json`) y pueden incorporar una extensión `code` estable para que los frontends puedan reaccionar programáticamente.

Ejemplos de conflictos de dominio:

- temporada duplicada;
- divisional duplicada para el mismo género/nivel;
- `CompetitionFormat.code` duplicado;
- equipo ya inscrito en una competición;
- intento de modificar estructuralmente un formato bloqueado;
- intento de quitar una inscripción cuando ya existe fixture incompatible.

# 1. Season

## Endpoints

| Caso de uso | Método + endpoint | Request | Response | Entidades afectadas | Reglas principales |
|---|---|---|---|---|---|
| Listar temporadas | `GET /api/admin/seasons` | query filters | `SeasonSummaryDto[]` | `SEASON` | filtros opcionales por active/año |
| Obtener temporada | `GET /api/admin/seasons/{id}` | — | `SeasonDto` | `SEASON` | 404 si no existe |
| Crear temporada | `POST /api/admin/seasons` | `CreateSeasonRequest` | `SeasonDto` | `SEASON` | año único; fechas coherentes |
| Modificar temporada | `PUT /api/admin/seasons/{id}` | `UpdateSeasonRequest` | `SeasonDto` | `SEASON` | año único; fechas coherentes |
| Activar/desactivar | `PATCH /api/admin/seasons/{id}/active` | `SetActiveRequest` | `SeasonDto` | `SEASON` | preferir desactivación a borrado histórico |

No se incorpora `DELETE /seasons` en esta etapa.

## DTOs conceptuales

```csharp
public sealed record CreateSeasonRequest(
    short Year,
    string Name,
    DateOnly? StartDate,
    DateOnly? EndDate
);

public sealed record UpdateSeasonRequest(
    short Year,
    string Name,
    DateOnly? StartDate,
    DateOnly? EndDate
);

public sealed record SeasonDto(
    int SeasonId,
    short Year,
    string Name,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool Active
);

public sealed record SeasonSummaryDto(
    int SeasonId,
    short Year,
    string Name,
    bool Active
);

public sealed record SetActiveRequest(bool Active);
```

# 2. Divisional

## Endpoints

| Caso de uso | Método + endpoint | Request | Response | Entidades afectadas | Reglas principales |
|---|---|---|---|---|---|
| Listar divisionales | `GET /api/admin/divisions` | query filters | `DivisionSummaryDto[]` | `DIVISION` | filtros por gender/active |
| Obtener divisional | `GET /api/admin/divisions/{id}` | — | `DivisionDto` | `DIVISION` | 404 si no existe |
| Crear divisional | `POST /api/admin/divisions` | `CreateDivisionRequest` | `DivisionDto` | `DIVISION` | género válido; nivel > 0; unicidades |
| Modificar divisional | `PUT /api/admin/divisions/{id}` | `UpdateDivisionRequest` | `DivisionDto` | `DIVISION` | respetar `(name,gender)` y `(level_order,gender)` |
| Activar/desactivar | `PATCH /api/admin/divisions/{id}/active` | `SetActiveRequest` | `DivisionDto` | `DIVISION` | preferir desactivar a borrar |

## DTOs conceptuales

```csharp
public enum Gender
{
    Male,
    Female
}

public sealed record CreateDivisionRequest(
    string Name,
    short LevelOrder,
    Gender Gender
);

public sealed record UpdateDivisionRequest(
    string Name,
    short LevelOrder,
    Gender Gender
);

public sealed record DivisionDto(
    int DivisionId,
    string Name,
    short LevelOrder,
    Gender Gender,
    bool Active
);

public sealed record DivisionSummaryDto(
    int DivisionId,
    string Name,
    short LevelOrder,
    Gender Gender,
    bool Active
);
```

# 3. CompetitionFormat

## Regla de agregado

La API trata `CompetitionFormat` como agregado. No se exponen por defecto endpoints CRUD independientes para cada tabla `FORMAT_*`.

El agregado incluye:

- `COMPETITION_FORMAT`;
- `FORMAT_PHASE`;
- `FORMAT_GROUP`;
- `FORMAT_QUALIFICATION_RULE`;
- `FORMAT_PLAYOFF_SERIES`;
- `FORMAT_SERIES_PARTICIPANT_SOURCE`;
- `FORMAT_SCORING_RULE`;
- `FORMAT_TIEBREAK_RULE`;
- `FORMAT_MOVEMENT_RULE`.

## Endpoints

| Caso de uso | Método + endpoint | Request | Response | Entidades afectadas | Reglas principales |
|---|---|---|---|---|---|
| Listar formatos | `GET /api/admin/competition-formats` | query filters | `CompetitionFormatSummaryDto[]` | `COMPETITION_FORMAT` | filtros active/rango equipos |
| Obtener formato | `GET /api/admin/competition-formats/{id}` | — | `CompetitionFormatDto` | agregado FORMAT | devuelve definición completa |
| Crear formato | `POST /api/admin/competition-formats` | `CreateCompetitionFormatRequest` | `CompetitionFormatDto` | agregado FORMAT | transaccional; validar coherencia completa |
| Modificar formato | `PUT /api/admin/competition-formats/{id}` | `UpdateCompetitionFormatRequest` | `CompetitionFormatDto` | agregado FORMAT | cambios estructurales sólo si no está bloqueado |
| Clonar formato | `POST /api/admin/competition-formats/{id}/clone` | `CloneCompetitionFormatRequest` | `CompetitionFormatDto` | agregado FORMAT | nueva identidad/code/name; estructura independiente |
| Activar/desactivar | `PATCH /api/admin/competition-formats/{id}/active` | `SetActiveRequest` | `CompetitionFormatDto` | `COMPETITION_FORMAT` | no borrar historia |
| Validar definición | `POST /api/admin/competition-formats/validate` | `CompetitionFormatDefinitionDto` | `CompetitionFormatValidationDto` | ninguna | valida sin persistir |

## Referencias internas por code

En requests de creación/modificación del agregado, las referencias entre fases, grupos y series deben resolverse mediante `code` lógico y no mediante IDs persistentes que todavía no existen.

Ejemplos:

- `sourcePhaseCode: "REGULAR"`;
- `targetGroupCode: "CHAMPIONSHIP"`;
- `sourceSeriesCode: "SF1"`.

## DTO principal

```csharp
public sealed record CompetitionFormatDto(
    int CompetitionFormatId,
    string Code,
    string Name,
    string? Description,
    short MinTeams,
    short MaxTeams,
    bool Active,
    IReadOnlyList<FormatPhaseDto> Phases,
    IReadOnlyList<FormatQualificationRuleDto> QualificationRules,
    IReadOnlyList<FormatScoringRuleDto> ScoringRules,
    IReadOnlyList<FormatTiebreakRuleDto> TiebreakRules,
    IReadOnlyList<FormatMovementRuleDto> MovementRules
);

public sealed record CompetitionFormatSummaryDto(
    int CompetitionFormatId,
    string Code,
    string Name,
    short MinTeams,
    short MaxTeams,
    bool Active
);

public sealed record CreateCompetitionFormatRequest(
    string Code,
    string Name,
    string? Description,
    short MinTeams,
    short MaxTeams,
    CompetitionFormatDefinitionDto Definition
);

public sealed record CompetitionFormatDefinitionDto(
    IReadOnlyList<FormatPhaseInputDto> Phases,
    IReadOnlyList<FormatQualificationRuleInputDto> QualificationRules,
    IReadOnlyList<FormatScoringRuleInputDto> ScoringRules,
    IReadOnlyList<FormatTiebreakRuleInputDto> TiebreakRules,
    IReadOnlyList<FormatMovementRuleInputDto> MovementRules
);
```

## DTOs de fases y grupos

```csharp
public sealed record FormatPhaseInputDto(
    string Code,
    string Name,
    PhaseType PhaseType,
    PhaseRole PhaseRole,
    short Sequence,
    short? Rounds,
    FixtureMode? FixtureMode,
    IReadOnlyList<FormatGroupInputDto> Groups,
    IReadOnlyList<FormatPlayoffSeriesInputDto> Series
);

public sealed record FormatGroupInputDto(
    string Code,
    string Name,
    GroupRole GroupRole,
    short Sequence,
    short Rounds,
    FixtureMode FixtureMode,
    CarryOverMode CarryOverMode
);
```

La implementación debe tomar como fuente los valores admitidos por el modelo SQL vigente para `PhaseType`, `PhaseRole`, `FixtureMode`, `GroupRole` y `CarryOverMode`.

## DTOs de playoff y clasificación

```csharp
public sealed record FormatPlayoffSeriesInputDto(
    string Code,
    string Name,
    short Sequence,
    short WinsRequired,
    short Team1InitialWins,
    short Team2InitialWins,
    IReadOnlyList<SeriesParticipantSourceInputDto> ParticipantSources
);

public sealed record SeriesParticipantSourceInputDto(
    byte TargetSide,
    SeriesParticipantSourceType SourceType,
    string SourceSeriesCode
);

public sealed record FormatQualificationRuleInputDto(
    string SourcePhaseCode,
    string? SourceGroupCode,
    QualificationSelectionMode SelectionMode,
    short? FromPosition,
    short? ToPosition,
    QualificationTargetType TargetType,
    string TargetPhaseCode,
    string? TargetGroupCode,
    string? TargetSeriesCode,
    byte? TargetSide,
    short Sequence
);
```

## DTOs de puntuación, desempate y movimiento

```csharp
public sealed record FormatScoringRuleInputDto(
    byte WinnerSets,
    byte LoserSets,
    short WinnerTablePoints,
    short LoserTablePoints
);

public sealed record FormatTiebreakRuleInputDto(
    short Sequence,
    TiebreakCriterion Criterion,
    SortDirection SortDirection
);

public sealed record FormatMovementRuleInputDto(
    MovementType MovementType,
    MovementSourceType SourceType,
    string? SourcePhaseCode,
    string? SourceGroupCode,
    string? SourceSeriesCode,
    short FromPosition,
    short ToPosition,
    short TargetLevelDelta,
    bool AppliesIfTargetExists
);
```

## Inmutabilidad estructural

Un formato usado por una competición que ya dejó `DRAFT` se considera estructuralmente bloqueado.

Para crear una variante debe utilizarse:

`POST /api/admin/competition-formats/{id}/clone`

La clonación de formato es independiente de crear una competición basada en otra competición.

# 4. Competition

## Regla obligatoria

Toda `Competition` debe tener `Season`, `Divisional` y `CompetitionFormat`.

Los únicos modos técnicos de creación inicial son:

- `FROM_FORMAT`;
- `FROM_COMPETITION`.

## Endpoints

| Caso de uso | Método + endpoint | Request | Response | Entidades afectadas | Reglas principales |
|---|---|---|---|---|---|
| Listar competiciones | `GET /api/admin/competitions` | query filters | `CompetitionSummaryDto[]` | `COMPETITION` + maestras | filtros season/division/status |
| Obtener competición | `GET /api/admin/competitions/{id}` | — | `CompetitionDto` | `COMPETITION` + maestras | 404 si no existe |
| Crear competición | `POST /api/admin/competitions` | `CreateCompetitionRequest` | `CompetitionDto` | `COMPETITION` + estructura instanciada | transaccional |
| Modificar metadatos | `PUT /api/admin/competitions/{id}` | `UpdateCompetitionRequest` | `CompetitionDto` | `COMPETITION` | restricciones según estado |
| Cambiar estado | `PATCH /api/admin/competitions/{id}/status` | `ChangeCompetitionStatusRequest` | `CompetitionDto` | `COMPETITION` | sólo transición válida |
| Obtener estructura | `GET /api/admin/competitions/{id}/structure` | — | `CompetitionStructureDto` | fases/grupos/series | devuelve estructura instanciada |

No se incorpora un endpoint independiente `clone-structure`: el origen de estructura forma parte de `CreateCompetitionRequest`.

## CreateCompetitionRequest

```csharp
public enum CompetitionStructureSourceType
{
    Format,
    Competition
}

public sealed record CompetitionStructureSourceDto(
    CompetitionStructureSourceType Type,
    int? CompetitionFormatId,
    int? SourceCompetitionId
);

public sealed record CreateCompetitionRequest(
    string Name,
    int SeasonId,
    int DivisionId,
    CompetitionPeriodType PeriodType,
    DateOnly? StartDate,
    DateOnly? EndDate,
    CompetitionStructureSourceDto StructureSource
);
```

Regla XOR:

- `Type = Format` → `CompetitionFormatId` obligatorio y `SourceCompetitionId = null`;
- `Type = Competition` → `SourceCompetitionId` obligatorio y `CompetitionFormatId = null`.

## Comportamiento de creación

### Desde formato

1. validar Season;
2. validar Divisional;
3. validar CompetitionFormat;
4. crear `COMPETITION` en `DRAFT`;
5. instanciar `COMPETITION_PHASE`;
6. instanciar `PHASE_GROUP`;
7. instanciar `PLAYOFF_SERIES`;
8. instanciar fuentes de participantes de series;
9. no crear todavía equipos, fixture, partidos, resultados ni planteles.

### Basada en otra competición

1. validar la competición modelo;
2. obtener su `competition_format_id`;
3. reutilizar ese `CompetitionFormat`;
4. ejecutar la misma instanciación de estructura indicada para `FROM_FORMAT`.

No se copian datos operativos de la competición modelo.

## DTOs de consulta

```csharp
public sealed record CompetitionDto(
    int CompetitionId,
    string Name,
    SeasonSummaryDto Season,
    DivisionSummaryDto Division,
    CompetitionFormatSummaryDto Format,
    CompetitionPeriodType PeriodType,
    DateOnly? StartDate,
    DateOnly? EndDate,
    CompetitionStatus Status
);

public sealed record CompetitionSummaryDto(
    int CompetitionId,
    string Name,
    short SeasonYear,
    string DivisionName,
    Gender Gender,
    string FormatName,
    CompetitionPeriodType PeriodType,
    CompetitionStatus Status
);
```

## Estructura instanciada

```csharp
public sealed record CompetitionStructureDto(
    int CompetitionId,
    IReadOnlyList<CompetitionPhaseDto> Phases
);

public sealed record CompetitionPhaseDto(
    int PhaseId,
    string Code,
    string Name,
    PhaseType PhaseType,
    PhaseRole PhaseRole,
    short Sequence,
    short? Rounds,
    FixtureMode? FixtureMode,
    CompetitionPhaseStatus Status,
    IReadOnlyList<CompetitionPhaseGroupDto> Groups,
    IReadOnlyList<CompetitionPlayoffSeriesDto> Series
);
```

# 5. TeamEntry

`Team` representa la identidad permanente. `TeamEntry` representa la inscripción del equipo en una competición concreta.

## Endpoints

| Caso de uso | Método + endpoint | Request | Response | Entidades afectadas | Reglas principales |
|---|---|---|---|---|---|
| Listar participantes | `GET /api/admin/competitions/{id}/entries` | — | `TeamEntryDto[]` | `TEAM_ENTRY`, `TEAM` | — |
| Inscribir equipo | `POST /api/admin/competitions/{id}/entries` | `AddTeamEntryRequest` | `TeamEntryDto` | `TEAM_ENTRY` | equipo único dentro de Competition; no superar maxTeams |
| Cambiar seed | `PATCH /api/admin/competitions/{id}/entries/{entryId}/seed` | `SetTeamEntrySeedRequest` | `TeamEntryDto` | `TEAM_ENTRY` | seed válido |
| Cambiar estado | `PATCH /api/admin/competitions/{id}/entries/{entryId}/status` | `ChangeTeamEntryStatusRequest` | `TeamEntryDto` | `TEAM_ENTRY` | transición válida |
| Eliminar inscripción | `DELETE /api/admin/competitions/{id}/entries/{entryId}` | — | — | `TEAM_ENTRY` | sólo antes de quedar comprometida por fixture/estructura operativa |

## DTOs

```csharp
public sealed record AddTeamEntryRequest(
    int TeamId,
    short? Seed
);

public sealed record TeamEntryDto(
    int TeamEntryId,
    int TeamId,
    string TeamName,
    short? Seed,
    TeamEntryStatus Status
);
```

## Rango de equipos

Durante la carga:

- permitir estar temporalmente por debajo de `minTeams`;
- impedir superar `maxTeams`.

Antes de generar fixture:

`minTeams <= validTeamEntries <= maxTeams`.

# 6. Fixture / Scheduling inicial

## Principio

Las reglas estructurales del fixture pertenecen al formato/estructura instanciada. El frontend no debe volver a enviarlas.

## Endpoints

| Caso de uso | Método + endpoint | Request | Response | Entidades afectadas | Reglas principales |
|---|---|---|---|---|---|
| Generar fixture inicial | `POST /api/admin/competitions/{id}/fixture/generate` | `GenerateFixtureRequest` | `GenerateFixtureResponse` | `MATCH` y estructura aplicable | validar cantidad de equipos y estado |
| Consultar fixture | `GET /api/admin/competitions/{id}/fixture` | — | `CompetitionFixtureDto` | fases/grupos/matches | — |
| Consultar partido Admin | `GET /api/admin/matches/{matchId}` | — | `MatchAdminDto` | `MATCH` + relacionadas | — |
| Programar/reprogramar | `PUT /api/admin/matches/{matchId}/schedule` | `ScheduleMatchRequest` | `MatchAdminDto` | `MATCH` | validar estado y sede |

## GenerateFixtureRequest

```csharp
public sealed record GenerateFixtureRequest(
    int? RandomSeed
);
```

`RandomSeed` es opcional. Cuando el modo de fixture tenga comportamiento aleatorio/balanceado permite reproducibilidad en pruebas y auditoría.

## GenerateFixtureResponse

```csharp
public sealed record GenerateFixtureResponse(
    int CompetitionId,
    int MatchesCreated,
    int RandomSeed,
    IReadOnlyList<FixturePhaseDto> Phases
);
```

## Estrategia inicial de fases posteriores

No crear partidos de playoff con participantes nulos por defecto.

Generar partidos de una fase posterior cuando sus participantes hayan sido resueltos por las reglas de clasificación/progresión correspondientes.

Para formatos con grupos de segunda fase:

1. finalizar/resolver fase previa;
2. aplicar QualificationRules;
3. poblar `PHASE_GROUP_ENTRY`;
4. generar los partidos correspondientes a esos grupos.

La progresión automática/semiautomática completa entre fases se diseñará en una iteración posterior de Scheduling.

## DTOs de fixture

```csharp
public sealed record CompetitionFixtureDto(
    int CompetitionId,
    IReadOnlyList<FixturePhaseDto> Phases
);

public sealed record FixtureMatchDto(
    int MatchId,
    short? RoundNumber,
    short? MatchNumber,
    TeamEntrySummaryDto? HomeTeam,
    TeamEntrySummaryDto? AwayTeam,
    DateTimeOffset? MatchDate,
    VenueSummaryDto? Venue,
    MatchStatus Status
);

public sealed record ScheduleMatchRequest(
    DateTimeOffset? MatchDate,
    int? VenueId
);
```

# 7. Estados iniciales de Competition

Estados contemplados:

- `DRAFT`;
- `SCHEDULED`;
- `IN_PROGRESS`;
- `FINISHED`;
- `CANCELLED`.

Flujo base:

`DRAFT → SCHEDULED → IN_PROGRESS → FINISHED`

Cancelación inicial contemplada:

`DRAFT / SCHEDULED → CANCELLED`

La API no debe aceptar cambios arbitrarios de status. `ChangeCompetitionStatus` debe validar transiciones e invariantes.

Interpretación inicial:

- `DRAFT`: configuración, estructura y participantes en preparación;
- `SCHEDULED`: fixture inicial generado y competición preparada;
- `IN_PROGRESS`: la competición comenzó;
- `FINISHED`: fases requeridas resueltas y competición cerrada;
- `CANCELLED`: competición cancelada.

# 8. Consultas y paginación

Las listas deben soportar filtros relevantes mediante query string, por ejemplo:

`GET /api/admin/competitions?seasonId=3&divisionId=5&status=Draft`

No es obligatorio introducir paginación para catálogos pequeños como Season/Division en la primera versión. Para colecciones potencialmente grandes se podrá incorporar `page`/`pageSize` cuando el caso de uso lo requiera.

# 9. Catálogo inicial consolidado de Admin

```text
/api/admin

SEASONS
├─ GET    /seasons
├─ GET    /seasons/{id}
├─ POST   /seasons
├─ PUT    /seasons/{id}
└─ PATCH  /seasons/{id}/active

DIVISIONS
├─ GET    /divisions
├─ GET    /divisions/{id}
├─ POST   /divisions
├─ PUT    /divisions/{id}
└─ PATCH  /divisions/{id}/active

COMPETITION FORMATS
├─ GET    /competition-formats
├─ GET    /competition-formats/{id}
├─ POST   /competition-formats
├─ PUT    /competition-formats/{id}
├─ POST   /competition-formats/{id}/clone
├─ PATCH  /competition-formats/{id}/active
└─ POST   /competition-formats/validate

COMPETITIONS
├─ GET    /competitions
├─ GET    /competitions/{id}
├─ POST   /competitions
├─ PUT    /competitions/{id}
├─ PATCH  /competitions/{id}/status
└─ GET    /competitions/{id}/structure

TEAM ENTRIES
├─ GET    /competitions/{id}/entries
├─ POST   /competitions/{id}/entries
├─ PATCH  /competitions/{id}/entries/{entryId}/seed
├─ PATCH  /competitions/{id}/entries/{entryId}/status
└─ DELETE /competitions/{id}/entries/{entryId}

FIXTURE
├─ POST   /competitions/{id}/fixture/generate
├─ POST   /competitions/{id}/fixture/regenerate
├─ GET    /competitions/{id}/fixture
├─ GET    /matches/{matchId}
└─ PUT    /matches/{matchId}/schedule
```

# 10. Flujo administrativo soportado

```text
Crear Season / disponer de Divisional
        ↓
crear o seleccionar CompetitionFormat
        ↓
crear Competition (FROM_FORMAT o FROM_COMPETITION)
        ↓
instanciar fases + grupos + series
        ↓
inscribir Teams mediante TeamEntry
        ↓
validar min/max equipos
        ↓
generar fixture inicial
        ↓
programar fechas/sedes
        ↓
Competition → SCHEDULED
```

La progresión entre fases, clasificación automática, generación incremental de segunda fase/playoffs y cierre deportivo de Competition queda definida en las secciones siguientes.

## Standings administrativo

`GET /api/admin/competitions/{competitionId}/phases/{phaseId}/standings`

Para fases con grupos:

`GET /api/admin/competitions/{competitionId}/phases/{phaseId}/standings?phaseGroupId=12`

Standings se calcula bajo demanda desde partidos `FINISHED`, `MATCH_SET`,
`FORMAT_SCORING_RULE` y `FORMAT_TIEBREAK_RULE`; no se persiste una tabla derivada.
Incluye todos los participantes del ámbito aunque tengan cero partidos jugados.
En fases con grupos `phaseGroupId` es obligatorio y los participantes proceden
exclusivamente de `PHASE_GROUP_ENTRY`. No se admite para fases playoff.

El resultado informa estadísticas de partidos, sets y puntos, puntos de tabla,
posición compartida, empate deportivo irresuelto e `IsFinal`. Las inconsistencias
de resultados o configuración persistida se devuelven como `409 ProblemDetails`.

# 11. Progresión de fases

## Principio

La progresión se modela como caso de uso de Application y no como CRUD de estados ni como cascada implícita al cerrar el último partido.

Para fases de liga/grupos:

`resultados → standings → CompletePhase → QualificationRules → participantes posteriores → fixture incremental`

No se exponen endpoints técnicos independientes para `ApplyQualificationRules`, `PopulatePhaseGroupEntries`, `ResolveSeriesParticipants` o `GenerateNextSeriesMatch`. Son responsabilidades internas de los casos de uso de progresión.

## Endpoints

| Caso de uso | Método + endpoint | Request | Response | Entidades afectadas | Reglas principales |
|---|---|---|---|---|---|
| Previsualizar cierre de fase | `GET /api/admin/competitions/{competitionId}/phases/{phaseId}/completion-preview` | — | `PhaseCompletionPreviewDto` | ninguna | sin persistencia; devuelve blockers, standings y clasificados |
| Completar fase | `POST /api/admin/competitions/{competitionId}/phases/{phaseId}/complete` | sin body | `PhaseCompletionResultDto` | `COMPETITION_PHASE`, `PHASE_GROUP_ENTRY`, `PLAYOFF_SERIES`, `MATCH` | transaccional e idempotente |
| Consultar progresión | `GET /api/admin/competitions/{competitionId}/progression` | — | `CompetitionProgressionDto` | consulta | muestra avance de fases/partidos/series |

No se incorpora `PATCH /phases/{id}/status` para finalizar fases. Finalizar una fase tiene consecuencias de dominio y debe pasar por `CompletePhase`.

## Condiciones para completar una fase de liga/grupos

Una fase sólo puede completarse cuando todos sus partidos requeridos están resueltos.

Un `MATCH` `CANCELLED` no cuenta automáticamente como resuelto para esta validación porque su efecto deportivo requiere una regla explícita posterior.

Cuando la fase contiene varios grupos, v1 la completa como unidad: todos los grupos requeridos deben estar resueltos. No se define todavía `CompletePhaseGroup`.

## Semántica TOP_HALF / BOTTOM_HALF

Si `N` es par:

- `TOP_HALF = N / 2`;
- `BOTTOM_HALF = N / 2`.

Si `N` es impar:

- `TOP_HALF = (N + 1) / 2`;
- `BOTTOM_HALF = (N - 1) / 2`.

El participante adicional queda en Championship/mitad superior.

Ejemplos: 9 → 5/4; 10 → 5/5; 11 → 6/5; 12 → 6/6.

## CarryOverMode

En v1 sólo se admite operativamente `NONE`.

Los valores `ALL` y `QUALIFIED_ONLY` pueden permanecer en el modelo pero deben rechazarse hasta definir explícitamente su semántica deportiva.

## DTOs

```csharp
public sealed record PhaseCompletionBlockerDto(
    string Code,
    string Message,
    IReadOnlyList<int>? MatchIds,
    IReadOnlyList<int>? TeamEntryIds,
    int? QualificationRuleId
);

public sealed record QualificationPreviewDto(
    int QualificationRuleId,
    int TeamEntryId,
    string TeamName,
    int SourcePosition,
    string TargetPhaseCode,
    string? TargetGroupCode,
    string? TargetSeriesCode,
    byte? TargetSide
);

public sealed record PhaseCompletionPreviewDto(
    int CompetitionId,
    int PhaseId,
    string PhaseCode,
    string PhaseName,
    bool CanComplete,
    IReadOnlyList<PhaseCompletionBlockerDto> Blockers,
    IReadOnlyList<StandingsDto> Standings,
    IReadOnlyList<QualificationPreviewDto> Qualifications,
    IReadOnlyList<GeneratedFixtureSummaryDto> GeneratedFixtures,
    IReadOnlyList<ResolvedSeriesDto> ResolvedSeries
);

public sealed record QualifiedTeamDto(
    int QualificationRuleId,
    int TeamEntryId,
    string TeamName,
    int SourcePosition,
    string TargetPhaseCode,
    string? TargetGroupCode,
    string? TargetSeriesCode,
    byte? TargetSide
);

public sealed record GeneratedFixtureSummaryDto(
    int PhaseId,
    int? PhaseGroupId,
    int? SeriesId,
    int MatchesCreated
);

public sealed record ResolvedSeriesDto(
    int SeriesId,
    string SeriesCode,
    int? Team1EntryId,
    int? Team2EntryId,
    PlayoffSeriesStatus Status
);

public sealed record PhaseCompletionResultDto(
    int CompetitionId,
    int PhaseId,
    CompetitionPhaseStatus Status,
    bool AlreadyCompleted,
    IReadOnlyList<StandingsDto> Standings,
    IReadOnlyList<QualifiedTeamDto> Qualifications,
    IReadOnlyList<GeneratedFixtureSummaryDto> GeneratedFixtures,
    IReadOnlyList<ResolvedSeriesDto> ResolvedSeries
);
```

`CompletePhase` debe ser idempotente en sus efectos. Si la fase ya fue completada correctamente, un reintento puede devolver `200 OK` con `AlreadyCompleted = true`; nunca debe duplicar `PHASE_GROUP_ENTRY` ni `MATCH`.

`Standings` contiene una tabla independiente por cada grupo; nunca se combinan
grupos para producir una clasificación implícita. Una regla de una fase con
grupos debe indicar `sourceGroup`. El preview devuelve `200 OK` y
`CanComplete = false` para blockers operativos (partidos no resueltos,
cancelados o un empate que cruza una frontera de clasificación) y no proyecta
efectos persistibles mientras exista alguno.

Un empate compartido sólo bloquea cuando la selección `POSITION_RANGE`,
`TOP_HALF` o `BOTTOM_HALF` tendría que separar equipos empatados. La posición
deportiva original se conserva en `source_position`; `seed` permanece `null`.

El POST recalcula el mismo plan dentro de una transacción serializable protegida
contra ejecuciones concurrentes. Materializa `PHASE_GROUP_ENTRY`, participantes
de series y fixture incremental antes de marcar la fase `FINISHED`; cualquier
error revierte toda la operación. En v1 únicamente se admite
`CarryOverMode.NONE`.

Para grupos resueltos se reutiliza el generador round-robin existente. Para una
serie que pasa a `READY` se crea sólo su primer partido real, sin fecha ni sede
obligatorias. `GeneratedFixtureSummaryDto` distingue ambos scopes mediante
`PhaseGroupId` y `SeriesId`.

Respuestas principales:

- `200`: preview, cierre exitoso o repetición idempotente;
- `404`: Competition/Phase inexistente o fuera del scope;
- `409`: fase/tipo inválido, configuración inconsistente, carry-over no
  soportado, conflicto de efectos o POST con blockers (`phase_cannot_complete`).

# 12. Generación incremental de fixture

El fixture no se crea completo para todas las fases al inicio de la Competition.

Regla:

`generar partidos cuando sus participantes reales estén resueltos`

### Segunda fase

Al completar la fase previa:

1. obtener standings definitivos;
2. aplicar QualificationRules;
3. poblar `PHASE_GROUP_ENTRY`;
4. generar partidos para cada grupo según su configuración instanciada.

`source_position` conserva la posición de clasificación desde la fuente. `seed` es independiente.

### Playoffs

No crear por defecto `MATCH` con participantes nulos.

Cuando ambos participantes de una `PLAYOFF_SERIES` están resueltos:

1. la serie pasa de `PENDING` a `READY`;
2. se genera únicamente el siguiente partido real requerido.

Después de cada partido finalizado:

`wins = initialWins + realMatchWins`

Si un participante alcanza `winsRequired`, la serie termina. En caso contrario se genera el siguiente partido.

Esto permite que una semifinal con ventaja 1-0 y `winsRequired = 2` genere un segundo partido sólo si realmente es necesario.

### Motor automático PlayoffSeries Progression

La progresión de una serie no expone `CompletePlayoffSeries` ni un endpoint
administrativo de avance. El caso de uso de Application
`ProcessFinishedPlayoffMatch` será invocado por el futuro flujo que cierre
definitivamente un partido, especialmente el cierre del Scorer.

Después de cada `MATCH` `FINISHED` perteneciente a una serie se recalcula:

```text
seriesWins = initialWins + realMatchWins
```

No se persiste un contador mutable de victorias. Sólo cuentan partidos
`FINISHED` con un ganador válido perteneciente a la serie. Si nadie alcanzó
`winsRequired`, la serie queda `IN_PROGRESS` y se garantiza únicamente el
siguiente partido real.

La numeración es `1, 2, 3, ...` y está protegida por la restricción única
`(series_id, match_number)`. La localía v1 alterna determinísticamente:

- partido impar: Team1 local, Team2 visitante;
- partido par: Team2 local, Team1 visitante.

Los partidos incrementales nacen `PENDING`, sin fecha ni sede. Al alcanzar
`winsRequired`, se persiste `winner_team_entry_id`, el perdedor se deriva del
otro participante y se propagan todas las fuentes `SERIES_WINNER` y
`SERIES_LOSER`. Un destino con un solo lado permanece `PENDING`; con ambos pasa
a `READY` y recibe exactamente su primer partido.

El procesamiento es transaccional, idempotente y serializado por Competition.
Esto protege tanto dos reintentos del mismo partido como semifinales concurrentes
que escriben lados diferentes de Final/Tercer Puesto. No se sobrescriben lados
ocupados y los conflictos de participantes o partidos se reportan mediante
códigos de Application estables `playoff_series_*`.

Cuando todas las series obligatorias de una fase playoff están `FINISHED`, la
fase pasa automáticamente a `FINISHED`. La Competition no se finaliza: continúa
requiriendo el futuro `CompleteCompetition`. Series o partidos `CANCELLED` no
producen ganador ni progresión automática.

# 13. Final y tercer puesto

Semifinal, Final y Tercer Puesto se modelan siempre como `PLAYOFF_SERIES`.

Partido único:

```text
winsRequired = 1
team1InitialWins = 0
team2InitialWins = 0
```

Serie al mejor de tres:

```text
winsRequired = 2
```

La ventaja deportiva se representa mediante `initialWins`, nunca mediante partidos ficticios.

Las fuentes de participantes de Final y Third Place se resuelven automáticamente al finalizar las semifinales:

- `SERIES_WINNER`;
- `SERIES_LOSER`.

No importa cuál semifinal termine primero. Una serie destino queda `PENDING` hasta tener ambos participantes y pasa a `READY` cuando los dos están resueltos.

# 14. Estados de series y fases

## PLAYOFF_SERIES

- `PENDING`: faltan participantes;
- `READY`: participantes completos, sin partido iniciado;
- `IN_PROGRESS`: serie iniciada sin ganador;
- `FINISHED`: un participante alcanzó `winsRequired`;
- `CANCELLED`: cancelada administrativamente.

La progresión entre series se produce automáticamente cuando una serie queda `FINISHED`.

## COMPETITION_PHASE

Para fases de liga/grupos:

`PENDING → IN_PROGRESS → FINISHED`

- `PENDING → IN_PROGRESS`: al comenzar el primer partido de la fase;
- `IN_PROGRESS → FINISHED`: mediante `CompletePhase`.

Para fases playoff, el estado puede derivarse del avance de sus series y quedar `FINISHED` cuando todas las series requeridas estén `FINISHED`.

# 15. Progresión y cierre de Competition

## SCHEDULED → IN_PROGRESS

La Competition pasa automáticamente de `SCHEDULED` a `IN_PROGRESS` cuando comienza el primer partido oficial.

No se cambia por fecha de calendario ni mediante un botón administrativo genérico.

## Cierre explícito

Se incorporan:

| Caso de uso | Método + endpoint | Request | Response |
|---|---|---|---|
| Previsualizar cierre | `GET /api/admin/competitions/{competitionId}/completion-preview` | — | `CompetitionCompletionPreviewDto` |
| Completar Competition | `POST /api/admin/competitions/{competitionId}/complete` | sin body | `CompetitionCompletionResultDto` |

La Competition no pasa automáticamente a `FINISHED` al terminar la Final.

`CompleteCompetition` debe validar que todas las fases y series obligatorias estén resueltas.

Las `FORMAT_MOVEMENT_RULE` pueden producir un resultado calculado de promoción/relegación en el preview/cierre, pero en v1 no crean automáticamente `TeamEntry` en futuras competiciones.

DTOs conceptuales:

```csharp
public sealed record MatchProgressDto(
    int Total,
    int Pending,
    int InProgress,
    int Finished,
    int Cancelled
);

public sealed record CompetitionPhaseProgressDto(
    int PhaseId,
    string Code,
    string PhaseName,
    short Sequence,
    PhaseType PhaseType,
    CompetitionPhaseStatus Status,
    MatchProgressDto Matches,
    IReadOnlyList<CompetitionGroupProgressDto> Groups,
    IReadOnlyList<PlayoffSeriesProgressDto> Series
);

public sealed record CompetitionGroupProgressDto(
    int PhaseGroupId,
    string Code,
    string Name,
    MatchProgressDto Matches
);

public sealed record PlayoffSeriesProgressDto(
    int SeriesId,
    string Code,
    string Name,
    PlayoffSeriesStatus Status,
    TeamEntrySummaryDto? Team1,
    TeamEntrySummaryDto? Team2,
    short Team1InitialWins,
    short Team2InitialWins,
    int Team1RealWins,
    int Team2RealWins,
    int Team1Wins,
    int Team2Wins,
    short WinsRequired,
    int? WinnerTeamEntryId,
    MatchProgressDto Matches
);

public sealed record CompetitionProgressionDto(
    int CompetitionId,
    string CompetitionName,
    CompetitionStatus Status,
    MatchProgressDto Matches,
    IReadOnlyList<CompetitionPhaseProgressDto> Phases
);

public sealed record CompetitionCompletionPreviewDto(
    int CompetitionId,
    string CompetitionName,
    CompetitionStatus Status,
    bool AlreadyCompleted,
    bool CanComplete,
    IReadOnlyList<CompetitionCompletionBlockerDto> Blockers,
    IReadOnlyList<MovementResultDto> Movements
);

public sealed record CompetitionCompletionBlockerDto(
    string Code,
    string Message,
    int? PhaseId = null,
    int? SeriesId = null,
    int? MatchId = null,
    int? MovementRuleId = null
);

public sealed record MovementResultDto(
    int MovementRuleId,
    MovementType MovementType,
    MovementSourceDto Source,
    int TeamEntryId,
    int TeamId,
    string TeamName,
    int SourcePosition,
    int? StandingPosition,
    int SourceDivisionId,
    string SourceDivisionName,
    short SourceDivisionLevelOrder,
    MovementResultStatus Status,
    int? TargetDivisionId,
    string? TargetDivisionName,
    short? TargetDivisionLevelOrder,
    short TargetLevelDelta,
    MovementNotAppliedReason? NotAppliedReason
);

public sealed record CompetitionCompletionResultDto(
    int CompetitionId,
    CompetitionStatus Status,
    bool AlreadyCompleted,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<MovementResultDto> Movements
);
```

`GET /progression` informa únicamente estado deportivo y contadores derivados.
`Total` representa partidos materializados actualmente; `PENDING` incluye
`PENDING/SCHEDULED` e `IN_PROGRESS` incluye `IN_PROGRESS/SUSPENDED`, de modo que
siempre se cumple `Total = Pending + InProgress + Finished + Cancelled`.

El preview devuelve blockers operativos con `200 OK`. Estados o configuraciones
persistidas imposibles producen `409` con códigos `competition_completion_*`.
El POST devuelve `409 competition_cannot_complete` y los blockers estructurados
si el estado cambió o sigue incompleto.

Los movimientos se calculan por equipo desde `FORMAT_MOVEMENT_RULE`:

- `PHASE_POSITION` y `GROUP_POSITION`: posición absoluta de standings;
- `SERIES_RESULT`: `1 = winner`, `2 = loser`;
- `PHASE_LAST_N` y `GROUP_LAST_N`: posición relativa desde el final, conservando
  además `StandingPosition` real.

La Division destino debe tener exactamente
`SourceDivision.LevelOrder + TargetLevelDelta` y el mismo género; no se saltan
niveles. Si falta y `AppliesIfTargetExists=true`, el resultado queda
`NOT_APPLICABLE`; en caso contrario aparece `movement_target_division_missing`.
V1 no persiste movimientos ni crea futuras `TeamEntry`.

El primer cierre cambia únicamente `Competition.Status` a `FINISHED` y persiste
`CompletedAt = UtcNow`. Reintentos conservan el timestamp y devuelven
`AlreadyCompleted=true`. Preview y POST comparten evaluador; el POST relee y
serializa por Competition dentro de una transacción. `PATCH /status` nunca puede
establecer `FINISHED`.

# 16. Regeneración controlada de fixture

Se incorpora:

`POST /api/admin/competitions/{competitionId}/fixture/regenerate`

Regla conservadora v1:

- puede regenerarse el fixture del ámbito soportado mientras ningún partido de ese ámbito esté `IN_PROGRESS` o `FINISHED`;
- si existe un partido iniciado/finalizado, responder conflicto de dominio;
- la regeneración reemplaza emparejamientos generados;
- fecha/hora/sede previamente programadas pueden quedar invalidadas y perderse;
- el frontend debe advertir al administrador cuando la regeneración afectará programación existente.

La generación de emparejamientos y la programación de fecha/sede siguen siendo responsabilidades separadas.

# 17. Catálogo Admin adicional de Scheduling avanzado

```text
PHASE PROGRESSION
├─ GET  /competitions/{competitionId}/phases/{phaseId}/completion-preview
└─ POST /competitions/{competitionId}/phases/{phaseId}/complete

COMPETITION PROGRESSION
└─ GET  /competitions/{competitionId}/progression

COMPETITION COMPLETION
├─ GET  /competitions/{competitionId}/completion-preview
└─ POST /competitions/{competitionId}/complete

FIXTURE
└─ POST /competitions/{competitionId}/fixture/regenerate
```

# 18. Flujo deportivo soportado

# 19. People Admin

# 20. Match Officials

Admin: `GET/POST /api/admin/matches/{matchId}/officials` y `PUT/DELETE /api/admin/matches/{matchId}/officials/{matchOfficialId}`. Scorer: `PUT /api/scorer/matches/{matchId}/officials/{role}` para reemplazo durante `IN_PROGRESS`. Requests usan `refereeId` y roles expresivos; responses incluyen identidad proyectada de Person y `HealthCardStatus`. Se documentan respuestas 400, 404 y 409 mediante ProblemDetails con `code` estable.

People y documentos se exponen bajo `/api/admin/people`; perfiles y listados bajo
`/players`, `/coaches` y `/referees`. Los perfiles se crean con
`POST /people/{personId}/{player|coach|referee}` sin body. No existe DELETE.

Los listados son paginados (`page`, `pageSize`, máximo 100). Los enums HTTP son
`HealthCard`, `LeagueCard`, `Valid`, `Missing`, `Expired` y `ValidityUnknown`.
Request inválido devuelve 400, inexistencia 404 y unicidad 409 con ProblemDetails.

```text
Competition DRAFT
        ↓
TeamEntry + fixture inicial
        ↓
Competition SCHEDULED
        ↓
primer partido comienza
        ↓
Competition IN_PROGRESS
        ↓
fase regular/grupos
        ↓
completion-preview
        ↓
CompletePhase
        ↓
Standings + QualificationRules
        ↓
PHASE_GROUP_ENTRY / participantes de series
        ↓
fixture incremental
        ↓
PlayoffSeriesResolver
        ↓
Final / Third Place
        ↓
completion-preview de Competition
        ↓
CompleteCompetition
        ↓
Competition FINISHED
```
