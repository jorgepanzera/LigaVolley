Manual para reproducirlo
Abre cuatro terminales en C:\JCode\LigaVolley.
1. Crear o restaurar el escenario demo
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/LigaVolley.Api -- --seed-demo-match
El seeder es idempotente: puedes ejecutarlo nuevamente sin duplicar jugadores, planteles, oficiales ni seleccionar otro partido demo.
Al finalizar imprime los identificadores y URLs correspondientes.
2. Ejecutar la API
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/LigaVolley.Api --urls http://localhost:5000
Debe usarse localhost:5000 porque los proxies de Vite apuntan a esa dirección.
3. Ejecutar Scorer
cd C:\JCode\LigaVolley\src\LigaVolley.Scorer
npm install
npm run dev -- --host 127.0.0.1 --port 5173 --strictPort
npm install solamente es necesario la primera vez o cuando cambien las dependencias.
Abrir:
http://127.0.0.1:5173/?matchId=160
4. Ejecutar Public
cd C:\JCode\LigaVolley\src\LigaVolley.Public
npm install
npm run dev -- --host 127.0.0.1 --port 5174 --strictPort
Abrir:
http://127.0.0.1:5174/matches/160
http://127.0.0.1:5174/competitions/5
5. Detener todo
Pulsa Ctrl+C en cada una de las tres terminales de ejecución.
Nota sobre la vista pública
Mientras el partido permanezca SCHEDULED, Public mostrará su información programada. Después de iniciar el partido desde Scorer, recarga una vez la página pública; desde ese momento habilitará la consulta live y actualizará el marcador periódicamente.
Si una instalación anterior del Scorer conserva eventos incompatibles en IndexedDB, limpia los datos del sitio 127.0.0.1:5173 desde DevTools → Application → Storage → Clear site data y vuelve a abrir la URL.