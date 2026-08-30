# 01 — Arquitectura

## Visión

LigaVolley tiene un único backend ASP.NET Core, una única base SQL Server y tres frontends separados: Admin, Scorer y Public.

## Backend

El backend es un **Modular Monolith**. Los módulos comparten proceso y base de datos, pero preservan límites claros de dominio y aplicación. No se introducen microservicios, mensajería distribuida, CQRS distribuido ni event sourcing salvo una decisión de arquitectura posterior.

Proyectos base:

- `LigaVolley.Domain`: entidades, value objects, reglas e invariantes;
- `LigaVolley.Application`: casos de uso, contratos, validaciones y puertos;
- `LigaVolley.Infrastructure`: SQL Server, persistencia e integraciones;
- `LigaVolley.Api`: endpoints HTTP y composición.

Domain y Application no dependen de Infrastructure. Los endpoints delegan las reglas de negocio en Application o Domain.

## Frontends

### Admin

`LigaVolley.Admin` usa React 18, TypeScript, Vite, React Router, TanStack Query, React Hook Form, Zod superficial y el cliente HTTP centralizado. Es server-centric, consume exclusivamente `/api/admin` y no incorpora PWA, Service Worker, IndexedDB, Dexie, modo offline ni MatchEngine local.

Admin prepara y supervisa:

- catálogos y personas;
- perfiles Player, Coach y Referee;
- competiciones, formatos, participantes, fixture y progresión;
- planteles por Competition;
- programación y oficiales de Match;
- readiness para Scorer;
- supervisión read-only del MatchSheet.

Competition Workspace agrupa Resumen, Participantes, Fixture, Planteles y Progresión. Match Workspace agrupa Resumen, Preparación, Oficiales y Acta. Admin no abre el acta ni ejecuta acciones deportivas.

### Scorer

`LigaVolley.Scorer` usa React, TypeScript y Vite como PWA offline-first. Es la única consola operacional del partido.

La apertura del acta requiere conectividad y materializa un snapshot autosuficiente con MatchSheet, equipos, convocatoria, staff, líberos y sesión. Desde allí, Dexie/IndexedDB conserva el estado deportivo y una cola local durable. El Service Worker cubre únicamente el App Shell.

El MatchEngine TypeScript es puro y no depende de React, red ni persistencia. Cada mutación se aplica primero localmente y persiste evento, snapshot y secuencia en una transacción; la sincronización con el backend ocurre después. La reconciliación usa snapshot canónico más replay de pendientes.

### Public

`LigaVolley.Public` usa React 18, TypeScript y Vite. Es anónimo, read-only y server-centric; no usa PWA, IndexedDB deportivo ni MatchEngine local.

Public sólo consulta información explícitamente publicable. Live lee el último estado operacional central mediante polling HTTP; no usa SignalR ni WebSocket en v1.

## Superficies HTTP

Las rutas se separan obligatoriamente por consumidor:

- `/api/admin`;
- `/api/scorer`;
- `/api/public`.

Cada superficie tiene contratos propios. Se puede reutilizar lógica interna, pero no se comparte automáticamente el mismo DTO HTTP entre consumidores.

## Persistencia y consistencia

SQL Server es la única base de datos. PK, FK, UNIQUE y CHECK preservan las invariantes relacionales pertinentes.

No se adopta event sourcing. Se persiste el estado operacional actual y, cuando corresponde, eventos o auditoría para trazabilidad y reconstrucción.

La sincronización offline entra por Application y reutiliza el mismo MatchEngine del backend dentro de una transacción serializable y el bloqueo existente por Match. `MATCH_EVENT.SequenceNumber` es global y canónico; `LocalSequence` es causal dentro de una sesión.

## Security

Security es transversal. Permanecen abiertos el proveedor de identidad, autenticación, roles, claims y permisos finos. El dominio no debe acoplarse a una tecnología concreta hasta cerrar esas decisiones.
