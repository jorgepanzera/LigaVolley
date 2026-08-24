# LigaVolley

Repositorio principal de **Liga Volley**.

## Alcance actual

La solución administrará competiciones de voleibol, el acta electrónica de los partidos y la consulta pública de la información deportiva mediante un backend único y tres frontends:

- **Admin**: gestión y configuración.
- **Scorer**: consola del partido.
- **Public**: consulta pública de competiciones, fixture, resultados, tablas de posiciones e información pública de partidos.

## Stack acordado

- Backend: ASP.NET Core / .NET.
- Base de datos: SQL Server.
- Arquitectura backend: Modular Monolith.
- Frontends: proyectos separados para Admin, Scorer y Public.

La tecnología concreta de frontend se fijará cuando se cierre el diseño técnico de esa capa.

## Estructura

```text
LigaVolley/
├── AGENTS.md
├── README.md
├── docs/
├── db/
│   └── scripts/
├── src/
│   ├── LigaVolley.Api/
│   ├── LigaVolley.Application/
│   ├── LigaVolley.Domain/
│   ├── LigaVolley.Infrastructure/
│   ├── LigaVolley.Admin/
│   ├── LigaVolley.Scorer/
│   └── LigaVolley.Public/
└── tests/
```

## Estado

La implementación backend se realiza incrementalmente por vertical slices. El
primer slice disponible contiene los catálogos administrativos `Season` y
`Division`, con persistencia SQL Server administrada mediante EF Core Migrations.

