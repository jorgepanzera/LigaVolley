# 07 — Decisiones y pendientes

## Decisiones cerradas

- Backend único.
- Base SQL Server única.
- Arquitectura backend Modular Monolith.
- Tres frontends: Admin, Scorer y Public.
- Prefijos de API obligatorios por consumidor: `/api/admin`, `/api/scorer` y `/api/public`.
- Flujo inicial del Scorer: seleccionar/validar planteles → abrir acta → asignar oficiales → alineación inicial.
- `Season` y `Divisional` son entidades maestras; cada `Competition` referencia obligatoriamente una de cada una.
- `PLAYER_ROLE` es contextual al ámbito competitivo/plantel, no una clasificación global inmutable del jugador.
- Una `PERSON` puede ser simultáneamente `PLAYER`, `COACH` y/o `REFEREE`; por ahora no se modelan vigencias ni exclusividad.
- No se adopta event sourcing: se persiste estado operacional actual y se registran eventos/auditoría relevantes.
- Public expone solo información explícitamente habilitada para publicación.
- Public es la aplicación de consulta pública de competiciones, fixture, resultados, tablas de posiciones e información pública de partidos.
- Scorer es una consola del partido.
- Formatos de competición parametrizables.
- Competiciones clonables a nivel estructural.
- No copiar equipos/fixture/resultados/fechas/planteles al clonar.
- `PERSON` como raíz de jugador/técnico/árbitro.
- Planteles contextualizados por competición.
- Hasta dos líberos.
- Estado de seis jugadores efectivos derivado de alineación + sustituciones + rotación + líbero.
- Se contempla tercer puesto además de semifinales y final.
- Scorer debe contemplar offline/sincronización.

## Pendientes que NO deben inventarse durante implementación

1. Tecnología exacta de los frontends.
2. Proveedor y esquema de autenticación/autorización.
3. Reglas finas de permisos.
4. Catálogo completo y definitivo de endpoints.
5. DTOs finales de cada módulo.
6. Estrategia concreta de persistencia local/offline del Scorer.
7. Protocolo de sincronización y resolución de conflictos.
8. Mecanismo exacto de corrección/anulación/compensación/versionado de eventos.
9. Límite exacto entre estado canónico, estado derivado y snapshots para offline.
10. Reglas reglamentarias finas sobre uso/habilitación de uno o dos líberos.
11. Reglas adicionales de publicación para planteles, personas, oficiales u otros datos públicos.
12. Reglas reglamentarias adicionales que aún no se hayan validado explícitamente.
13. Observabilidad, logging, tracing y despliegue.

## Próximo paso recomendado

Diseñar e implementar primero el backend por módulos, comenzando por contratos/casos de uso antes de escribir controllers o persistencia.
