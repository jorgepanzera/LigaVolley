# 04 — Personas y planteles

## Personas y perfiles

`PERSON` contiene la identidad. `PLAYER`, `COACH` y `REFEREE` son perfiles 1:1 opcionales y una misma persona puede tener más de uno. No hay vigencias ni exclusividad entre perfiles en v1.

No existe DELETE físico. Health Card y League Card son documentos históricos. Health Card es warning para Player y Referee, nunca bloqueo; Coach no la requiere.

## CompetitionRoster

Cada `TEAM_ENTRY` admite un único `COMPETITION_ROSTER`, creado en `DRAFT`. Su lifecycle es `DRAFT → ACTIVE → CLOSED`; ACTIVE continúa editable mientras la competición sea operativa y CLOSED es de consulta histórica.

Se admiten hasta 15 jugadores ACTIVE y dos técnicos ACTIVE. Los miembros INACTIVE permanecen históricos y no cuentan para los máximos. Jugador y técnico son únicos dentro del roster. Dorsal y capitanía pertenecen al partido, no al roster.

## Función habitual y líbero

`CompetitionRosterPlayer.Role` es una **función habitual** nullable e informativa. Puede utilizarse para mostrar, ordenar o sugerir candidatas, pero no habilita acciones deportivas ni limita la alineación, sustitución o reemplazo de líbero. No existe un máximo de miembros con función habitual `LIBERO`.

La declaración reglamentaria se realiza al abrir el acta y queda en `MATCH_LIBERO`. Sólo esa declaración determina quién puede participar en reemplazos de líbero. Las reglas de declaración y reemplazo se documentan en [05 — Acta electrónica y Scorer](05-match-scorer.md).

## Oficiales

`MATCH_OFFICIAL` vincula un Match con un Referee. Los roles `FIRST_REFEREE`, `SECOND_REFEREE` y `SCORER` son únicos por partido, y un referee no puede ocupar dos roles. Admin los edita antes del inicio; Scorer puede reemplazar el oficial vigente durante el partido sin vaciar el rol.

La apertura exige los tres oficiales y ambos rosters ACTIVE. La convocatoria seleccionada se congela en el MatchSheet y no cambia por ediciones posteriores del roster.
