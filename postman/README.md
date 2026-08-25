# Postman

## Importación

1. Importar `LigaVolley.Admin.postman_collection.json` en Postman.
2. Importar `LigaVolley.Local.postman_environment.json`.
3. Seleccionar el environment **LigaVolley Local**.
4. Iniciar la API en `http://localhost:5195`:

   ```powershell
   dotnet run --project src/LigaVolley.Api --launch-profile http
   ```

## Uso

La carpeta **01 - Catálogos y competición** debe ejecutarse primero. Los scripts
de test guardan en el environment los IDs devueltos por la API. Después pueden
ejecutarse las carpetas restantes o requests individuales.

La carpeta **04 - Standings** contiene la consulta de fase regular, la consulta
por grupo y un caso de grupo inválido. Para la consulta exitosa por grupo deben
definirse `competitionId`, `phaseId` y `phaseGroupId` con datos ya materializados
por la futura progresión deportiva o preparados para pruebas.

Para una corrida completamente nueva, borrar la variable de colección
`runSuffix` o volver a importar la colección. La temporada de prueba usa el año
2090; si ese año ya existe en la base, eliminar los datos de prueba o cambiar el
valor antes de ejecutar la carpeta.

La carpeta **05 - Phase Completion** contiene los cinco recorridos del slice.
Configurar `completionCompetitionId`/`completionPhaseId` con una fase
`IN_PROGRESS` cuyos partidos estén `FINISHED`, y
`blockedCompetitionId`/`blockedPhaseId` con una fase que tenga partidos no
resueltos o `CANCELLED`. Ejecutar el cierre exitoso antes del cierre repetido;
este último verifica `AlreadyCompleted = true`. El preview bloqueado permanece
informativo (`200`, `CanComplete = false`) y el POST bloqueado espera `409` con
`phase_cannot_complete`.

Los requests incluyen aserciones básicas de código HTTP, contratos de error y
resultados relevantes. Las colecciones son una ayuda de exploración y smoke
testing; los tests automatizados de integración siguen siendo la prueba de
regresión principal.
