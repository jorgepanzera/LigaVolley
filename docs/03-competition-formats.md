# 03 — Competiciones y formatos parametrizables

## Objetivo

Permitir representar formatos de competición reutilizables y clonables sin codificar un torneo específico en la aplicación.

El principio central es:

> El formato describe **cómo se juega una competición**. La competición concreta contiene **quiénes juegan, cuándo y qué resultados obtienen**.

## Relación obligatoria de Competition

Toda `Competition` debe estar asociada obligatoriamente a:

- una `Season`;
- una `Divisional`;
- un `CompetitionFormat`.

No se considera válido crear una `Competition` sin formato.

La opción funcional "desde cero" significa que el administrador crea o selecciona previamente un `CompetitionFormat` y luego crea la competición a partir de él.

## Tablas/entidades de formato acordadas

El agregado de formato incluye, según el modelo SQL vigente:

- `COMPETITION_FORMAT`
- `FORMAT_PHASE`
- `FORMAT_GROUP`
- `FORMAT_QUALIFICATION_RULE`
- `FORMAT_PLAYOFF_SERIES`
- `FORMAT_SERIES_PARTICIPANT_SOURCE`
- `FORMAT_SCORING_RULE`
- `FORMAT_TIEBREAK_RULE`
- `FORMAT_MOVEMENT_RULE`

Estas estructuras permiten modelar:

- fases de liga/round robin;
- subdivisiones o grupos;
- reglas de clasificación;
- series eliminatorias;
- fuentes/orígenes de participantes de series posteriores;
- puntuación de tabla;
- criterios de desempate;
- reglas parametrizadas de ascenso y descenso.

## Fases y grupos

Un formato puede contener varias fases ordenadas mediante `sequence`.

Los tipos/roles concretos deben respetar el modelo SQL vigente. Conceptualmente se contemplan al menos:

- fase regular;
- fase campeonato;
- fase permanencia/descenso;
- semifinales;
- tercer puesto;
- final.

Los grupos permiten subdividir una fase, por ejemplo:

- `CHAMPIONSHIP`;
- `RELEGATION`;
- otros grupos futuros.

El formato puede determinar además la cantidad de ruedas y el modo de generación del fixture.

## Clasificación entre fases

`FORMAT_QUALIFICATION_RULE` describe cómo los participantes pasan desde una fase/grupo hacia otra fase, grupo o serie.

La API de administración debe trabajar con referencias lógicas por `code` dentro de una definición de formato, evitando requerir IDs persistentes que todavía no existen al crear el agregado completo.

Ejemplos conceptuales:

- mejores posiciones de fase regular → grupo campeonato;
- últimas posiciones → grupo descenso;
- posiciones 1 y 4 → semifinal 1;
- posiciones 2 y 3 → semifinal 2.

## Playoffs

El modelo debe permitir al menos:

- semifinales;
- final;
- partido por tercer y cuarto puesto.

La ventaja deportiva de semifinales debe modelarse como victorias iniciales de la serie y no mediante partidos ficticios.

Ejemplo:

- SF1: 1.º vs 4.º, `team1_initial_wins = 1`, `wins_required = 2`;
- SF2: 2.º vs 3.º, `team1_initial_wins = 1`, `wins_required = 2`.

Los participantes de series posteriores pueden provenir de otras series mediante `FORMAT_SERIES_PARTICIPANT_SOURCE`.

Ejemplo:

- Final side 1 ← ganador SF1;
- Final side 2 ← ganador SF2;
- Tercer puesto side 1 ← perdedor SF1;
- Tercer puesto side 2 ← perdedor SF2.

## Reglas de puntuación y desempate

El formato puede parametrizar la puntuación de tabla mediante `FORMAT_SCORING_RULE`.

La configuración vigente para los formatos principales de la liga se representa mediante reglas y contempla:

- 3-0 → 2 puntos ganador / 1 perdedor;
- 3-1 → 2 puntos ganador / 1 perdedor;
- 3-2 → 2 puntos ganador / 1 perdedor.

Otros formatos pueden definir repartos diferentes, incluido 3/0 para 3-0 y 3-1
y 2/1 para 3-2. Ningún cálculo de standings debe asumir un reparto fijo.

Los criterios de desempate se parametrizan mediante `FORMAT_TIEBREAK_RULE`. El modelo contempla criterios como:

- `TABLE_POINTS`;
- `MATCH_WINS`;
- `SET_RATIO`;
- `POINT_RATIO`;
- `HEAD_TO_HEAD`.

El orden concreto se determina mediante `sequence`.

## Ascensos y descensos

`FORMAT_MOVEMENT_RULE` permite representar movimientos de nivel posteriores a una competición.

Conceptualmente:

- `PROMOTION` con `target_level_delta = -1` significa ascender a una divisional superior;
- `RELEGATION` con `target_level_delta = +1` significa descender a una divisional inferior.

La regla debe poder indicar que sólo se aplica cuando existe una divisional destino válida.

Las reglas funcionales finas de ascensos entre Apertura/Clausura o temporadas continúan sujetas a definición explícita cuando corresponda.

## Creación de una Competition

Existen dos modos técnicos de creación:

### FROM_FORMAT

Se selecciona directamente un `CompetitionFormat` existente.

La creación debe:

1. validar `Season`;
2. validar `Divisional`;
3. validar `CompetitionFormat`;
4. crear `COMPETITION` en estado inicial `DRAFT`;
5. instanciar nuevas `COMPETITION_PHASE` a partir de `FORMAT_PHASE`;
6. instanciar nuevos `PHASE_GROUP` a partir de `FORMAT_GROUP`;
7. instanciar nuevas `PLAYOFF_SERIES` a partir de `FORMAT_PLAYOFF_SERIES`;
8. instanciar las fuentes de participantes de series correspondientes.

No se crean en ese momento:

- `TEAM_ENTRY`;
- integrantes de grupos dependientes de clasificación;
- fixture operativo;
- `MATCH`;
- resultados;
- planteles.

### FROM_COMPETITION

Se selecciona una competición existente como modelo de estructura.

La nueva competición:

1. obtiene el `CompetitionFormat` de la competición modelo;
2. referencia ese mismo formato reutilizable;
3. crea sus propias instancias de fases, grupos y series usando la definición del formato.

La creación basada en otra competición **no duplica físicamente el `CompetitionFormat`**.

## Clonación física de CompetitionFormat

La clonación de un formato es un caso de uso independiente de crear una competición basada en otra.

`CloneCompetitionFormat` crea un nuevo agregado de formato con:

- nueva identidad;
- nuevo `code`;
- nuevo nombre;
- copia independiente de fases, grupos, reglas, series y demás configuración estructural.

Se utiliza cuando se quiere crear una variante del formato existente.

## Inmutabilidad estructural práctica

Un `CompetitionFormat` que ya haya sido utilizado por una competición operativa no debe modificarse estructuralmente.

Regla inicial:

- formato nuevo/no utilizado: se permiten cambios estructurales;
- formato ya utilizado por una competición que dejó `DRAFT`: no se permiten cambios estructurales;
- para introducir cambios estructurales se debe clonar el formato y modificar el clon;
- los cambios descriptivos o de activación pueden permitirse según el caso de uso.

El objetivo es preservar el significado histórico del formato usado por competiciones ya configuradas o disputadas sin introducir versionado formal en la primera versión.

## Inscripción de equipos y rango permitido

`COMPETITION_FORMAT` define `min_teams` y `max_teams`.

Durante la inscripción de equipos:

- no se debe bloquear por no alcanzar todavía `min_teams`;
- sí se debe impedir superar `max_teams`.

Antes de generar el fixture inicial debe validarse:

`min_teams <= cantidad de equipos válidos <= max_teams`.

## Instancias de grupos dependientes de clasificación

La estructura de fases y grupos puede existir desde la creación de la competición, pero los participantes concretos de un grupo (`PHASE_GROUP_ENTRY`) se incorporan cuando se resuelve la fase/regla que determina la clasificación.

Ejemplo:

`Regular → QualificationRules → Championship/Relegation → PHASE_GROUP_ENTRY`

No debe modelarse como un CRUD administrativo libre si puede resolverse mediante reglas del formato.

## Fixture y formato

El frontend no debe volver a enviar reglas estructurales que ya pertenecen al formato al generar un fixture.

El generador debe leer de la estructura instanciada, entre otros:

- cantidad de ruedas;
- modo de fixture;
- grupos;
- estructura de playoffs.

Para modos aleatorios/balanceados puede aceptarse un `randomSeed` opcional para reproducibilidad y pruebas.

No es necesario crear partidos de playoff con participantes nulos desde el comienzo. La estrategia inicial será crear partidos de fases posteriores cuando sus participantes estén resueltos y puedan programarse.

## Progresión de fases y clasificación

La progresión deportiva se separa conceptualmente en tres responsabilidades:

`resultado deportivo → progresión → generación incremental de fixture`

Los resultados de `MATCH` alimentan la tabla de posiciones. La clasificación final de una fase se obtiene aplicando las reglas parametrizadas de puntuación y desempate. Luego `FORMAT_QUALIFICATION_RULE` determina qué `TEAM_ENTRY` avanza hacia grupos o series posteriores.

Para fases de liga o grupos (`ROUND_ROBIN` / `GROUP_STAGE`), el cierre es un caso de uso administrativo explícito. No se debe hacer que el último partido finalizado dispare silenciosamente toda la progresión de la competición.

El cierre de una fase debe:

1. validar que todos los partidos requeridos estén resueltos;
2. obtener la clasificación definitiva;
3. aplicar las `QualificationRules`;
4. materializar participantes en `PHASE_GROUP_ENTRY` o lados de `PLAYOFF_SERIES`;
5. generar incrementalmente los partidos de la etapa siguiente que ya tengan participantes resueltos;
6. actualizar estados de fase/serie de manera transaccional e idempotente.

Un partido `CANCELLED` no se considera automáticamente resuelto para permitir el cierre de una fase. La consecuencia deportiva de una cancelación requiere una regla explícita posterior.

### Preview antes de completar una fase

Antes del cierre debe poder consultarse una previsualización sin efectos persistentes que muestre:

- si la fase puede cerrarse;
- bloqueos existentes;
- tabla final proyectada;
- clasificados que resultarán de cada regla;
- grupos/series que quedarán preparados;
- fixture posterior que podría generarse.

## Semántica de TOP_HALF y BOTTOM_HALF

`TOP_HALF` y `BOTTOM_HALF` deben cubrir conjuntamente a todos los participantes ordenados de la fuente.

Si `N` es par:

- `TOP_HALF = N / 2`;
- `BOTTOM_HALF = N / 2`.

Si `N` es impar, el participante adicional queda en Championship/mitad superior:

- `TOP_HALF = (N + 1) / 2`;
- `BOTTOM_HALF = (N - 1) / 2`.

Ejemplos:

| Equipos | TOP_HALF / Championship | BOTTOM_HALF / Relegation |
|---:|---:|---:|
| 9 | 5 | 4 |
| 10 | 5 | 5 |
| 11 | 6 | 5 |
| 12 | 6 | 6 |

La selección se aplica sobre el orden definitivo producido por las reglas de puntuación y desempate del formato.

## Semántica de PHASE_GROUP_ENTRY

`PHASE_GROUP_ENTRY.source_position` conserva la posición de clasificación obtenida en la fase/grupo origen.

Ejemplo: un equipo que finaliza 1.º en Regular y clasifica a Championship mantiene `source_position = 1`.

`seed` es un concepto independiente y sólo debe utilizarse cuando una regla o modo de fixture requiera un orden específico dentro del nuevo grupo.

## CarryOverMode soportado en v1

El modelo puede contener los valores:

- `NONE`;
- `ALL`;
- `QUALIFIED_ONLY`.

Sin embargo, en v1 el motor de progresión soporta operativamente sólo:

`CarryOverMode.NONE`

`ALL` y `QUALIFIED_ONLY` permanecen modelados pero deben rechazarse para formatos operativos hasta definir explícitamente cómo se trasladan resultados, puntos de tabla, victorias, set ratio, point ratio y demás efectos deportivos.

Codex no debe inferir esa semántica.

## Progresión de fases con grupos

Cuando una fase contiene varios grupos, la unidad de cierre en v1 es la `COMPETITION_PHASE` completa.

Para completar la fase:

- todos los grupos requeridos deben tener sus partidos resueltos;
- no se habilita en v1 un `CompletePhaseGroup` independiente.

Ejemplo:

`SECOND_STAGE = CHAMPIONSHIP + RELEGATION`

Aunque Championship haya terminado, la fase no se completa hasta que también esté resuelto Relegation. Recién entonces se aplican las reglas que dependan de esa fase.

## Generación incremental del fixture

El fixture se genera conforme se conocen los participantes reales de cada etapa.

Flujo conceptual:

`fixture inicial → fase regular → clasificación → segunda fase → semifinales → final/tercer puesto`

No se deben crear por defecto partidos futuros con participantes nulos.

Para grupos de segunda fase:

1. completar la fase previa;
2. aplicar QualificationRules;
3. poblar `PHASE_GROUP_ENTRY`;
4. generar partidos de cada grupo según `rounds` y `fixture_mode`.

Para playoffs:

1. resolver ambos participantes de la serie;
2. cambiar la serie a `READY`;
3. generar únicamente el siguiente partido real requerido.

## Series de playoff

Semifinal, Final y Tercer Puesto se representan siempre mediante `PLAYOFF_SERIES`.

Un partido único se representa con:

- `wins_required = 1`;
- `team1_initial_wins = 0`;
- `team2_initial_wins = 0`.

Una serie al mejor de tres se representa normalmente con `wins_required = 2`.

Las semifinales con ventaja deportiva se representan con victorias iniciales, por ejemplo:

- `wins_required = 2`;
- favorito: `initial_wins = 1`;
- rival: `initial_wins = 0`.

No se crean partidos ficticios para representar ventajas.

### Generación de partidos dentro de una serie

Después de cada partido real finalizado:

`teamWins = initialWins + partidos reales ganados`

Si un participante alcanza `wins_required`, la serie pasa a `FINISHED` y se determina ganador/perdedor.

Si ninguno alcanzó `wins_required`, se crea el siguiente `MATCH` de la serie.

Por tanto, una semifinal con ventaja 1-0 y `wins_required = 2` puede requerir uno o dos partidos reales; no se crean ambos obligatoriamente desde el comienzo.

## Estados de PLAYOFF_SERIES

Semántica v1:

- `PENDING`: todavía no están resueltos ambos participantes;
- `READY`: ambos participantes están resueltos y todavía no comenzó un partido real;
- `IN_PROGRESS`: la serie comenzó y todavía no existe ganador;
- `FINISHED`: un participante alcanzó `wins_required`;
- `CANCELLED`: cancelación administrativa; su consecuencia deportiva queda pendiente de reglas específicas.

## Resolución de series posteriores

En la definición del agregado, `FORMAT_SERIES_PARTICIPANT_SOURCE` describe los participantes dependientes de una serie previa. Su futura instancia operativa se resolverá al crear y progresar una `Competition`.

Ejemplo:

- ganador SF1 → Final side 1;
- ganador SF2 → Final side 2;
- perdedor SF1 → Third Place side 1;
- perdedor SF2 → Third Place side 2.

La resolución no depende del orden cronológico de finalización de SF1 y SF2. Una serie destino permanece `PENDING` mientras falte uno de sus participantes y pasa a `READY` cuando ambos estén resueltos.

Esta progresión de playoffs es automática a partir del resultado concluyente de una serie y no requiere un cierre administrativo adicional de cada serie.

## Estados y cierre de Competition

Regla v1:

- `DRAFT → SCHEDULED`: cuando se cumplen las invariantes de preparación y existe fixture inicial válido;
- `SCHEDULED → IN_PROGRESS`: automáticamente cuando comienza el primer partido oficial;
- `IN_PROGRESS → FINISHED`: mediante un caso de uso administrativo explícito `CompleteCompetition`;
- `DRAFT / SCHEDULED → CANCELLED`: cancelación administrativa inicial.

`CompleteCompetition` sólo puede ejecutarse cuando todas las fases obligatorias y series requeridas estén resueltas.

La Competition no se cierra automáticamente al terminar la final porque pueden existir tercer puesto, grupo de descenso, partidos pendientes o validaciones administrativas.

Las reglas de movimiento (`FORMAT_MOVEMENT_RULE`) pueden utilizarse para calcular/promostrar ascensos y descensos resultantes, pero v1 no crea automáticamente inscripciones en una competición futura.

## Regeneración de fixture

Regla conservadora v1:

- puede regenerarse un fixture de un ámbito mientras ningún partido de ese ámbito haya comenzado o finalizado;
- si existe algún partido `IN_PROGRESS` o `FINISHED`, la regeneración debe rechazarse;
- regenerar reemplaza los emparejamientos generados y puede invalidar/perder fecha y sede previamente programadas;
- el frontend debe recibir una advertencia/preview cuando existan programaciones que se perderían.

La programación (`fecha/hora/sede`) permanece conceptualmente separada de la generación de emparejamientos.
