# Diagrama de componentes

- Estado: vigente
- ADR relacionado: [ADR-0001: plataforma y observabilidad](../adr/ADR-0001-plataforma-y-observabilidad.md)
- Alcance: componentes lógicos de la plataforma inicial

Esta vista describe las responsabilidades y dependencias de la plataforma. No define todavía componentes de dominio ni incorpora información específica de ninguna campaña.

```mermaid
flowchart LR
    user["Usuario<br/>DM o jugador"]

    subgraph frontend["Frontend · Angular"]
        shell["Aplicación y routing"]
        status_ui["Pantalla de estado"]
        status_client["PlatformStatusService<br/>HttpClient"]

        shell --> status_ui
        status_ui --> status_client
    end

    subgraph edge["Entrada web"]
        nginx["Nginx<br/>estáticos y proxy same-origin"]
    end

    subgraph backend["Backend · ASP.NET Core"]
        middleware["Pipeline HTTP<br/>errores y correlación"]
        status_endpoint["Endpoint de estado<br/>/api/v1/platform/status"]
        health["Health checks<br/>/health/live · /health/ready"]
        persistence["Persistencia<br/>EF Core y Npgsql"]
        telemetry["Instrumentación<br/>OpenTelemetry"]

        middleware --> status_endpoint
        middleware --> health
        status_endpoint --> persistence
        health --> persistence
        middleware --> telemetry
        status_endpoint --> telemetry
    end

    database[("PostgreSQL")]
    postgres_exporter["PostgreSQL exporter"]
    observability["Backend de observabilidad<br/>Collector · Prometheus · Tempo · Loki · Grafana"]

    user -->|"HTTP"| nginx
    nginx -->|"recursos estáticos"| frontend
    status_client -->|"GET /api/v1/platform/status"| nginx
    nginx -->|"proxy /api y /health"| middleware
    persistence -->|"conexión SQL"| database
    database -->|"estadísticas operativas"| postgres_exporter
    observability -->|"scrape Prometheus"| postgres_exporter
    telemetry -->|"OTLP: logs, métricas y trazas"| observability
```

## Responsabilidades

| Componente | Responsabilidad | No debe asumir |
|---|---|---|
| Angular | Presentación, navegación y consumo de la API | Autoridad de seguridad o acceso directo a datos |
| Nginx | Servir el build y mantener `/api` y `/health` bajo el mismo origen | Reglas de dominio |
| ASP.NET Core | Contratos HTTP, autorización futura, coordinación y diagnóstico | Renderizado del frontend |
| EF Core/Npgsql | Acceso transaccional a PostgreSQL | Definir por adelantado el modelo de dominio |
| PostgreSQL | Persistencia primaria | Exposición directa al navegador |
| PostgreSQL exporter | Exponer métricas operativas de la base de datos a Prometheus | Servir tráfico público o almacenar credenciales en la imagen |
| OpenTelemetry | Instrumentación neutral respecto del proveedor | Registrar secretos o contenido sensible |
| Stack Grafana LGTM | Recibir, almacenar y consultar telemetría local | Ser la topología de producción |

## Reglas de dependencia

- El navegador solo accede a la API a través de Nginx en el recorrido integrado.
- Angular no se conecta directamente a PostgreSQL ni al collector de telemetría.
- El backend es responsable de filtrar la información antes de construir una respuesta HTTP.
- La persistencia depende de abstracciones de EF Core; la estructura concreta del dominio se decidirá por separado.
- La indisponibilidad del backend de observabilidad no debe impedir que la API atienda tráfico.
