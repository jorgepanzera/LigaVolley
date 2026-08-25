# 05 — Partido en vivo y Scorer

## Objetivo

Modelar un partido completo desde la apertura del acta hasta el cierre, preservando suficiente información para reconstruir el estado reglamentario y permitir correcciones.

## Flujo mínimo validado conceptualmente

1. Cargar/seleccionar los planteles habilitados.
2. Abrir acta.
3. Asignar oficiales.
4. Definir alineación inicial del primer set.
5. Iniciar set.
6. Registrar puntos.
7. Gestionar cambio de saque y rotación.
8. Registrar sustituciones.
9. Registrar reemplazos de líbero.
10. Registrar timeouts.
11. Finalizar set.
12. Comenzar sets siguientes.
13. Corregir/anular un punto o evento cuando corresponda.
14. Cerrar partido.

## Jugadores efectivos en cancha

La definición acordada es:

`alineación inicial P1..P6 + sustituciones normales + rotation_offset + reemplazo de líbero activo = 6 jugadores físicamente en cancha`

Esta fórmula conceptual es central para el diseño.

### Alineación

Cada set comienza con seis posiciones reglamentarias P1..P6 por equipo.

### Rotación

La rotación se modela mediante un desplazamiento/estado (`rotation_offset` o equivalente) sobre la alineación vigente, evitando reescribir innecesariamente seis filas ante cada cambio de saque.

### Sustituciones

Las sustituciones normales modifican qué jugador ocupa la plaza lógica correspondiente para ese set.

### Líbero

El sistema debe soportar un máximo de dos líberos registrados/habilitados y registrar el reemplazo activo de líbero de manera diferenciada de una sustitución normal. Las reglas reglamentarias finas que determinen cuándo corresponde registrar uno o dos líberos quedan pendientes de definición explícita.

## Estado que debe poder obtenerse

Para cualquier instante relevante del partido:

- marcador por set y partido;
- equipo al saque;
- jugador servidor;
- rotación;
- seis jugadores efectivos en cancha por equipo;
- sustituciones realizadas;
- reemplazo de líbero activo;
- timeouts;
- secuencia de eventos;
- correcciones.

## Persistencia de estado y eventos

### Reemplazo de oficiales

Los tres oficiales se designan inicialmente desde Admin. Durante `IN_PROGRESS`, Scorer puede reemplazar el Referee vigente de un rol mediante un caso de uso específico, sin convertir Scorer en CRUD administrativo. `MATCH_OFFICIAL` conserva el estado canónico actual. El futuro MatchSheet deberá auditar un evento `OFFICIAL_REPLACEMENT` con rol, Referee anterior, Referee nuevo y fecha. Antes de `OpenMatchSheet` deberán existir los tres roles; esa precondición no forma parte de este slice.

No se adopta event sourcing como arquitectura. El estado operacional actual necesario para operar y consultar eficientemente el partido se persiste. Los cambios relevantes se registran además como eventos/auditoría para mantener trazabilidad y permitir correcciones o reconstrucción cuando corresponda.

El límite exacto entre estado canónico persistido, datos derivados y snapshots para operación offline se cerrará al diseñar la sincronización.

## Correcciones

Una corrección no debe destruir la trazabilidad necesaria del partido. El mecanismo exacto —anulación, compensación, versionado u otro equivalente— queda abierto hasta diseñar los casos de uso de corrección y sincronización del Scorer, pero debe preservar la consistencia del estado resultante y la historia relevante.

## Offline

Scorer debe tolerar pérdida temporal de conectividad. El diseño de persistencia local, IDs de eventos, resolución de conflictos y sincronización se definirá antes de implementar esta parte.
