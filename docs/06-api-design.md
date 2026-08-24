# 06 — Lineamientos iniciales de API

## Estado

La arquitectura de la API está acordada a nivel de módulos y superficies. El catálogo definitivo de endpoints y DTOs se cerrará módulo por módulo antes de implementar cada bloque.

## Superficies

### Admin

Prefijo obligatorio:

`/api/admin`

Responsabilidades previstas:

- CRUD/consulta de entidades maestras;
- personas y roles;
- planteles;
- competiciones;
- formatos;
- clonación de estructura de competición;
- inscripciones de equipos;
- fixture y configuración de partidos.

### Scorer

Prefijo obligatorio:

`/api/scorer`

Responsabilidades previstas:

- abrir/consultar acta;
- obtener contexto operativo del partido;
- alineaciones;
- puntos/eventos;
- rotaciones;
- sustituciones;
- líberos;
- timeouts;
- fin de set;
- correcciones;
- cierre del partido;
- sincronización futura.

### Public

Prefijo obligatorio:

`/api/public`

Responsabilidades previstas:

- consultar competiciones publicadas;
- consultar fixture y calendario;
- consultar resultados;
- consultar tablas de posiciones;
- consultar información pública de partidos;
- consultar únicamente otros datos deportivos que tengan una regla explícita de publicación.

Esta superficie será consumida por `LigaVolley.Public` y es de solo consulta. No expone automáticamente toda la información disponible en la base de datos. Como mínimo se contemplan competiciones visibles/publicadas, equipos participantes, fixture, resultados confirmados, tablas de posiciones e información pública de partidos. Planteles, personas, oficiales u otros datos requieren una decisión explícita de visibilidad/publicación antes de incorporarse.

## DTOs

No usar entidades EF/Domain directamente como contratos HTTP.

Separar DTOs según caso de uso y consumidor. Un mismo concepto puede tener:

- DTO de administración;
- DTO optimizado para Scorer;
- DTO de consulta pública.

## Casos de uso

Los endpoints deben delegar en casos de uso de Application. Los controllers/endpoints no contienen lógica de negocio.

## Clonación

La clonación debe ser un caso de uso explícito de Competition/Application, no una copia genérica de filas de base de datos.

Su contrato debe permitir identificar:

- competición destino/nueva competición;
- competición modelo;
- metadatos propios de la nueva competición.

La implementación debe copiar solo las estructuras permitidas según `03-competition-formats.md`.
