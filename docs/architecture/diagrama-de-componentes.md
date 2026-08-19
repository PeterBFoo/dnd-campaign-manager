# Diagrama de componentes

- Estado: vigente
- ADR relacionados: [ADR-0001: plataforma y observabilidad](../adr/0001-monorepositorio-y-monolito-modular.md) y [ADR-0002: identidad e invitaciones](../adr/0002-identidad-invitaciones-y-correo-transaccional.md)
- Alcance: componentes lógicos de la plataforma y flujo funcional de identidad e invitaciones

Esta vista describe las responsabilidades y dependencias de la plataforma. No define todavía componentes de dominio ni incorpora información específica de ninguna campaña.

```mermaid
flowchart LR
    user["Usuario<br/>DM o jugador"]

    subgraph frontend["Frontend · Angular"]
        shell["Aplicación y routing"]
        status_ui["Pantalla de estado"]
        status_client["PlatformStatusService<br/>HttpClient"]
        identity_ui["Login · bootstrap<br/>aceptación · panel de invitaciones"]
        identity_client["Auth e Invitation services<br/>interceptor bearer"]

        shell --> status_ui
        status_ui --> status_client
        shell --> identity_ui
        identity_ui --> identity_client
    end

    subgraph edge["Entrada web"]
        nginx["Nginx<br/>estáticos y proxy same-origin"]
    end

    subgraph backend["Backend · ASP.NET Core"]
        middleware["Pipeline HTTP<br/>errores y correlación"]
        status_endpoint["Endpoint de estado<br/>/api/v1/platform/status"]
        health["Health checks<br/>/health/live · /health/ready"]
        persistence["Persistencia<br/>EF Core y Npgsql"]
        invitations["Dominio de invitaciones<br/>tipos · estados · token hash"]
        identity["Identidad y sesiones<br/>password hash · sesión opaca"]
        identity_endpoints["Endpoints de identidad<br/>bootstrap · login · invitaciones"]
        outbox["Worker de outbox<br/>token cifrado · reintentos"]
        email_port["Puerto de correo transaccional"]
        brevo_adapter["Adaptador HTTP de Brevo"]
        telemetry["Instrumentación<br/>OpenTelemetry"]

        middleware --> status_endpoint
        middleware --> health
        status_endpoint --> persistence
        health --> persistence
        invitations --> email_port
        middleware --> identity_endpoints
        identity_endpoints --> identity
        identity_endpoints --> invitations
        identity_endpoints --> persistence
        invitations --> outbox
        outbox --> email_port
        email_port --> brevo_adapter
        middleware --> telemetry
        status_endpoint --> telemetry
        brevo_adapter --> telemetry
    end

    database[("PostgreSQL")]
    postgres_exporter["PostgreSQL exporter"]
    observability["Backend de observabilidad<br/>Collector · Prometheus · Tempo · Loki · Grafana"]
    brevo["Brevo<br/>API transaccional v3"]

    user -->|"HTTP"| nginx
    nginx -->|"recursos estáticos"| frontend
    status_client -->|"GET /api/v1/platform/status"| nginx
    identity_client -->|"JSON · Authorization Bearer"| nginx
    nginx -->|"proxy /api y /health"| middleware
    persistence -->|"conexión SQL"| database
    database -->|"estadísticas operativas"| postgres_exporter
    observability -->|"scrape Prometheus"| postgres_exporter
    telemetry -->|"OTLP: logs, métricas y trazas"| observability
    brevo_adapter -->|"HTTPS · API key solo backend"| brevo
```

## Responsabilidades

| Componente | Responsabilidad | No debe asumir |
|---|---|---|
| Angular | Presentación, navegación y consumo de la API | Autoridad de seguridad o acceso directo a datos |
| Nginx | Servir el build y mantener `/api` y `/health` bajo el mismo origen | Reglas de dominio |
| ASP.NET Core | Contratos HTTP, autorización futura, coordinación y diagnóstico | Renderizado del frontend |
| Dominio de invitaciones | Estados, caducidad de siete días y validación del token de un solo uso | Enviar correo o exponer el token persistido |
| Identidad y sesiones | Validar credenciales, emitir y revocar sesiones opacas | Permitir autorregistro o guardar tokens de sesión en claro |
| Worker de outbox | Entregar invitaciones con reintentos y eliminar el ciphertext tras procesarlo | Conceder permisos o registrar destinatarios y tokens |
| Puerto y adaptador Brevo | Aislar el proveedor y enviar correo transaccional por HTTPS | Decidir si una invitación concede acceso |
| EF Core/Npgsql | Acceso transaccional a PostgreSQL | Definir por adelantado el modelo de dominio |
| PostgreSQL | Persistencia primaria | Exposición directa al navegador |
| PostgreSQL exporter | Exponer métricas operativas de la base de datos a Prometheus | Servir tráfico público o almacenar credenciales en la imagen |
| OpenTelemetry | Instrumentación neutral respecto del proveedor | Registrar secretos o contenido sensible |
| Stack Grafana LGTM | Recibir, almacenar y consultar telemetría local | Ser la topología de producción |

## Reglas de dependencia

- El navegador solo accede a la API a través de Nginx en el recorrido integrado.
- Angular no se conecta directamente a PostgreSQL ni al collector de telemetría.
- El backend es responsable de filtrar la información antes de construir una respuesta HTTP.
- El dominio de invitaciones no depende de Brevo; el adaptador implementa un puerto de aplicación.
- Brevo solo transporta mensajes. El estado funcional de la invitación permanece en el backend y PostgreSQL.
- La persistencia depende de abstracciones de EF Core; la estructura concreta del dominio se decidirá por separado.
- La indisponibilidad del backend de observabilidad no debe impedir que la API atienda tráfico.
