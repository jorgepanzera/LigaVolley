# 01 — Arquitectura

## Visión

LigaVolley tendrá un único backend y una única base de datos SQL Server. Tendrá tres frontends separados —Admin, Scorer y Public— que consumen el mismo backend.

## Backend

Se adopta un **Modular Monolith**. El objetivo es separar claramente módulos y responsabilidades sin asumir el coste operacional y transaccional de microservicios.

Proyectos base:

- Domain
- Application
- Infrastructure
- Api

## Frontends

### Admin

Aplicación orientada a mantenimiento y administración de:

- clubes;
- equipos;
- sedes/canchas;
- personas;
- jugadores/técnicos/árbitros;
- planteles por competencia;
- competiciones;
- formatos;
- inscripciones de equipos;
- fixture;
- configuración y seguimiento de partidos.

### Scorer

La apertura del acta requiere conectividad y materializa un snapshot operacional autosuficiente (`MATCH_SHEET`, equipos, convocatoria, staff, líberos y sesión). A partir de ese bootstrap la futura PWA podrá persistir el estado en IndexedDB; este slice no implementa todavía el protocolo de sincronización.

Aplicación especializada en el partido en vivo. Debe concebirse como una consola operacional con flujos rápidos, estado visible y tolerancia a conectividad intermitente.

### Public

Aplicación de consulta pública, sin funciones administrativas ni de scoring. Permitirá consultar información publicada de las competiciones, incluyendo como mínimo fixture, resultados, tablas de posiciones e información pública de partidos. Sus contratos y experiencia de usuario deben optimizarse para lectura y navegación pública.

## API surfaces

Las rutas se separan obligatoriamente por consumidor mediante estos prefijos:

- `/api/admin`
- `/api/scorer`
- `/api/public`

## Security

Security es transversal. En esta etapa no están cerrados:

- proveedor de identidad;
- autenticación;
- roles;
- claims;
- permisos finos.

No acoplar el dominio a una tecnología concreta de autenticación hasta tomar esa decisión.

## Persistencia

SQL Server es la base de datos única de la aplicación. Se preservan restricciones relacionales importantes mediante PK, FK, UNIQUE y CHECK cuando resulte apropiado.
