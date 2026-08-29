# ADR-0009: Broker de eventos push para correo y observabilidad en Grafana

- Estado: Aceptado
- Fecha: 2026-08-29
- Decisores: equipo del proyecto
- Alcance: entrega asíncrona de invitaciones, escala de Azure Container Apps y operación
- Especificación: [spec 021](../specs/021-broker-eventos-correo/spec.md)
- Sustituye parcialmente: [ADR-0002](0002-identidad-invitaciones-y-correo-transaccional.md), únicamente en el mecanismo de entrega del outbox

## Contexto

Access confirma la invitación y su mensaje de outbox en PostgreSQL, pero un `BackgroundService` consulta la tabla cada cinco segundos cuando está vacía. Esto mantiene una réplica activa y genera lecturas periódicas incluso sin correos pendientes. El objetivo es reaccionar a mensajes duraderos sin perder la garantía de que una invitación confirmada tiene trabajo de entrega.

La topología ya usa Azure Container Apps, identidad administrada, Azure Blob y Grafana Cloud/OTLP. Las prioridades son coste gratuito sostenible, configuración razonable, privacidad del token y estadísticas operativas consultables desde Grafana.

## Decisión

1. Se adopta un custom topic de **Azure Event Grid Basic**. El evento inicial será `access.invitation-email.requested.v1` en CloudEvents 1.0 y se entregará por push HTTPS a un endpoint interno de Access.
2. La identidad administrada de la API publicará con `EventGrid Data Sender` mediante la API CloudEvents y `DefaultAzureCredential`. La entrega al webhook se protegerá con Microsoft Entra ID, una audiencia propia y un rol de aplicación exclusivo para Event Grid; no se reutilizarán sesiones de usuarios.
3. La coordinación de emisión publicará el evento antes del commit local de PostgreSQL. Si el webhook llega antes de que la transacción sea visible, responderá `503` para que Event Grid reintente. Una transacción abortada deja como máximo un evento huérfano que termina en dead letter sin enviar correo.
4. `InvitationOutboxMessage` se conserva durante este incremento como ledger de entrega e idempotencia, con su identificador usado como `CloudEvent.id`. Se retira `InvitationOutboxWorker` y el polling, no la trazabilidad de `DeliveryStatus`. La eliminación o renombrado de la tabla será un incremento posterior si deja de ser necesaria.
5. El payload solo llevará el identificador de invitación, el ciphertext del token, la versión de clave, el instante, el tipo y la versión de esquema. La clave actual y la anterior deberán poder descifrar eventos durante toda la retención configurada.
6. El consumidor será al menos una vez e idempotente en el estado persistido. No se promete exactamente una vez frente a Brevo; si el proveedor acepta el envío y falla la confirmación local, puede existir un duplicado técnico.
7. La API podrá escalar a cero. No se añadirá ningún `BackgroundService`, cron o reconciliador que consulte PostgreSQL periódicamente.
8. La observabilidad combinará métricas OpenTelemetry de API/Brevo con métricas de Azure Monitor para Event Grid. Grafana tendrá un dashboard versionado `dnd-event-broker`, un datasource Azure Monitor de solo lectura y alertas para publicación, entrega, latencia, endpoint, dead letters y consumo de la franquicia.

## Alternativas consideradas

- **Azure Service Bus Basic:** mejor semántica de cola y `PeekLock`, pero cobra por operación, solo ofrece colas y no incluye topics, transacciones ni detección de duplicados. Queda como alternativa si el caso exige una cola de comandos.
- **Azure Service Bus Standard:** resuelve esas capacidades, pero incorpora cargo base horario.
- **Azure Storage Queues:** simple y reutilizable con Storage, pero lectura no bloqueante, sin dead letter automático y sin la entrega push necesaria.
- **Upstash QStash/Cloudflare Queues:** tienen franquicias gratuitas y push HTTP, pero introducen una segunda plataforma, secretos y otro circuito operativo.
- **RabbitMQ/NATS autogestionado:** software gratuito, pero exige un proceso y persistencia operativos permanentes.

## Consecuencias

### Positivas

- No hay sondeo de PostgreSQL ni proceso residente de entrega.
- Event Grid Basic cubre el volumen inicial dentro de 100.000 operaciones gratuitas mensuales.
- El custom topic deja una ruta de fan-out futura sin introducirla ahora.
- Grafana permite distinguir fallos de API, broker, webhook, Brevo y dead letter.
- La tabla actual conserva los estados de entrega y facilita rollback durante la transición.

### Costes y riesgos

- Publicar dentro de una transacción local alarga su duración y requiere pruebas de carrera con el webhook.
- Event Grid no garantiza orden ni exactamente una vez; el consumidor debe tolerar duplicados.
- La autenticación Entra del webhook requiere una App Registration y una asignación RBAC adicional.
- Las métricas Azure Monitor requieren un datasource de lectura en Grafana Cloud; los dashboards locales solo mostrarán las métricas OTLP de la aplicación.
- El dead letter usa Blob Storage y puede generar coste de almacenamiento/operaciones, aunque no añade un proceso residente.

## Revisión

Se abrirá un ADR sustituto si el volumen supera de forma sostenida el 80 % de la franquicia, si se necesita orden/sesiones/transacciones/deduplicación de broker, si Event Grid cambia su modelo gratuito o si los duplicados y dead letters incumplen los objetivos operativos.
