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

`MATCH_OFFICIAL` vincula un `MATCH` con las personas que actúan como árbitros/oficiales en ese encuentro y con el rol correspondiente.
