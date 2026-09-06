# Observed Libero Replacements & Effective Court v1

Decisión de 2026-09-06, posterior a Console UI v1 y Rules Assistant v1.

## Diagnóstico previo

PrepareSet ofrece por equipo seis regulares, un selector «Líbero del set» (ninguno por defecto) y botones de plazas lógicas 0..5, inicialmente vacías. No selecciona automáticamente plazas ni distingue preferencias de saque/recepción. El frontend envía p1MatchPlayerId..p6MatchPlayerId, liberoMatchPlayerId y liberoLogicalPositions mediante SET_LINEUP. Application recibe SetLineupRequest y guarda MATCH_SET_LIBERO_PLAN (un líbero y máscara 1..63 por equipo/set). Elegir sólo líbero sin plazas deshabilita el plan; SQL lo elimina y el cliente conservaba la selección inactiva hasta reconciliar.

StartSet y Point reconciliaban el plan: P5/P6 y P1 receptor eran elegibles; recuperar saque rota y puede retirar/insertar al líbero, P5→P4 lo retira. Una sustitución regular mantiene la cobertura y cambia el regular subyacente. CorrectLastPoint cerraba reemplazos automáticos y los recalculaba. Las entradas manuales activas tenían precedencia sobre el plan.

automatic no es columna SQL: GET /sheet lo deriva de la ausencia de un evento LiberoEnter con el UUID del reemplazo; TypeScript lo guardaba en snapshot. Los automáticos tienen fila y UUID, pero no evento deportivo independiente. Dos declarados estaban soportados, un único elegido por plan; LIBERO_ENTER ya intercambiaba A→B sobre la misma plaza. El evaluador admitía erróneamente como warning insertar el segundo en otra plaza, dejando dos efectivos.

GET /sheet, sync y takeover proyectan lineups, offsets, sustituciones, reemplazos, planes y última jugada/regular; Dexie conserva el snapshot y replay aplicaba automatismos. Takeover conserva filas sin recalcular. Public calcula cancha desde las filas centrales, nunca lee pendientes. Admin sólo tenía resumen operacional, faltaban cancha detallada e historial. Documentación y pruebas respaldaban automatismos ahora sustituidos por esta decisión explícita.

## Invariantes aprobadas

La alineación inicial P1..P6 contiene seis regulares distintos y se congela al iniciar. Cancha efectiva = alineación + sustituciones regulares + rotación + cobertura observada. StartSet comienza con regulares. Después de StartSet y antes del primer punto se registra LIBERO_ENTER, incluso P5/P6 del equipo al saque o P1 del receptor. Sólo un líbero efectivo por lado. El regular lógico vigente vuelve al salir, incluso después de una sustitución bajo cobertura.

El plan es opcional y sólo genera sugerencias efímeras. Varias plazas candidatas no obligan a dos entradas. Una sugerencia no crea evento, UUID, secuencia, snapshot ni sync. Se confirma mediante el comando deportivo existente; descartarla no persiste nada. Puntos y correcciones no inventan cambios de ocupante. El software no detecta físicamente quién cruzó la zona de reemplazo.

LIBERO_ENTER identifica al entrante y al ocupante efectivo que sale (regular o segundo líbero). LIBERO_EXIT devuelve el regular vigente. Se mantienen UUID, secuencia, payload y confirmedRuleWarnings del único evento. Referencias inválidas, duplicados, dos líberos efectivos, lifecycle y tracking deshabilitado son HARD. Frente, saque no permitido y reemplazos sin jugada completada son WARNING confirmables. Sync sólo protege HARD y conserva decisiones locales.

## Compatibilidad

Las actas nuevas congelan rulesProtocolVersion/rulesSnapshotVersion 2 en las columnas existentes. Los comandos locales nuevos llevan observedLiberoReplacements=true en el payload. Los comandos directos usan semántica observada. El replay y sync de comandos antiguos sin marca en actas anteriores conservan el algoritmo legado; no se reescriben payloads, filas ni resultados aceptados. En actas v2 los eventos nuevos START_SET/POINT/CORRECT_LAST_POINT sin marca se rechazan como incompatibilidad técnica; los UUID ya aceptados continúan siendo reintentables. Actualizar el cliente antes de operar actas v2. La migración 20260906181534_ObservedLiberoReplacements amplía únicamente CK_MATCH_SHEET_rules_snapshot para admitir el par versión/protocolo 2; no agrega tablas o columnas ni modifica filas. Down rechaza actas v2 o payloads observados antes de cambiar la restricción. Las sugerencias nunca viajan a Public.

## Verificación

Resultados de ejecución y estado final del demo: se completan al terminar la validación del slice.
