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

La configuración estándar actualmente contemplada es:

- 3-0 → 3 puntos ganador / 0 perdedor;
- 3-1 → 3 puntos ganador / 0 perdedor;
- 3-2 → 2 puntos ganador / 1 perdedor.

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
