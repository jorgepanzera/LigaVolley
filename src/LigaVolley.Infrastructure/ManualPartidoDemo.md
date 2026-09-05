# Ejecución local de LigaVolley

Abrir cuatro terminales. Cada proceso tiene un puerto fijo y Vite falla de forma
explícita si ese puerto ya está ocupado.

| Aplicación | Comando desde su proyecto | URL |
|---|---|---|
| API | `dotnet run dev` | `http://localhost:5195` |
| Public | `npm run dev` | `http://localhost:5173` |
| Scorer | `npm run dev` | `http://localhost:5174` |
| Admin | `npm run dev` | `http://localhost:5175/admin/` |

## 1. API

```powershell
cd C:\JCode\LigaVolley\src\LigaVolley.Api
dotnet run dev
```

El perfil predeterminado `dev` establece `ASPNETCORE_ENVIRONMENT=Development`
y publica HTTP en `5195`. Swagger queda disponible en
`http://localhost:5195/swagger`.

Para crear o restaurar el partido demo antes de iniciar normalmente la API:

```powershell
dotnet run -- --seed-demo-match
```

El seeder es idempotente y al finalizar imprime los identificadores y rutas del
partido preparado.

## 2. Frontends

En una terminal distinta para cada proyecto:

```powershell
cd C:\JCode\LigaVolley\src\LigaVolley.Public
npm run dev
```

```powershell
cd C:\JCode\LigaVolley\src\LigaVolley.Scorer
npm run dev
```

```powershell
cd C:\JCode\LigaVolley\src\LigaVolley.Admin
npm run dev
```

Los tres frontends consumen rutas relativas `/api`. Durante desarrollo sus
servidores Vite las redirigen a `http://localhost:5195`, por lo que no requieren
CORS ni una URL configurada manualmente en el navegador.

`npm install` sólo es necesario la primera vez o cuando cambien las dependencias.

## 3. Partido demo

Reemplazar los IDs por los impresos por el seeder:

- Scorer: `http://localhost:5174/?matchId=160`
- Public Match: `http://localhost:5173/matches/160`
- Public Competition: `http://localhost:5173/competitions/5`

Si una instalación anterior del Scorer conserva eventos incompatibles en
IndexedDB, limpiar los datos de `http://localhost:5174` desde DevTools →
Application → Storage → Clear site data.

Para detener los procesos, pulsar `Ctrl+C` en cada terminal.


### Reinicio del partido demo

Cada ejecución de `--seed-demo-match` elimina transaccionalmente el acta anterior del partido demo y sus datos deportivos (eventos, sesiones, auditoría/snapshot, convocados, alineaciones, líberos, sustituciones, timeouts y sets), limpia el resultado y lo devuelve a `SCHEDULED`. Conserva ID, fixture, fecha, sede, planteles y oficiales; no reinicia otros partidos ni la competición. El ID se resuelve mediante los marcadores DEMO, no se fija a 160.

El seeder no puede borrar IndexedDB del navegador. Después de reiniciarlo, cerrar las pestañas del Scorer y limpiar los datos de `http://localhost:5174` en DevTools > Application > Storage > Clear site data antes de volver a abrirlo. Esto descarta también las pruebas offline guardadas en ese origen.
