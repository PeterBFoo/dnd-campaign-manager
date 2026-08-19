# D&D Campaign Manager

Aplicación web para preparar y dirigir campañas con estado narrativo persistente y separación estricta entre la información del DM y de los jugadores.

## Estado

La fundación técnica definida por el ADR-0001 está implementada. Los incrementos funcionales posteriores seguirán el flujo Spec-Driven Development descrito en [la guía de especificaciones](docs/specs/README.md).

## Arquitectura actual

- `apps/web`: Angular 22, componentes standalone y modo estricto.
- `apps/api`: C# y ASP.NET Core 10 LTS.
- `apps/api-tests`: pruebas de integración del backend.
- PostgreSQL 18 como persistencia primaria.
- OpenTelemetry para logs, métricas y trazas.
- Grafana LGTM local: Collector, Prometheus, Tempo, Loki y Grafana.
- Nginx como servidor del build Angular y proxy same-origin de `/api`.

La decisión de plataforma está documentada en:

- [ADR-0001: monorepositorio, plataforma y observabilidad](docs/adr/0001-monorepositorio-y-monolito-modular.md)

La arquitectura resultante se representa, de lo lógico a lo físico, en:

1. [Diagrama de componentes](docs/architecture/diagrama-de-componentes.md)
2. [Diagrama de despliegue](docs/architecture/diagrama-de-despliegue.md)

La preparación operativa de credenciales está en [Secretos de despliegue](docs/operations/secretos-de-despliegue.md).
La selección, contenido y uso de los paneles está en [Dashboards de observabilidad](docs/operations/dashboards-de-observabilidad.md).
El procedimiento productivo gratuito está en [Despliegue en Azure](docs/operations/despliegue-azure.md).

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

PostgreSQL aplica `POSTGRES_PASSWORD` solo al inicializar un volumen vacío. Cambiar después el `.env` no modifica la contraseña almacenada: rota la credencial dentro de PostgreSQL o, únicamente si los datos locales son prescindibles, recrea el volumen de desarrollo.

Servicios locales:

| Servicio | Dirección |
|---|---|
| Aplicación Angular | http://localhost:4200 |
| API ASP.NET Core | http://localhost:8080/api/v1/platform/status |
| Liveness | http://localhost:8080/health/live |
| Readiness | http://localhost:8080/health/ready |
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
docker compose build api
docker compose run --rm api-tests
docker compose config --quiet
```

Para verificar un entorno desplegado a través de su entrada web:

```sh
BASE_URL=https://app.example.com sh scripts/smoke-test.sh
```

Si Grafana es accesible desde el ejecutor de la prueba, añade `GRAFANA_URL`.

## Producción gratuita

La topología de referencia utiliza GitHub Pages para Angular, Azure Container Apps Consumption con escala a cero para ASP.NET Core, Neon Free para PostgreSQL y Grafana Cloud Free para telemetría. Las imágenes AMD64 se publican en GHCR y el workflow manual `deploy-azure` despliega únicamente tags inmutables asociados al commit. `deploy-pages` publica el frontend cuando cambia `main`.

La validación productiva inicial solo expone endpoints públicos de plataforma. Antes de publicar autenticación o información de campaña se recuperará una entrada same-origin o se documentará expresamente una alternativa segura.

## Tratamiento de fuentes

Las fuentes y análisis privados se conservan solo para consulta interna. `sources/`, `docs/analysis/` y las decisiones todavía no autorizadas se excluyen de Git y del contexto Docker; no deben publicarse, copiarse al frontend ni servirse como recursos estáticos.

## Licencia

Todavía no se ha seleccionado una licencia para el código. Que el repositorio sea público no concede por sí mismo permiso para reutilizarlo o redistribuirlo.
