# 02 — Modelo de dominio consolidado

## Admin Master Data v1

MCLUBM representa la institución estable y posee cero o un logo institucional actual. SQL Server conserva únicamente Mlogo_storage_keyM, Mlogo_content_typeM y Mlogo_versionM; el binario reside en storage de Infrastructure. MTEAMM pertenece obligatoriamente a un Club y no cambia de Club luego del alta. TeamEntry continúa siendo la participación contextual. No existen columnas de logo en Team, TeamEntry o Match. MVENUEM permanece independiente de Club y Team.

## Bloques principales

El modelo se divide conceptualmente en:

1. estructura deportiva;
2. competiciones y formatos;
3. personas y planteles;
4. partidos;
5. acta electrónica y estado en vivo.

## Proyección pública y frescura operacional

MMATCH_SHEET.last_operational_update_atM conserva el instante generado por el servidor en que se persistió la última mutación deportiva observable. Se actualiza en la misma transacción de mutaciones online o eventos nuevos de sync, nunca por GET, polling, reintentos idempotentes, UUID conocidos o requests rechazados. Puede ser NULL para actas históricas hasta su próxima mutación. La cancha pública deriva los seis jugadores efectivos mediante el calculador canónico; sólo publica P1..P6, dorsal, display name e indicador de líbero.

Public Live UX/UI v2 agrega únicamente una proyección nullable MservingPlayerM (dorsal y display name), usando el servidor regular canónico del backend. No introduce entidades, columnas ni estado derivado persistido. Véase [contrato y presentación](08-public-live-ux-ui-v2.md).

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

MVenueM representa la sede/cancha donde se disputa un partido y permite desacoplar el encuentro de la identidad de los clubes/equipos.

## Competición

MSeasonM representa la temporada deportiva (por ejemplo, 2026). MDivisionalM representa la categoría/división competitiva (por ejemplo, B Femenina). Ambas tienen identidad propia y son entidades maestras reutilizables.

Una MCompetitionM pertenece obligatoriamente a una MSeasonM y a una MDivisionalM, y se configura con un formato estructurado. Ejemplo: “Apertura B Femenina 2026” referencia la temporada 2026 y la divisional B Femenina.

Los equipos que participan efectivamente en una competición se representan mediante MTEAM_ENTRYM o su equivalente persistente acordado. No confundir la identidad permanente de un equipo con su inscripción en una competición concreta.

## Partido

MMATCHM es la entidad reutilizada por fixture y acta electrónica. El partido en vivo agrega información operacional sin crear una identidad paralela del encuentro.

## Principio de identidad y People v1

MPERSONM es la única raíz de identidad física. Su documento opcional es único
por M(document_type, document_number)M. MPLAYERM, MCOACHM y MREFEREEM son perfiles
1:1 opcionales, sin vigencias temporales, que pueden coexistir.

MPERSON_ADDITIONAL_DOCUMENTM conserva múltiples MHEALTH_CARDM y MLEAGUE_CARDM.
El MHealthCardStatusM se deriva al consultar y nunca se persiste.

Separar entidades permanentes (por ejemplo Team o Person) de su participación contextual (por ejemplo TeamEntry, CompetitionRoster, MatchOfficial).

## Match Officials v1

MMATCH_OFFICIALM representa la asignación vigente de un perfil MREFEREEM a un MMATCHM. No duplica Mcompetition_idM ni datos de MPERSONM. Los roles cerrados son MFIRST_REFEREEM, MSECOND_REFEREEM y MSCORERM; cada rol y cada Referee son únicos dentro del Match.

## MatchSheet Opening v1

MMATCHM conserva la identidad del fixture y MMATCH_SHEETM representa su acta operacional única. MMATCH_TEAMM materializa HOME/AWAY desde los TeamEntry del Match; MMATCH_PLAYERM y MMATCH_TEAM_STAFFM congelan la convocatoria seleccionada desde el roster; MMATCH_LIBEROM declara hasta dos líberos sin modelar todavía su presencia en cancha. MMATCH_SHEET_SESSIONM identifica la sesión activa y MMATCH_SHEET_AUDITM registra MMATCH_SHEET_OPENEDM.

MCOMPETITION_ROSTERM es la participación contextual única de un MTEAM_ENTRYM. Sus jugadores y técnicos conservan historia mediante estados MACTIVE/INACTIVEM; el roster usa MDRAFT/ACTIVE/CLOSEDM. El rol del jugador pertenece a esa inscripción competitiva; dorsal y capitanía pertenecen a MMATCH_PLAYERM.

## Electronic Scoresheet Match Engine v1

MMATCH_SETM se reutiliza como estado operacional y resultado consumido por standings. Un set del Scorer referencia MMATCH_SHEETM, posee UUID, número 1..5, READY/IN_PROGRESS/FINISHED, puntos, ganador, saque, offsets y timestamps. Los resultados históricos previos conservan compatibilidad mediante el vínculo nullable; toda creación del motor nuevo exige MatchSheet.

MMATCH_LINEUPM y MMATCH_LINEUP_POSITIONM congelan P1..P6 por lado y set. MMATCH_EVENTM ordena trazabilidad mediante UUID y MSequenceNumberM monotónico. MMATCH_SUBSTITUTIONM, MMATCH_LIBERO_REPLACEMENTM y MMATCH_TIMEOUTM conservan el detalle operacional sin convertir el sistema en event sourcing.

La cancha efectiva se deriva centralmente como alineación inicial + sustituciones + Mrotation_offsetM + reemplazo activo de líbero. El servidor es siempre el jugador regular vigente en P1 del lado que posee el saque; el líbero nunca se convierte en servidor.

MMATCH_SET_LIBERO_PLANM guarda, por lado y set, el líbero elegido y una máscara de plazas lógicas cubiertas. El plan opcional se configura en READY como preferencias de sugerencias. P5/P6 y P1 receptor son candidatos habituales. Ninguna posición retira ni inserta al líbero automáticamente; se requiere una acción observada.

## Sesiones de Scorer y sincronización

MMATCH_SHEET_SESSIONM admite ACTIVE, ABANDONED y CLOSED, conserva MLastAcceptedSequenceM y tiene como máximo una fila ACTIVE por MatchSheet. MMATCH_EVENTM mantiene su secuencia global y puede vincularse a la sesión con una secuencia local y hash del payload sincronizado. MMATCH_SHEET_AUDITM registra MMATCH_SHEET_TAKEN_OVERM con sesión/dispositivo anterior y nuevo.

## Proyecciones administrativas del partido

Match Readiness es una evaluación sin persistencia que reutiliza las precondiciones comunes de OpenMatchSheet. Admin MatchSheet Oversight proyecta MatchSheet, sesión relevante y estado operacional resumido con cancha efectiva, regular subyacente e historial de reemplazos centrales, sin estado offline local. No agrega tablas ni estado de dominio.

## Match-specific jersey number and captain

MCompetitionRosterPlayerM conserva la habilitación competitiva y el rol contextual. El dorsal y la capitanía pertenecen exclusivamente a MMatchPlayerM, se capturan en MOpenMatchSheetM y quedan congelados para esa acta.

## SCORER RULES ASSISTANT v1

La decisión cerrada más reciente está en [SCORER RULES ASSISTANT v1](09-scorer-rules-assistant.md). Sustituye los rechazos deportivos anteriores por evaluación y confirmación explícita cuando la transición sea representable; sync conserva decisiones locales y BLOCKED protege exclusivamente integridad, autoridad y causalidad. Las reglas efectivas se congelan al abrir el acta; la UI aprobada y los cinco stores se conservan.


## Observed Libero Replacements & Effective Court v1

La decisión más reciente es [Observed Libero Replacements](10-observed-libero-replacements.md). Sustituye la cobertura automática: planes opcionales sólo sugieren; StartSet, Point y CorrectLastPoint no crean reemplazos observados. LIBERO_ENTER/EXIT registran entrada, salida e intercambio directo de hasta dos declarados con máximo uno efectivo por lado. Se conserva historia automática y compatibilidad explícita de replay/sync.
