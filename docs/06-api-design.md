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
- programación administrativa de partidos.

## Superficies

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

La progresión entre fases, clasificación automática, generación de segunda fase/playoffs y cierre deportivo completo se detallará como siguiente bloque de Scheduling antes de implementarlo.
