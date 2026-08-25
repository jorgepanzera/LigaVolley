# 04 — Personas y planteles

## Modelo acordado

Entidades persistentes principales:

- `PERSON`
- `PLAYER`
- `COACH`
- `REFEREE`
- `PLAYER_ROLE`
- `COMPETITION_ROSTER`
- `COMPETITION_ROSTER_PLAYER`
- `COMPETITION_ROSTER_STAFF`
- `MATCH_OFFICIAL`

Se reutilizan:

- `TEAM_ENTRY`
- `MATCH`

## PERSON como raíz

`PERSON` contiene la identidad común de una persona.

`PLAYER`, `COACH` y `REFEREE` representan roles/capacidades deportivas que esa persona puede tener. Una misma `PERSON` puede tener simultáneamente cero o un registro en cada una de esas tres entidades. En esta etapa no se modelan vigencias temporales ni exclusividad entre roles, salvo que aparezca un requisito explícito que lo justifique.

## Plantel por competición

`COMPETITION_ROSTER` representa el plantel de un equipo inscrito (`TEAM_ENTRY`) para una competición concreta.

Sus integrantes se separan en:

- `COMPETITION_ROSTER_PLAYER`;
- `COMPETITION_ROSTER_STAFF`.

El plantel no debe estar embebido directamente en Team porque puede variar entre competiciones/temporadas.

## PLAYER_ROLE

Representa el rol deportivo del jugador dentro de un contexto competitivo/plantel, no una clasificación global e inmutable de `PERSON` o `PLAYER`. Permite, entre otros usos, identificar al líbero sin convertirlo en una entidad de persona distinta. El rol puede variar entre competiciones/planteles.

## Oficiales del partido

## Alcance implementado: People v1

- Person nace activa y no existe DELETE físico.
- Health Card y League Card admiten historial; no existe entidad League en v1.
- Health Card es warning para Player y Referee, nunca bloqueo; Coach no la requiere.
- El documento relevante activo se elige por `valid_to`, `valid_from` e ID más altos.
- Crear Person no crea perfiles; crear perfil exige una Person existente.

## Alcance implementado: Competition Rosters v1

Cada `TEAM_ENTRY` admite como máximo un `COMPETITION_ROSTER`, creado explícitamente en `DRAFT`. El flujo administrativo es `DRAFT -> ACTIVE -> CLOSED`; no hay mínimos para activar y `ACTIVE` continúa editable mientras la Competition sea operativa. `CLOSED` conserva su consulta histórica y rechaza toda modificación.

Se admiten hasta 15 jugadores `ACTIVE`, dos técnicos `ACTIVE` y dos jugadores `ACTIVE` con rol `LIBERO`. Los miembros `INACTIVE` se conservan, no cuentan para los máximos y se reactivan sobre la misma inscripción. Player y Coach son únicos por roster; el dorsal es contextual y único entre jugadores activos. No se infiere rango reglamentario para el dorsal.

`PLAYER_ROLE` es contextual y admite `SETTER`, `OUTSIDE_HITTER`, `MIDDLE_BLOCKER`, `OPPOSITE` y `LIBERO`. Health Card se deriva con la lógica de People y es únicamente una advertencia, nunca bloquea una operación de roster. No existe DELETE físico ni publicación pública de planteles en v1.

Las mutaciones se serializan mediante bloqueo de `TEAM_ENTRY`; las unicidades naturales también están respaldadas por índices SQL.

## Alcance implementado: Match Officials v1

`MATCH_OFFICIAL` vincula un `MATCH` exclusivamente con un perfil `REFEREE`. Admite `FIRST_REFEREE`, `SECOND_REFEREE` y `SCORER`, con máximo uno por rol y sin repetir el mismo Referee en otro rol del partido. Health Card se deriva desde People, se muestra como warning y nunca bloquea.

Admin puede crear, modificar y eliminar designaciones en `PENDING` o `SCHEDULED`. Desde `IN_PROGRESS` Admin queda bloqueado; Scorer puede reemplazar una asignación existente, pero no vaciarla. `FINISHED`, `CANCELLED` y `SUSPENDED` son de consulta para este slice.

Al abrir el acta, el Scorer selecciona únicamente miembros ACTIVE de cada roster ACTIVE. Esa selección se copia a `MATCH_PLAYER`/`MATCH_TEAM_STAFF`; cambios posteriores del roster no alteran el acta ya materializada. Health Card continúa siendo warning.
