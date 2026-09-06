# 02 — Modelo de dominio

## Agregados principales

- **Competition** pertenece a una `Season`, una `Division` y un `CompetitionFormat`. Materializa fases, grupos y series sin duplicar el formato.
- **TeamEntry** representa la participación de un equipo en una competición. `CompetitionRoster` es su plantel explícito.
- **Match** pertenece al fixture y resuelve HOME/AWAY desde los TeamEntry. `MatchSheet` es su acta única y operacional.
- **Person** es la raíz común de `Player`, `Coach` y `Referee`; los tres perfiles son opcionales e independientes.

## Plantel y partido

`CompetitionRosterPlayer.Role` representa una función habitual nullable dentro del roster. Sus códigos son `SETTER`, `OUTSIDE_HITTER`, `MIDDLE_BLOCKER`, `OPPOSITE` y `LIBERO`; no es una autorización deportiva ni un rol táctico de partido.

Al abrir el acta, `MatchTeam` congela HOME/AWAY, `MatchPlayer` congela convocatoria, dorsal y capitanía, y `MatchTeamStaff` congela el cuerpo técnico. `MatchLibero` congela la declaración reglamentaria de líbero para ese Match; no se deduce de la función habitual ni se modifica por cambios posteriores del roster.

`MatchSet`, `MatchLineup` y `MatchLineupPosition` representan el set y las posiciones lógicas P1..P6. `MatchSubstitution`, `MatchLiberoReplacement` y `MatchTimeout` son proyecciones operacionales con eventos de trazabilidad en `MatchEvent`; el sistema no adopta event sourcing.

## Estado deportivo

La cancha efectiva combina la alineación regular, sustituciones, offset de rotación y la cobertura observada de líbero. Las entidades operacionales permiten conocer marcador, saque, servidor, rotación, seis jugadoras efectivas, timeouts e historial de correcciones.

Las reglas efectivas se congelan como `RulesSnapshot` al abrir el acta. Esto incluye límites de sustituciones y timeouts, habilitación y máximo de líberos, saque de líbero y versión de protocolo. Las actas históricas conservan su snapshot y no se reinterpretan.

## Reglas de identidad e historial

Los maestros y planteles no tienen DELETE físico en los alcances vigentes. Las relaciones competitivas conservan historia, y los cambios posteriores en roster, personas o formato no reescriben la convocatoria ni el estado de un MatchSheet materializado.

Para estructura de competición, consultar [03 — Formatos y competiciones](03-competition-formats.md). Para personas y planteles, consultar [04 — Personas y planteles](04-people-and-rosters.md). Para operación de partido, consultar [05 — Acta electrónica y Scorer](05-match-scorer.md).
