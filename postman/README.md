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

Para una corrida completamente nueva, borrar la variable de colección
`runSuffix` o volver a importar la colección. La temporada de prueba usa el año
2090; si ese año ya existe en la base, eliminar los datos de prueba o cambiar el
valor antes de ejecutar la carpeta.

Los requests incluyen aserciones básicas de código HTTP, contratos de error y
resultados relevantes. Las colecciones son una ayuda de exploración y smoke
testing; los tests automatizados de integración siguen siendo la prueba de
regresión principal.
