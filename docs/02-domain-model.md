# 02 — Modelo de dominio consolidado

## Bloques principales

El modelo se divide conceptualmente en:

1. estructura deportiva;
2. competiciones y formatos;
3. personas y planteles;
4. partidos;
5. acta electrónica y estado en vivo.

## Proyección pública y frescura operacional

`MATCH_SHEET.last_operational_update_at` conserva el instante generado por el servidor en que se persistió la última mutación deportiva observable. Se actualiza en la misma transacción de mutaciones online o eventos nuevos de sync, nunca por GET, polling, reintentos idempotentes, UUID conocidos o requests rechazados. Puede ser NULL para actas históricas hasta su próxima mutación. La cancha pública deriva los seis jugadores efectivos mediante el calculador canónico; sólo publica P1..P6, dorsal, display name e indicador de líbero.

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

## Match Officials v1

`MATCH_OFFICIAL` representa la asignación vigente de un perfil `REFEREE` a un `MATCH`. No duplica `competition_id` ni datos de `PERSON`. Los roles cerrados son `FIRST_REFEREE`, `SECOND_REFEREE` y `SCORER`; cada rol y cada Referee son únicos dentro del Match.

## MatchSheet Opening v1

`MATCH` conserva la identidad del fixture y `MATCH_SHEET` representa su acta operacional única. `MATCH_TEAM` materializa HOME/AWAY desde los TeamEntry del Match; `MATCH_PLAYER` y `MATCH_TEAM_STAFF` congelan la convocatoria seleccionada desde el roster; `MATCH_LIBERO` declara hasta dos líberos sin modelar todavía su presencia en cancha. `MATCH_SHEET_SESSION` identifica la sesión activa y `MATCH_SHEET_AUDIT` registra `MATCH_SHEET_OPENED`.

`COMPETITION_ROSTER` es la participación contextual única de un `TEAM_ENTRY`. Sus jugadores y técnicos conservan historia mediante estados `ACTIVE/INACTIVE`; el roster usa `DRAFT/ACTIVE/CLOSED`. El rol y dorsal del jugador pertenecen a esa inscripción competitiva, no a `PLAYER`.

## Electronic Scoresheet Match Engine v1

`MATCH_SET` se reutiliza como estado operacional y resultado consumido por standings. Un set del Scorer referencia `MATCH_SHEET`, posee UUID, número 1..5, READY/IN_PROGRESS/FINISHED, puntos, ganador, saque, offsets y timestamps. Los resultados históricos previos conservan compatibilidad mediante el vínculo nullable; toda creación del motor nuevo exige MatchSheet.

`MATCH_LINEUP` y `MATCH_LINEUP_POSITION` congelan P1..P6 por lado y set. `MATCH_EVENT` ordena trazabilidad mediante UUID y `SequenceNumber` monotónico. `MATCH_SUBSTITUTION`, `MATCH_LIBERO_REPLACEMENT` y `MATCH_TIMEOUT` conservan el detalle operacional sin convertir el sistema en event sourcing.

La cancha efectiva se deriva centralmente como alineación inicial + sustituciones + `rotation_offset` + reemplazo activo de líbero. El servidor es siempre el jugador regular vigente en P1 del lado que posee el saque; el líbero nunca se convierte en servidor.

`MATCH_SET_LIBERO_PLAN` guarda, por lado y set, el líbero elegido y una máscara de plazas lógicas cubiertas. El plan se configura en READY y se rechaza si alguna combinación de rotación/saque pudiera requerir dos reemplazos simultáneos. P5/P6 son elegibles; P1 sólo lo es cuando el equipo recibe. P2/P3/P4 restauran automáticamente al regular vigente de la plaza.
# Sesiones de Scorer y sincronización

`MATCH_SHEET_SESSION` admite ACTIVE, ABANDONED y CLOSED, conserva `LastAcceptedSequence` y tiene como máximo una fila ACTIVE por MatchSheet. `MATCH_EVENT` mantiene su secuencia global y puede vincularse a la sesión con una secuencia local y hash del payload sincronizado. `MATCH_SHEET_AUDIT` registra `MATCH_SHEET_TAKEN_OVER` con sesión/dispositivo anterior y nuevo.
