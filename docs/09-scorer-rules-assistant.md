# SCORER RULES ASSISTANT v1

## Decisión cerrada

Scorer es asistente del tercer juez. Separa Evaluate de Apply: las violaciones HARD protegen lifecycle, referencias, representación determinista, autoridad e integridad; los warnings deportivos requieren confirmación concreta del juez antes de crear el evento local. Cancelar no consume UUID ni secuencia. Las confirmaciones pertenecen al payload del único evento deportivo, nunca a un evento override separado.

El backend es autoridad sobre integridad, sincronización y persistencia canónica. Una decisión deportiva técnicamente representable ya persistida localmente no puede ser rechazada posteriormente por reevaluación reglamentaria del backend. Sync aplica invariantes HARD y conserva las confirmaciones originales incluso cuando su evaluación deportiva difiere. BLOCKED queda reservado para problemas técnicos, autoridad, causalidad e integridad.

CompetitionFormat define defaults de seis sustituciones y dos timeouts por equipo/set. Competition permite overrides nullable; el valor efectivo es override ?? default. Los defaults forman parte del agregado estructural bloqueable del formato y se copian al clonar. OpenMatchSheet congela columnas explícitas autosuficientes: límites efectivos, LiberoEnabled=true, MaxLiberos=2, LiberoCanServe=false, DecidingSetCourtChangePoint=8 y versión de snapshot/protocolo. Actas anteriores conservan snapshot legacy con sustituciones ilimitadas (null), sin reinterpretar sus eventos. Modificar Competition no cambia actas abiertas.

Lineup representa seis jugadores regulares distintos del lado correcto, sin líberos declarados. El líbero es una capa separada de cancha efectiva. Una sustitución cambia el regular lógico incluso cuando lo cubre un líbero; al salir éste vuelve el regular vigente. Una solicitud múltiple contiene todas sus parejas y se aplica atómicamente; cada pareja cuenta para el límite. La irregularidad deportiva representable admite confirmación; duplicar un jugador efectivo es HARD.

Scorer mantiene y muestra P1..P6 y servidor esperado derivados de lineup, sustituciones y offset. Registrar excepcionalmente un servidor observado no modifica el orden rotacional. No se detecta posición física real ni solapamiento. Los warnings aparecen sólo ante acciones concretas o consultas explícitas. Cambio de campo al alcanzar ocho en el set decisivo es un reminder no bloqueante; HOME permanece a la izquierda.

Se conserva la UI aprobada, sus overlays, jerarquía de puntos y los cinco stores Dexie. Admin configura y supervisa; Public proyecta exclusivamente estado canónico aceptado. No se agrega motor a Public, autenticación, sanciones ni corrección histórica general.

Esta decisión reemplaza las prohibiciones deportivas anteriores sobre parejas de sustitución, uso irregular del líbero y límite duro de timeouts; no reemplaza las garantías técnicas de sync, takeover, CLOSED ni CorrectLastPoint.

## Contratos y evaluación

`GET /api/scorer/matches/{id}/sheet`, apertura, sync y takeover incluyen `rulesSnapshot` y los flags de tracking. `operationalState` conserva las sustituciones por pareja, el carácter `automatic` de los reemplazos y `lastLiberoRally`/`lastLiberoRegular` por lado, necesarios para una evaluación consistente tras reentrada. No se agrega ningún campo a Public en este slice.

```json
{
  "rulesSnapshot": {
    "rulesProtocolVersion": 1,
    "rulesSnapshotVersion": 1,
    "maxSubstitutionsPerSet": 6,
    "maxTimeoutsPerSet": 2,
    "liberoEnabled": true,
    "maxLiberos": 2,
    "liberoCanServe": false,
    "decidingSetCourtChangePoint": 8
  }
}
```

Los comandos directos de punto, timeout, sustitución individual, entrada/salida de líbero admiten `confirmedRuleWarnings: string[]` opcional. Ausente/null se interpreta como lista vacía. `AddPoint` agrega `observedServerMatchPlayerId` nullable; su omisión sigue siendo el flujo normal, sin preguntas adicionales.

`POST /api/scorer/matches/{matchId}/sets/{setNumber}/substitution-requests`:

```json
{
  "eventUuid": "11111111-1111-4111-8111-111111111111",
  "side": "Home",
  "replacements": [
    { "playerOutMatchPlayerId": 101, "playerInMatchPlayerId": 107 },
    { "playerOutMatchPlayerId": 102, "playerInMatchPlayerId": 108 }
  ],
  "confirmedRuleWarnings": ["substitution_limit_exceeded"]
}
```

Sin cobertura de todos los warnings actuales, devuelve `409` con `code: rule_confirmation_required` y `warnings: [{code, context, requiresConfirmation:true}]`; todavía no crea evento ni sustituciones. `context` contiene sólo datos concretos como `used`, `requested`, `projected`, `maximum`, identificadores esperados/observados o posición. Al confirmar se reevalúa la acción y, si resulta representable, se registra una vez. Un UUID aceptado con contenido distinto produce conflicto. No existe `force` ni un evento override independiente.

En `/sync`, el evento `SUBSTITUTION_REQUEST` lleva `setNumber`, `side`, `replacements` y `confirmedRuleWarnings` dentro de `payload`; conserva el UUID, secuencia y timestamp del sobre existente. La lista original se audita intacta, aunque incluya un warning que el backend no reproduzca o falte uno que el backend detecte. No se solicita reconfirmación durante replay/reconciliation ni se cambia el payload para adecuarlo al backend. Sólo las invariantes HARD pueden impedir la aplicación. El batch sigue siendo transaccional y los reintentos usan el hash del contenido original.

## Códigos de reglas

| Clase | Códigos |
| --- | --- |
| WARNING, sustitución | `substitution_limit_exceeded`, `substitution_reentry_irregular`, `substitution_player_already_bound`, `substitution_original_player_mismatch` |
| WARNING, líbero | `libero_wrong_regular_replacement`, `libero_irregular_second_libero_replacement`, `libero_replacement_without_completed_rally`, `libero_in_front_row`, `libero_service_not_allowed`, `libero_used_as_regular_substitute` |
| WARNING, saque/timeout | `unexpected_server`, `timeout_limit_exceeded` |
| Respuesta de confirmación directa | `rule_confirmation_required` |
| HARD, representación/referencias | `invalid_side`, `invalid_court_state`, `invalid_substitution`, `substitution_player_already_on_court`, `duplicate_effective_player`, `server_player_wrong_team`, `libero_not_declared`, `libero_invalid_replaced_player`, `libero_already_on_court`, `invalid_libero_replacement` |
| HARD, configuración/alineación | `substitution_tracking_disabled`, `libero_tracking_disabled`, `lineup_duplicate_player`, `lineup_player_wrong_team`, `lineup_libero_not_allowed`, `lineup_invalid`, `lineup_incomplete`, `lineup_locked`, `invalid_libero_plan`, `ambiguous_libero_plan` |
| HARD, lifecycle | `match_already_closed`, `match_already_decided`, `match_not_decided`, `match_set_not_found`, `match_set_invalid_state`, `match_sheet_invalid_state`, `point_not_last_effective_event` |

Los códigos técnicos existentes de sync se conservan: por ejemplo `sync_event_uuid_conflict`, `sync_sequence_gap`, sesión no activa/mismatch y payload inválido. Ningún código WARNING habilita a ignorar un HARD. La clasificación explícita del evaluador es `VALID`, `WARNING_REQUIRES_CONFIRMATION` o `INVALID_STATE`. Sin tracking no se presume infracción por falta de historia.

## Admin y persistencia

Create/Update/Validate de CompetitionFormat reciben `maxSubstitutionsPerSet` y `maxTimeoutsPerSet` (defaults 6/2), también expuestos en sus DTOs. Clonar copia los valores; un formato estructuralmente bloqueado no permite modificarlos.

`PUT /api/admin/competitions/{id}/match-rules` recibe `maxSubstitutionsPerSetOverride` y `maxTimeoutsPerSetOverride`, ambos nullable. Los valores configurados deben ser enteros 1..99. Null restaura herencia. `CompetitionDto.matchRules` informa overrides, defaults y valores efectivos. Admin integra estos campos en el editor de formatos y en la Competition; no opera decisiones deportivas. Las competiciones finalizadas/canceladas quedan en consulta.

Migración `20260905233721_ScorerRulesAssistant`:

| Tabla | Cambio |
| --- | --- |
| `COMPETITION_FORMAT` | `max_substitutions_per_set`, `max_timeouts_per_set`, defaults 6/2 y check 1..99 |
| `COMPETITION` | Overrides nullable con checks 1..99 |
| `MATCH_SHEET` | Ocho columnas explícitas del snapshot, con defaults legacy para filas anteriores |
| `MATCH_EVENT` | `command_payload` nullable para el comando/confirmaciones; enum `SUBSTITUTION_REQUEST` aditivo |
| `MATCH_SUBSTITUTION` | `request_event_uuid` nullable/indexado para agrupar parejas en un evento |
| `MATCH_TIMEOUT` | `timeout_number` pasa a int positivo para representar excesos confirmados |

No hay tablas nuevas ni nueva persistencia de cancha. La sesión/dispositivo y timestamp siguen en la auditoría/evento existente. Los eventos históricos sin payload de confirmación equivalen a `[]`; no se reescribe su historia. Down rechaza la pérdida silenciosa de actas nuevas o payloads operativos: requieren un plan explícito de archivo/conversión antes de retroceder a binarios que no comprenden estas decisiones.

## UX y verificación

El overlay existente muestra advertencia, contexto, `Cancelar` y `Registrar igualmente`. Se conservan HOME/AWAY, geometría de cancha, PrepareSet, puntos dominantes y drawers. La sustitución muestra contadores y permite agregar parejas; la opción excepcional permanece explícita. Líbero manual y servidor observado viven en consultas secundarias de la plaza seleccionada. El historial local identifica las decisiones confirmadas. Corregir un punto conserva una decisión manual de líbero anterior y recalcula las entradas automáticas según el estado reconstruido.

`tests/shared/rules-assistant-v1.json` reúne los vectores compartidos C#/TypeScript. Las suites cubren límites, parejas múltiples, líbero sobre regular sustituido, segundo líbero, saque observado, timeout, legacy, CLOSED, confirmación/cancelación, replay, discrepancia backend, idempotencia y takeover. Los tests SQL crean bases aisladas; nunca usan LigaVolleyDev como base descartable.

El E2E opt-in `rules-assistant-real.spec.ts` usa respuestas reales de la API y el demo previamente reiniciado: apertura, PrepareSet, séptima sustitución cancelada/confirmada, tercer timeout, operación y recarga offline, sync, Public Live, pérdida de autoridad, BLOCKED y takeover conservando cola/candidatos/reglas. Ejecutar desde Scorer con `RULES_E2E_MATCH_ID` y opcionalmente `RULES_E2E_API`; no se reinicia una base automáticamente desde el test. Sus screenshots y JSON canónico se guardan como artifacts Playwright.

Permanecen fuera del alcance sanciones, reglas finas sobre obligatoriedad de uno/dos líberos, detección de posiciones físicas reales, corrección histórica general, autenticación nueva y branching/rebase offline.

## Resultado de validación — 2026-09-05

- Build .NET: correcto, cero errores/advertencias. Domain: 103/103; Application: 78/78.
- Integración SQL: 73/83. Pasan todos los casos de MatchEngine/Rules Assistant, OpenAPI y demo. Quedan diez fallos preexistentes ajenos a este slice: cuatro fixtures de `AdminCatalogEndpointsTests` desactualizados y seis casos de `Livosur2026ClubLogoSeederTests` por ausencia del paquete `seed-assets/club-logos` aprobado. No se alteraron esos contratos ni se fabricaron assets para ocultar los fallos.
- Scorer: 83/83 tests, build correcto y 10/10 E2E habituales. El E2E real opt-in pasó por separado con SQL Server local, sin respuestas deportivas simuladas.
- Admin: 13/13 tests y build correcto. Public: 33/33 tests y build correcto.
- Migración aplicada en LigaVolleyDev y en bases aisladas de integración. El reinicio del demo se verificó antes y después del E2E; quedó nuevamente SCHEDULED sin acta. El ID usado se obtuvo del resultado del seeder, que resuelve los marcadores DEMO.
- Evidencias locales en `TestResults/rules-assistant`: TRX/logs, OpenAPI generado y carpeta `e2e` con screenshots, snapshot canónico y Public Live. En la prueba final se aceptaron siete sustituciones HOME, tres timeouts y una nueva sesión tras takeover; las decisiones anteriores quedaron conservadas.
