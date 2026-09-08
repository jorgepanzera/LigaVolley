# 08 — Consulta pública y Live

La página `/matches/{id}` conserva el detalle público y consume `GET /api/public/matches/{id}/live`. Public sigue siendo anónimo, read-only y server-centric: representa exclusivamente el último estado central persistido. No ejecuta MatchEngine, no reconstruye eventos ni deriva servidor, rotación, cancha, sustituciones, líberos, sets ganados o ganador.

## Shell y navegación

Public usa un shell común. En desktop muestra navegación lateral y en móvil un drawer accesible; ambos contienen Inicio y Competiciones. El selector usa exclusivamente Seasons públicas y navega a `/seasons/{seasonId}`; esa ruta es el destino canónico del contexto de Season y no usa almacenamiento local. Fixture, Posiciones y Playoffs son enlaces contextuales de una Competition. El detalle de Match reconstruye su Competition y Season desde su DTO público, por lo que los deep links conservan el shell y el contexto sin almacenamiento local.

## Season Home

`GET /api/public/seasons/{seasonId}/home` alimenta exclusivamente `/seasons/{seasonId}`. Devuelve un único agregado público con `season`, `activeCompetitions`, `liveMatches`, `upcomingMatches`, `recentResults` y `finishedCompetitions`. `activeCompetitions` incluye sólo Competitions `SCHEDULED` e `IN_PROGRESS`; `finishedCompetitions`, sólo `FINISHED`. Los partidos live son `IN_PROGRESS`; próximos son `SCHEDULED` con fecha y los resultados recientes son `FINISHED`.

El servidor ordena los bloques y aplica un máximo de ocho partidos a live, próximos y resultados: live y próximos por fecha ascendente, y resultados por fecha descendente. El agregado sólo contiene summaries de Competition y Match: no incluye roster, personas, oficiales, cancha, eventos ni el estado operacional de Live. `DRAFT` y sus recursos dependientes no son publicables y una Season sin Competition publicable se comporta como inexistente.

React no compone esta pantalla consultando fixtures por Competition ni solicita `/live` por partido. Renderiza cada bloque directamente desde el agregado y conserva estados de carga, error, Season sin actividad y bloques vacíos parciales dentro del shell persistente.

## Jerarquía y responsive

Contexto → logos institucionales y equipos → puntos del set actual → sets ganados → resultados de sets finalizados → saque y servidor → cancha P1..P6 → frescura. HOME permanece a la izquierda y AWAY a la derecha. Los logos pertenecen al Club y tienen fallback de iniciales cuando faltan o no cargan.

En Fixture / Resultados cada Match es una fila compacta y navegable al detalle: equipos y logos ocupan la primera zona, mientras resultado, estado y fecha/sede se escanean en la segunda. El fixture proyecta los logos institucionales HOME y AWAY en todos los estados del Match; en móvil la metadata puede pasar debajo de los equipos sin desbordamiento horizontal. Las fases, grupos, rondas y series de playoffs conservan su agrupación y sólo muestran Matches materializados.

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

## Contrato Live

Live expone `servingSide`, cancha efectiva y el siguiente servidor explícito:

```json
"servingPlayer": { "jerseyNumber": 7, "displayName": "Pérez" }
```

`servingPlayer` es nullable; `jerseyNumber` es entero y `displayName` es string. Se mantiene `servingSide` sin cambios. La proyección reutiliza `MatchCourtStateCalculator.Calculate` sobre la formación regular con sustituciones y offset, y `MatchCourtStateCalculator.Server`, exactamente como la derivación existente del servidor en Scorer. No se incorpora una regla de saque nueva ni se obtiene el servidor desde P1 en React. Sólo se proyecta durante Match y set IN_PROGRESS con servidor y dorsal determinables; en READY, entre sets, SUSPENDED y FINISHED es null. No se publican IDs, convocatoria, perfiles, oficiales ni otros atributos del jugador. La cancha mantiene su contrato anterior, incluido su dorsal textual.

El contrato no expone IDs, convocatoria, perfiles, oficiales ni reglas deportivas adicionales. OpenAPI y Postman lo mantienen consistente.
