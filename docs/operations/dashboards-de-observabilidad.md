# Dashboards de observabilidad

- Estado: implementados y aprovisionados como código
- ADR relacionado: [ADR-0001: plataforma y observabilidad](../adr/ADR-0001-plataforma-y-observabilidad.md)
- Carpeta de Grafana: `D&D Campaign Companion`

Los dashboards cubren las señales necesarias para detectar indisponibilidad, degradación, saturación y problemas de persistencia sin registrar información narrativa ni datos de campaña.

Además de las métricas HTTP, Missions publica `missions.operations` y `missions.operation.duration` con las dimensiones acotadas `missions.operation` (`list`, `create`, `update`, `set_main`, `clear_main`, `delete`) y `missions.outcome`.

Combat publica `combat.operations` y `combat.operation.duration` con dimensiones acotadas: `combat.operation` (`list`, `get`, `get_active`, `create`, `rename`, `add_character`, `add_enemy`, `update_initiative`, `remove_participant`, `resolve_order`, `activate`, `advance`, `adjust_hit_points`, `finish`) y `combat.outcome`. Nombres e identificadores de usuarios, campañas, encuentros y participantes no forman parte de la telemetría. Estas series pueden investigarse desde Explore y correlacionarse con los paneles HTTP existentes.

## Selección

### Plataforma · Disponibilidad y rendimiento

Vista principal para operación y guardias. Aplica el enfoque RED a la API:

- disponibilidad derivada de respuestas sin errores 5xx;
- tasa de errores 5xx;
- peticiones por segundo;
- latencia p50, p95 y p99;
- tráfico y errores por endpoint;
- peticiones y conexiones activas o en cola;
- disponibilidad inmediata de PostgreSQL.

UID estable: `dnd-platform-overview`.

### ASP.NET Core · Runtime y saturación

Vista para investigar degradaciones del proceso:

- memoria de trabajo, heap y memoria comprometida;
- CPU utilizada;
- velocidad de asignación y colecciones del GC;
- tiempo relativo en pausas del GC;
- excepciones por segundo;
- hilos, trabajo en cola y conexiones en espera.

UID estable: `dnd-dotnet-runtime`.

### PostgreSQL · Salud y rendimiento

Vista de infraestructura de datos:

- disponibilidad del servidor;
- conexiones y porcentaje respecto al máximo;
- commits, rollbacks y transacciones por segundo;
- proporción de aciertos de caché;
- tamaño por base de datos;
- deadlocks, bloqueos y escrituras temporales;
- actividad de filas.

UID estable: `dnd-postgresql`.

### Event broker · correo

UID estable: `dnd-event-broker`. Combina métricas OTel de la API (`event.processed`,
`event.failed`, `event.duplicate`, `event.discarded`) con Azure Monitor para el
tópico y la suscripción de Event Grid (`PublishFailCount`, `DeliveryAttemptFailCount`,
`DeadLetteredCount` y latencia). Las alertas recomendadas son: fallos de publicación
o entrega sostenidos durante 5 minutos, cualquier incremento de dead-letter y ausencia
de eventos procesados durante 15 minutos cuando existan invitaciones emitidas.

En producción, Alloy consulta Azure Monitor cada minuto usando su identidad administrada
y exporta a Grafana Cloud las métricas de todos los topics con la etiqueta
`application=dnd-campaign-manager`. Por eso los fallos de entrega, los eventos descartados
y los dead letters aparecen aunque la petición nunca alcance el código de la API. Azure
publica estas métricas con cierto retraso; el colector consulta una ventana de 15 minutos
para no perder puntos todavía en proceso de agregación.

## Referencias utilizadas

La selección toma como base los dashboards de ASP.NET Core y endpoint publicados por el equipo .NET en Grafana, las métricas integradas de ASP.NET Core y la integración oficial de PostgreSQL de Grafana. Los JSON del proyecto son propios porque fijan el datasource `prometheus`, los nombres de servicio y las etiquetas que realmente produce este repositorio.

No se importan dashboards remotos durante el arranque. Esto evita cambios no revisados y permite reproducir exactamente las mismas vistas en cada entorno.

Referencias externas:

- Dashboard Grafana `19924`, **ASP.NET Core**.
- Dashboard Grafana `19925`, **ASP.NET Core Endpoint**.
- Dashboard Grafana `24919`, **PostgreSQL monitoring dashboard**.

## Aprovisionamiento

Los archivos se encuentran en `infra/observability/grafana/dashboards`. Grafana los carga mediante `infra/observability/grafana/dashboards.yaml` cada 30 segundos y no permite guardarlos desde la interfaz. Cualquier cambio debe hacerse en el repositorio y pasar revisión.

En producción, `scripts/publish-grafana-dashboards.sh` publica los mismos JSON en Grafana Cloud mediante una cuenta de servicio. El script descubre el datasource Prometheus real del stack y sustituye los UID locales durante la publicación; esos identificadores de cuenta no se escriben en Git.

En local, Prometheus recibe las métricas OpenTelemetry de la API y consulta `postgres-exporter:9187` según `infra/observability/prometheus.yaml`. En producción, Alloy consulta `127.0.0.1:9187` dentro de la misma réplica que ASP.NET Core y reenvía el resultado a Grafana Cloud por OTLP HTTPS. Ningún exporter publica un puerto al host ni a Internet.

## Interpretación operativa

1. Empezar en **Plataforma · Disponibilidad y rendimiento** para determinar alcance y momento de la degradación.
2. Si aumentan latencia o colas, abrir **ASP.NET Core · Runtime y saturación** para diferenciar presión de CPU, memoria, GC o thread pool.
3. Si readiness falla o la latencia coincide con presión de datos, abrir **PostgreSQL · Salud y rendimiento** y revisar conexiones, caché, bloqueos y rollbacks.
4. Para invitaciones, abrir **Event broker · correo** y separar publicación, entrega, endpoint, Brevo y dead letters antes de revisar una invitación concreta.
5. Usar las trazas de Tempo y los logs correlacionados en Loki para investigar peticiones concretas sin añadir datos sensibles a los dashboards.

## Límites y producción

- La disponibilidad calculada desde tráfico real no sustituye una sonda sintética externa.
- Los umbrales visuales son valores iniciales; deben revisarse con carga real y objetivos SLO aceptados.
- El exporter usa el usuario configurado para PostgreSQL en el entorno actual. Antes de un despliegue compartido debe provisionarse un usuario de monitorización con privilegios mínimos.
- El dashboard PostgreSQL detallado depende de `postgres-exporter`. Cuando Azure Monitor muestra `Replicas == 0`, la ausencia de series es esperada y no constituye una caída. Con una réplica activa, el dashboard debe mostrar `pg_up == 1`; si no hay datos, revisar los contenedores exporter/Alloy y la conexión a Neon.
- Durante la convivencia de fase A pueden existir dos fuentes de métricas. Deben distinguirse por revisión y no prolongarse después de validar la topología conjunta.
- `grafana/otel-lgtm` continúa siendo una solución local, de demostración y pruebas. En producción, los mismos dashboards requieren datasources compatibles con Prometheus, Tempo y Loki, pero la topología y retención deben definirse para el proveedor de destino.

La comprobación posterior al despliegue se automatiza con `scripts/smoke-test.sh`. Exige `BASE_URL` y admite `GRAFANA_URL` cuando Grafana sea accesible desde el ejecutor.
