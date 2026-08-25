# LigaVolley.Scorer

PWA offline-first de operación del acta. React no contiene reglas deportivas: el motor TypeScript produce el nuevo estado y `MatchRepository` persiste evento, snapshot y secuencia en una única transacción Dexie. El Service Worker conserva solamente el App Shell; el partido y la cola durable viven en IndexedDB.

```bash
npm install
npm run dev
npm run build
npm test
npm run e2e
```

La aplicación recibe el partido mediante `?matchId=123`. Para comprobar reentrada offline: cargar el partido conectado, operar al menos una acción, abrir Chrome DevTools → Network → Offline, recargar y continuar operando. Al recuperar red puede usarse `Sync now`; los retries conservan el mismo UUID.

Stores Dexie v1: `appMeta`, `matchSheets`, `sessions`, `snapshots` y `events`. Los eventos usan `PENDING`, `SYNCING` y `ACCEPTED`. En startup, cualquier `SYNCING` vuelve a `PENDING` porque el backend es idempotente.
