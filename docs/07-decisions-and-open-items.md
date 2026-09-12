# 07 — Decisiones y pendientes

## Decisiones cerradas

- Arquitectura: modular monolith .NET con una base SQL Server y tres frontends.
- API: prefijos obligatorios `/api/admin`, `/api/scorer` y `/api/public`; cada superficie tiene contratos propios.
- Competición: toda Competition requiere Season, Division y CompetitionFormat. Cambios estructurales de formatos usados requieren clonación.
- Fixture y progresión: fixture incremental con participantes resueltos; `CompletePhase` y `CompleteCompetition` son explícitos, transaccionales e idempotentes. Los ascensos y descensos aplicables quedan como resultados deportivos históricos y no inscriben equipos futuros.
- Personas y roster: perfiles opcionales sobre Person, roster explícito, 15 jugadores ACTIVE y dos técnicos ACTIVE. Función habitual nullable e informativa; no existe máximo de funciones habituales LIBERO.
- MatchSheet: convocatoria, dorsal, capitanía, reglas y declaración `MATCH_LIBERO` quedan congeladas al abrir.
- Scorer: PWA con cinco stores Dexie, eventos locales secuenciados, snapshot canónico, replay y takeover.
- Rules Assistant: HARD protege integridad; WARNING representable requiere confirmación explícita.
- Líberos: `MATCH_LIBERO` es la única declaración reglamentaria. Planes opcionales sólo sugieren y la cancha efectiva cambia mediante hechos observados.
- Public: anónimo, read-only y server-centric; Live usa polling HTTP y nunca deriva reglas deportivas en React.

## Pendientes explícitos

1. Proveedor de identidad y permisos finos.
2. Reglas reglamentarias finas sobre obligatoriedad y cantidad de líberos.
3. Sanciones, estadísticas por posición y funciones tácticas por set.
4. Corrección histórica general distinta de CorrectLastPoint.
5. Branching o rebase offline después de un evento rechazado.
6. Auditoría deportiva definitiva de reemplazo de oficiales.
7. Semántica de CarryOverMode distinta de NONE.
8. Decisión operativa sobre promoción y descenso futuro.

Los documentos 03, 05, 09 y 10 contienen las decisiones detalladas que gobiernan estos temas.
# ComposiciÃ³n de participantes

La promociÃ³n y el descenso persistidos se pueden usar como sugerencia histÃ³rica para una Competition DRAFT, mediante fuentes seleccionadas explÃ­citamente. No se inscribe ningÃºn equipo en forma automÃ¡tica; quedan pendientes las reglas de cupos y cascadas entre competiciones futuras.
