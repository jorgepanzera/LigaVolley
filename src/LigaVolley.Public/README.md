# LigaVolley.Public

Frontend público anónimo y read-only en React 18, TypeScript y Vite. Consume exclusivamente `/api/public`, mantiene el backend como fuente deportiva y usa polling HTTP para livescore. No contiene PWA, Dexie, IndexedDB ni MatchEngine.

```powershell
npm install
npm run dev
```

Public Live UX/UI v2: marcador mobile-first, cancha secundaria colapsable, frescura central 30/90 y resultado final por sets. El servidor se consume desde `servingPlayer`; no se deriva en React. Decisión completa en [docs](../../docs/08-public-live-ux-ui-v2.md).

Validación: `npm test`, `npm run build` y `npm run e2e`. Los tests de navegador usan respuestas HTTP controladas y verifican 320, 390, 768, 1024 y 1440 px; sus capturas quedan en `test-results/`. Requieren Chromium de Playwright (`npx playwright install chromium` si no está instalado).
