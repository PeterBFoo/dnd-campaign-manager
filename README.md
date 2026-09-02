# D&D Campaign Manager

Aplicación web para preparar y dirigir campañas con estado narrativo persistente y separación estricta entre la información del DM y de los jugadores.

## Estado

La aplicación dispone de identidad, campañas, personajes, bitácora, misiones, combate, catálogo de módulos y mapas privados reutilizables para el DM. El [roadmap de producto](docs/roadmap/product-roadmap.md) mantiene el alcance completo y cada capacidad se implementa mediante un incremento independiente siguiendo [la guía de especificaciones](docs/specs/README.md).

## Arquitectura actual

- `apps/web`: Angular 22 modular; composition root y shell separados de `Access`, `AdventureCatalog`, `Campaigns`, `Characters`, `Combat`, `Journal`, `Missions` y `Platform`, con primitivas técnicas en `shared`.
- `apps/api`: aplicación ASP.NET Core 10 LTS; contiene el host y sus módulos desplegados conjuntamente.
- `apps/api/Modules/Access/DndCampaign.Modules.Access`: módulo de acceso; un único proyecto con capas internas.
- `apps/api/Modules/Campaigns/DndCampaign.Modules.Campaigns`: campañas, DM único, consultas autorizadas y persistencia en esquema propio.
- `apps/api/Modules/AdventureCatalog/DndCampaign.Modules.AdventureCatalog`: módulos, portadas, mapas reutilizables, imágenes privadas y proyecciones DM.
- `apps/api/Modules/Characters`, `Combat`, `Journal` y `Missions`: capacidades de juego aisladas por campaña.
- `tests/Modules/Access/DndCampaign.Modules.Access.Tests`: tests unitarios, integración, componente y arquitectura propios de Access.
- `tests/Modules/Campaigns/DndCampaign.Modules.Campaigns.Tests`: tests de dominio, aplicación y arquitectura propios de Campaigns.
- `tests/Modules/AdventureCatalog/DndCampaign.Modules.AdventureCatalog.Tests`: dominio, persistencia y almacenamiento privado del catálogo y sus mapas.
- `tests/DndCampaign.ArchitectureTests`: fitness functions globales entre módulos y host.
- PostgreSQL 18 como persistencia primaria.
- OpenTelemetry para logs, métricas y trazas.
- Grafana LGTM local: Collector, Prometheus, Tempo, Loki y Grafana.
- Nginx como servidor del build Angular y proxy same-origin de `/api`.

La decisión de plataforma está documentada en:

- [ADR-0001: monorepositorio, plataforma y observabilidad](docs/adr/0001-monorepositorio-y-monolito-modular.md)
- [ADR-0002: identidad, invitaciones y correo transaccional](docs/adr/0002-identidad-invitaciones-y-correo-transaccional.md)
- [ADR-0003: bootstrap, sesiones y flujo funcional](docs/adr/0003-bootstrap-sesiones-y-flujo-de-invitaciones.md)
- [ADR-0004: arquitectura modular, CQRS y límites](docs/adr/0004-arquitectura-modular-cqrs-y-limites-de-dependencia.md)
- [ADR-0005: frontend modular por capacidades](docs/adr/0005-frontend-modular-por-capacidades.md)
- [ADR-0006: campañas, acceso e invitaciones](docs/adr/0006-campanas-acceso-e-invitaciones.md)

La arquitectura resultante se representa, de lo lógico a lo físico, en:

1. [Diagrama de componentes](docs/architecture/diagrama-de-componentes.md)
2. [Diagrama de despliegue](docs/architecture/diagrama-de-despliegue.md)

La preparación operativa de credenciales está en [Secretos de despliegue](docs/operations/secretos-de-despliegue.md).
El orden y la reversibilidad de los cambios de datos están en [Migraciones de base de datos](docs/operations/migraciones-de-base-de-datos.md).
La selección, contenido y uso de los paneles está en [Dashboards de observabilidad](docs/operations/dashboards-de-observabilidad.md).
El procedimiento productivo con coste acotado está en [Despliegue en Azure](docs/operations/despliegue-azure.md).

## Requisitos

- Node.js 24.15 o posterior.
- pnpm 11.19.
- Docker con Docker Compose.
- Opcional: SDK de .NET 10 para ejecutar el backend fuera de Docker.

## Arranque integrado

```sh
cp .env.example .env
pnpm install
docker compose up --build
```

`.env` está ignorado por Git y contiene únicamente credenciales locales. Sustituye los valores de ejemplo antes del primer arranque; nunca reutilices ese archivo en un despliegue.

En el primer acceso, Angular mostrará el alta inicial. Utiliza el valor local de `IDENTITY_BOOTSTRAP_TOKEN`; después de crear la primera cuenta, el endpoint queda cerrado y el acceso continúa mediante credenciales e invitaciones.

PostgreSQL aplica `POSTGRES_PASSWORD` solo al inicializar un volumen vacío. Cambiar después el `.env` no modifica la contraseña almacenada: rota la credencial dentro de PostgreSQL o, únicamente si los datos locales son prescindibles, recrea el volumen de desarrollo. Las imágenes de personajes, portadas y mapas se guardan en Blob/Azurite privado y siempre se leen mediante endpoints autorizados.

Servicios locales:

| Servicio | Dirección |
|---|---|
| Aplicación Angular | http://localhost:4200 |
| API ASP.NET Core | http://localhost:8080/api/v1/platform/status |
| Liveness | http://localhost:8080/health/live |
| Readiness | http://localhost:8080/health/ready |
| Swagger UI — solo Development | http://localhost:8080/swagger |
| Grafana | http://localhost:3000 |
| PostgreSQL | localhost:5432 |
| OTLP gRPC / HTTP | localhost:4317 / localhost:4318 |

Las credenciales locales de Grafana se leen desde `.env`; los valores de ejemplo no deben reutilizarse en un entorno compartido o desplegado.

Los dashboards del proyecto se aprovisionan automáticamente en la carpeta **D&D Campaign Companion** de Grafana.

Accesos directos:

- [Disponibilidad y rendimiento](http://localhost:3000/d/dnd-platform-overview)
- [ASP.NET Core y runtime](http://localhost:3000/d/dnd-dotnet-runtime)
- [PostgreSQL](http://localhost:3000/d/dnd-postgresql)

## Desarrollo rápido del frontend

Con la API disponible en el puerto 8080:

```sh
pnpm dev:web
```

Angular redirige `/api` y `/health` mediante `apps/web/proxy.conf.json`.

## Verificación

```sh
pnpm test:web
pnpm build
docker compose build web
docker compose build api
docker compose run --rm api-tests
docker compose config --quiet
```

Para verificar un entorno desplegado a través de su entrada web:

```sh
BASE_URL=https://app.example.com sh scripts/smoke-test.sh
```

Si Grafana es accesible desde el ejecutor de la prueba, añade `GRAFANA_URL`.

## Producción con coste acotado

La topología de referencia utiliza GitHub Pages para Angular y una Azure Container App Consumption conjunta para ASP.NET Core, PostgreSQL exporter y Alloy. La revisión escala a cero y solo ASP.NET Core recibe ingress. Azure Blob Storage conserva imágenes privadas, Neon Free proporciona PostgreSQL y Grafana Cloud recibe telemetría. Las imágenes AMD64 de API y Alloy se publican en GHCR y `deploy-azure` despliega únicamente tags inmutables asociados al commit. `deploy-pages` publica el frontend cuando cambia `main`.

El ADR-0003 autoriza temporalmente la autenticación entre orígenes mediante sesiones bearer opacas, `sessionStorage` y una política CORS exacta. Recuperar una entrada same-origin continúa siendo la evolución preferente antes de ampliar el volumen de información privada.

## Tratamiento de fuentes

Las fuentes y análisis privados se conservan solo para consulta interna. `sources/`, `docs/analysis/` y las decisiones todavía no autorizadas se excluyen de Git y del contexto Docker; no deben publicarse, copiarse al frontend ni servirse como recursos estáticos.

## Licencia

Todavía no se ha seleccionado una licencia para el código. Que el repositorio sea público no concede por sí mismo permiso para reutilizarlo o redistribuirlo.
