# 03 — Competiciones y formatos parametrizables

## Objetivo

Permitir representar formatos de competición reutilizables y clonables sin codificar un torneo específico en la aplicación.

## Tablas/entidades de formato acordadas

- `COMPETITION_FORMAT`
- `FORMAT_PHASE`
- `FORMAT_GROUP`
- `FORMAT_QUALIFICATION_RULE`
- `FORMAT_PLAYOFF_SERIES`
- `FORMAT_SERIES_PARTICIPANT_SOURCE`

Estas estructuras permiten modelar ligas/grupos, clasificación y cruces posteriores.

## Playoffs

El modelo debe permitir al menos:

- semifinales;
- final;
- partido por tercer y cuarto puesto.

Los participantes de una serie pueden provenir de posiciones/reglas de etapas anteriores mediante `FORMAT_SERIES_PARTICIPANT_SOURCE`.

## Clonación de competición

Caso de uso representativo:

- Nueva competición: `Apertura B Femenina 2026`
- Divisional: `B Femenina`
- Temporada: `2026`
- Crear estructura:
  - desde cero; o
  - basada en `Clausura C Femenina 2025`.

Al clonar se copia la estructura del formato, no los datos operacionales de la competición modelo.

### Se copia

- formato;
- fases;
- grupos/estructura;
- reglas de clasificación;
- series de playoff;
- fuentes de participantes;
- orden y configuración estructural;
- reglas/ventajas deportivas parametrizadas cuando formen parte del formato.

### No se copia

- equipos;
- TeamEntry;
- fixture;
- partidos;
- fechas;
- resultados;
- jugadores;
- planteles.

## Principio

El formato describe **cómo se juega una competición**. La competición concreta contiene **quiénes juegan, cuándo y qué resultados obtienen**.
