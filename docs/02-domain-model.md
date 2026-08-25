# 02 — Modelo de dominio consolidado

## Bloques principales

El modelo se divide conceptualmente en:

1. estructura deportiva;
2. competiciones y formatos;
3. personas y planteles;
4. partidos;
5. acta electrónica y estado en vivo.

## Entidades base ya consideradas

Entre las entidades del dominio base se encuentran conceptos equivalentes a:

- Club
- Team
- Venue
- Season
- Divisional
- Competition
- TeamEntry
- Match

`Venue` representa la sede/cancha donde se disputa un partido y permite desacoplar el encuentro de la identidad de los clubes/equipos.

## Competición

`Season` representa la temporada deportiva (por ejemplo, 2026). `Divisional` representa la categoría/división competitiva (por ejemplo, B Femenina). Ambas tienen identidad propia y son entidades maestras reutilizables.

Una `Competition` pertenece obligatoriamente a una `Season` y a una `Divisional`, y se configura con un formato estructurado. Ejemplo: “Apertura B Femenina 2026” referencia la temporada 2026 y la divisional B Femenina.

Los equipos que participan efectivamente en una competición se representan mediante `TEAM_ENTRY` o su equivalente persistente acordado. No confundir la identidad permanente de un equipo con su inscripción en una competición concreta.

## Partido

`MATCH` es la entidad reutilizada por fixture y acta electrónica. El partido en vivo agrega información operacional sin crear una identidad paralela del encuentro.

## Principio de identidad

## People v1

`PERSON` es la única raíz de identidad física. Su documento opcional es único
por `(document_type, document_number)`. `PLAYER`, `COACH` y `REFEREE` son perfiles
1:1 opcionales, sin vigencias temporales, que pueden coexistir.

`PERSON_ADDITIONAL_DOCUMENT` conserva múltiples `HEALTH_CARD` y `LEAGUE_CARD`.
El `HealthCardStatus` se deriva al consultar y nunca se persiste.

Separar entidades permanentes (por ejemplo Team o Person) de su participación contextual (por ejemplo TeamEntry, CompetitionRoster, MatchOfficial).

`COMPETITION_ROSTER` es la participación contextual única de un `TEAM_ENTRY`. Sus jugadores y técnicos conservan historia mediante estados `ACTIVE/INACTIVE`; el roster usa `DRAFT/ACTIVE/CLOSED`. El rol y dorsal del jugador pertenecen a esa inscripción competitiva, no a `PLAYER`.
