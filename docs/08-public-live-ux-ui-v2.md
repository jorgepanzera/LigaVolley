# Public Live UX/UI v2

Decisión cerrada. La página `/matches/{id}` conserva el detalle público y consume `GET /api/public/matches/{id}/live`. Public sigue siendo anónimo, read-only y server-centric: representa exclusivamente el último estado central persistido. No ejecuta MatchEngine, no reconstruye eventos ni deriva servidor, rotación, cancha, sustituciones, líberos, sets ganados o ganador.

## Jerarquía y responsive

Contexto → logos institucionales y equipos → puntos del set actual → sets ganados → resultados de sets finalizados → saque y servidor → cancha P1..P6 → frescura. HOME permanece a la izquierda y AWAY a la derecha. Los logos pertenecen al Club y tienen fallback de iniciales cuando faltan o no cargan.

Diseño mobile-first desde 320 px, sin desplazamiento horizontal. La cancha es una segunda capa en un disclosure nativo, operable con teclado y cerrado inicialmente. Al abrirlo se ven ambos equipos enfrentados y únicamente posición, dorsal, nombre e indicador textual de líbero recibidos del DTO. Ordenar posiciones para dibujarlas es una transformación de presentación, no una derivación deportiva.

A partir de 768 px se amplían espacios y tipografía. Desde 1024 px se usa una superficie central más ancha con cancha abierta inicialmente debajo del marcador. No se agregan datos deportivos por disponer de más ancho. La cancha final queda inicialmente cerrada en cualquier tamaño y se titula «Última formación en cancha».

## Estados y frescura

| Estado / frescura | Presentación |
| --- | --- |
| IN_PROGRESS, edad ≤ 30 s | EN VIVO; puntos actuales dominantes |
| IN_PROGRESS, 30 s < edad ≤ 90 s | EN VIVO · actualización demorada |
| IN_PROGRESS, edad > 90 s | PARTIDO EN CURSO + DATOS SIN ACTUALIZAR |
| IN_PROGRESS, LastUpdatedAt null | PARTIDO EN CURSO + Frescura desconocida; hora no disponible |
| SUSPENDED | PARTIDO SUSPENDIDO; conserva puntos, sets y última cancha |
| FINISHED | FINAL; sets ganados dominantes; sin saque ni servidor |

La edad inicial es `ServerTime - LastUpdatedAt`. Un reloj monotónico del cliente (`performance.now`) mide el tiempo transcurrido desde la recepción de esa respuesta; un temporizador visual actualiza el texto cada segundo sin generar requests. Los umbrales 30/90 viven en `livePolicy.ts`. El redondeo de segundos es sólo textual, no altera la clasificación de los límites. Una fecha nula o inválida produce UNKNOWN; nunca se inventa una fecha ni se presume frescura. Recibir otra respuesta con el mismo LastUpdatedAt no rejuvenece el dato: su ServerTime ya refleja el tiempo transcurrido.

El timestamp operacional conserva su semántica: generado en servidor, cambia sólo con mutaciones deportivas observables aceptadas, nunca por GET, polling, reintentos idempotentes, UUID conocidos o rechazos. Estado deportivo, frescura y fallo de transporte son conceptos separados. Un fallo de polling conserva marcador, cancha y edad del último Live válido y añade un aviso discreto de reintento.

## Ausencia y polling

PENDING y SCHEDULED muestran «El partido todavía no comenzó»; CANCELLED muestra «Partido cancelado». Con `liveAvailable=false` no se solicita Live. Un 404 con código `public_live_match_not_available` termina la carga con ausencia semántica explícita. Un 404 distinto o un fallo real de red/servidor se trata como fallo técnico; nunca se oculta un marcador ya recibido.

Polling preservado: 5 s en IN_PROGRESS, 15 s en SUSPENDED, stop en FINISHED; backoff 5/10/20/30 s ante fallos, reiniciado al recuperarse. Se conserva refresh al recuperar visibilidad y se evitan requests simultáneos. Navegar a otro partido cancela la consulta anterior y descarta respuestas tardías. Un partido que ya era FINISHED carga Live una vez; si el recurso falla, el resultado recibido en Match Detail permanece visible.

No se agregan SignalR, WebSocket, SSE, background sync, PWA, Dexie, IndexedDB ni estado deportivo local durable.

## Ampliación mínima autorizada del contrato

El contrato anterior exponía `servingSide` y cancha efectiva, pero no un servidor explícito. Se autorizó agregar exclusivamente:

```json
"servingPlayer": { "jerseyNumber": 7, "displayName": "Pérez" }
```

`servingPlayer` es nullable; `jerseyNumber` es entero y `displayName` es string. Se mantiene `servingSide` sin cambios. La proyección reutiliza `MatchCourtStateCalculator.Calculate` sobre la formación regular con sustituciones y offset, y `MatchCourtStateCalculator.Server`, exactamente como la derivación existente del servidor en Scorer. No se incorpora una regla de saque nueva ni se obtiene el servidor desde P1 en React. Sólo se proyecta durante Match y set IN_PROGRESS con servidor y dorsal determinables; en READY, entre sets, SUSPENDED y FINISHED es null. No se publican IDs, convocatoria, perfiles, oficiales ni otros atributos del jugador. La cancha mantiene su contrato anterior, incluido su dorsal textual.

No hay cambios de SQL, persistencia, migraciones, Scorer, Admin ni reglas deportivas. OpenAPI y Postman documentan y verifican la ampliación.

## Verificación

- Tests unitarios de presentación: 30/31/90/91 s, null, reloj relativo y estado deportivo independiente de frescura.
- Tests React: jerarquía, servidor exclusivamente desde DTO, cancha colapsable, P1..P6, logos y fallback, suspensión, final, ausencia esperada y conservación ante fallos.
- Tests del hook: intervalos, backoff, recuperación, visibilidad, cierre y cancelación de respuestas de otro partido.
- Playwright: 320/390/768/1024/1440 px, sin overflow, HOME/AWAY, disclosure con teclado, nombres largos y rutas de final/programado. Usa respuestas HTTP controladas para escenarios reproducibles.
- Integración SQL Server: servidor frente a la proyección canónica de Scorer tras rotación, sustitución, corrección y líbero receptor; null entre sets/READY/suspensión/final; timestamps en lectura/retry y esquema OpenAPI.

Comandos desde `src/LigaVolley.Public`: `npm test`, `npm run build`, `npm run e2e`. Desde la raíz: `dotnet test LigaVolley.sln`, con la conexión local de pruebas configurada para bases temporales aisladas.

### Resultado de verificación — 2026-09-05

Public: 33 tests frontend, build de producción y 8 tests Playwright correctos. Backend: 70 tests Domain, 77 Application y los 4 tests de integración Public/OpenAPI correctos, usando bases temporales de SQL Server local con autenticación Windows. La conexión de secrets de desarrollo no dispone de CREATE DATABASE; no se usó la base de desarrollo como base compartida de tests.

La suite completa de integración arroja 58 correctos y 13 fallidos. Se ejecutó también la revisión original `0e634db` en una copia aislada: 56 correctos y exactamente los mismos 13 fallidos. La comparación de los TRX no detecta fallos nuevos. Los fallos existentes corresponden a assets de logos no disponibles (6), tests de catálogo Admin (4), reinicio demo (2) y un test de sync (1). Esos fallos preexistentes quedan fuera del alcance de este slice. Evidencia local en `TestResults/public-live/` y `TestResults/public-live-baseline/results/`; capturas responsive en `src/LigaVolley.Public/test-results/`.
