# Postman

## Archivos

- `LigaVolley.API.postman_collection.json`: colección única del backend.
- `LigaVolley.Local.postman_environment.json`: configuración del despliegue local.

La colección está organizada primero por superficie HTTP y luego por módulo
funcional. Admin, Scorer y Public permanecen juntos porque comparten datos y
participan en recorridos deportivos transversales.

## Importación

1. Importar `LigaVolley.API.postman_collection.json`.
2. Importar `LigaVolley.Local.postman_environment.json`.
3. Seleccionar el environment **LigaVolley Local**.
4. Iniciar la API:

   ```powershell
   dotnet run --project src/LigaVolley.Api --launch-profile dev
   ```

El environment representa un despliegue y contiene únicamente `baseUrl`. Los
IDs y UUID generados durante una corrida se guardan como variables de colección.
Para comenzar una corrida limpia, usar **Reset all** sobre los valores actuales
de las variables de colección; no es necesario modificar ni reimportar el
environment.

## Estructura

- `00 - Setup y diagnóstico`: instrucciones de preparación de la ejecución.
- `01 - Admin`: maestros, personas, formatos, competiciones y preparación de partidos.
- `02 - Scorer`: apertura, Match Engine, reemplazo de oficiales y Offline Sync.
- `03 - Public`: consultas anónimas y assets públicos.
- `04 - Recorridos end-to-end`: secuencias transversales deliberadas.
- `90 - Casos de error y contratos`: respuestas negativas y Problem Details.

La numeración se usa solamente en el primer nivel. Dentro de cada superficie las
carpetas siguen el dominio, no el orden histórico en que fueron implementados
los slices.

## Ejecución y dependencias

`04 - Recorridos end-to-end / Preparar competición desde cero` crea los datos
básicos y conserva sus IDs. Los módulos restantes pueden ejecutarse de forma
individual cuando sus variables requeridas ya tengan valor.

Algunos escenarios avanzados requieren un estado deportivo específico que no se
puede inferir de un ID cualquiera. Entre ellos están Phase Completion,
Competition Completion, el bloqueo estructural de formatos, MatchSheet, Match
Engine y Offline Sync. Sus variables están declaradas en la colección con valor
vacío para hacer visible esa precondición.

Los requests repetidos son intencionales cuando comprueban idempotencia,
conflictos o variantes de un mismo endpoint. Los tests automatizados de
integración siguen siendo la regresión principal; Postman funciona como catálogo
ejecutable, smoke test y herramienta de exploración.

## Public Live UX/UI v2

`03 - Public / Live` verifica la ampliación mínima `servingPlayer`: nullable, con sólo `jerseyNumber` entero y `displayName` string. El servidor proviene del calculador canónico del backend; se conserva `servingSide`. En FINISHED se exige null. Los estados HTTP usan los nombres JSON vigentes (`InProgress`, `Suspended`, `Finished`). `LastUpdatedAt` admite null histórico; no se lo interpreta como información reciente.

El request de ausencia esperada usa `scheduledMatchId` y verifica 404 `public_live_match_not_available`. Para los otros requests, `matchId` debe referenciar un partido en curso y `finishedMatchId` uno cerrado con acta. Los tests SQL de integración comprueban además la correspondencia con el servidor canónico durante rotación, sustitución, corrección y la nulabilidad en READY, entre sets y suspensión.
