import 'fake-indexeddb/auto';
let id = 0;
Object.defineProperty(globalThis, 'crypto', {
  configurable: true,
  value: { randomUUID: () => `00000000-0000-4000-8000-${(++id).toString().padStart(12, '0')}` },
});
