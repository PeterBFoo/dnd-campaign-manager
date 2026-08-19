# Diagrama de despliegue

- Estado: vigente
- ADR relacionado: [ADR-0001: plataforma y observabilidad](../adr/ADR-0001-plataforma-y-observabilidad.md)
- Vista lógica relacionada: [diagrama de componentes](diagrama-de-componentes.md)
- Alcance: entorno local integrado definido en `compose.yaml`

Esta vista muestra la distribución física de los componentes del primer incremento. Es una topología de desarrollo e integración local, no un diseño de producción.

```mermaid
flowchart TB
    browser["Navegador del usuario"]
    developer["Desarrollador"]

    subgraph host["Equipo local"]
        compose["Docker Compose"]

        subgraph network["Red interna de Docker Compose"]
            web["Contenedor web<br/>Nginx + build Angular<br/>puerto interno 80"]
            api["Contenedor api<br/>ASP.NET Core 10<br/>puerto interno 8080"]
            postgres["Contenedor postgres<br/>PostgreSQL 18<br/>puerto interno 5432"]
            postgres_exporter["Contenedor postgres-exporter<br/>métricas Prometheus<br/>puerto interno 9187"]
            lgtm["Contenedor observability<br/>Grafana OpenTelemetry LGTM<br/>OTLP 4317/4318 · Grafana 3000"]
            tests["Contenedor api-tests<br/>perfil test"]
        end

        postgres_volume[("Volumen<br/>postgres18-data")]
        telemetry_volume[("Volumen<br/>observability-data")]
    end

    browser -->|"HTTP localhost:4200"| web
    web -->|"proxy HTTP /api y /health"| api
    api -->|"TCP 5432"| postgres
    api -->|"OTLP gRPC 4317"| lgtm
    postgres_exporter -->|"consultas de monitorización"| postgres
    lgtm -->|"scrape 9187"| postgres_exporter

    developer -->|"docker compose"| compose
    compose --> web
    compose --> api
    compose --> postgres
    compose --> postgres_exporter
    compose --> lgtm
    compose -.->|"perfil test"| tests
    tests -->|"TCP 5432"| postgres

    browser -.->|"diagnóstico localhost:8080"| api
    browser -.->|"Grafana localhost:3000"| lgtm
    postgres --> postgres_volume
    lgtm --> telemetry_volume
```

## Unidades de despliegue

| Servicio | Imagen o build | Puerto publicado | Dependencias de arranque | Persistencia |
|---|---|---:|---|---|
| `web` | `apps/web/Dockerfile` | `4200 → 80` | API saludable | Sin estado |
| `api` | `apps/api/Dockerfile`, target `runtime` | `8080 → 8080` | PostgreSQL y observabilidad saludables | Mediante PostgreSQL |
| `postgres` | `postgres:18-alpine` | `5432 → 5432` | Ninguna | `postgres18-data`, montado en `/var/lib/postgresql` |
| `postgres-exporter` | `quay.io/prometheuscommunity/postgres-exporter:v0.20.1` | No publicado | PostgreSQL saludable | Sin estado |
| `observability` | `grafana/otel-lgtm:0.30.0` | `3000 → 3000`, `4317 → 4317`, `4318 → 4318` | Ninguna | `observability-data` |
| `api-tests` | `apps/api/Dockerfile`, target `tests` | No publicado | PostgreSQL saludable | Efímera |

## Flujo de una petición

1. El navegador solicita la aplicación a `localhost:4200`.
2. Nginx sirve el build Angular.
3. Las llamadas Angular a `/api` vuelven al mismo origen y Nginx las redirige al contenedor `api`.
4. ASP.NET Core consulta PostgreSQL cuando el endpoint lo requiere.
5. La API exporta logs, métricas y trazas por OTLP al contenedor de observabilidad.
6. Grafana permite consultar la telemetría local desde `localhost:3000`.

## Restricciones operativas

- Los puertos publicados son conveniencias del entorno local y deben revisarse para otros entornos.
- Las credenciales por defecto solo son válidas para desarrollo local.
- Las fuentes privadas y la documentación excluida por Git no forman parte del contexto de las imágenes.
- Una topología de producción deberá concretar red, TLS, identidad, secretos, alta disponibilidad, copias de seguridad y retención de telemetría antes de desplegarse.
