# Base de datos

SQL Server es la base de datos acordada. EF Core Migrations es el mecanismo
operativo de creación y evolución del esquema utilizado por la implementación
.NET.

Los scripts SQL generados durante el modelado permanecen en `db/scripts/` como
referencia del modelo y sus restricciones, conservando su orden y trazabilidad.
No deben ejecutarse como mecanismo paralelo de creación sobre bases administradas
por EF Core Migrations.

Bloques ya modelados conceptualmente:

- entidades base y formatos de competición;
- personas y planteles;
- partido/acta electrónica.

Las migraciones deben preservar las decisiones aprobadas que correspondan al
alcance implementado en cada vertical slice.
