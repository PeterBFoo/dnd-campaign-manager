# Plan 021: Broker de eventos y entrega asíncrona de correo

- Estado: Aprobado
- Fecha: 2026-08-29
- Especificación: [spec.md](spec.md)
- ADR: [ADR-0009](../../adr/0009-broker-eventos-y-observabilidad-grafana.md)
- Dependencias de implementación: [ADR-0001](../../adr/0001-monorepositorio-y-monolito-modular.md), [ADR-0002](../../adr/0002-identidad-invitaciones-y-correo-transaccional.md), [ADR-0004](../../adr/0004-arquitectura-modular-cqrs-y-limites-de-dependencia.md), [docs de dashboards](../../operations/dashboards-de-observabilidad.md)

## Resultado esperado

Una emisión o reenvío de invitación confirmará su operación local y publicará un `CloudEvent` `access.invitation-email.requested.v1` en Azure Event Grid Basic. Event Grid llamará al endpoint interno de Access cuando exista un mensaje; Access consultará PostgreSQL solo durante esa entrega, enviará por Brevo y registrará el resultado. No quedará ningún `BackgroundService` consultando la tabla de outbox.

La API podrá ejecutarse con `min_replicas = 0`. Grafana mostrará las métricas de publicación, entrega, Brevo, dead letters, latencia y coste combinando OpenTelemetry y Azure Monitor.

## Topología objetivo

```text
HTTP emisión/reenvío
        │ transacción local: Invitation + InvitationOutboxMessage
        │ publicar antes del commit
        ▼
Azure Event Grid Basic · custom topic
        │ CloudEvents 1.0 · suscripción filtrada · max batch 1
        │ push HTTPS + Microsoft Entra ID
        ▼
API Access /internal/events/invitation-email
        │ consulta y actualiza PostgreSQL solo durante la entrega
        ├── Brevo API
        └── estado DeliveryStatus / métricas / logs / trazas

Azure Monitor ───────────────┐
OpenTelemetry → Grafana Cloud ├── dashboard dnd-event-broker + alertas
Loki/Tempo ──────────────────┘
```

## Estrategia de implementación

1. Incorporar el contrato, el ADR y las interfaces de aplicación sin cambiar todavía el contrato HTTP público.
2. Reutilizar `InvitationOutboxMessage` como ledger de entrega y publicar con su `Id` como `CloudEvent.id`; no eliminar inicialmente la tabla ni sus columnas de estado.
3. Extraer la lógica del worker a un caso de uso invocable por el endpoint interno y cubrirla con pruebas de duplicado, carrera y fallo.
4. Aprovisionar Event Grid, la autenticación Entra, el dead letter y las alertas en Azure; publicar con la identidad administrada existente.
5. Añadir métricas OpenTelemetry, consulta Azure Monitor, dashboard y runbook.
6. Migrar los pendientes del outbox, activar la suscripción, verificar el recorrido real y retirar el worker y su configuración.
7. Ejecutar suites, smoke tests y validación de infraestructura antes de cerrar el incremento.

Cada fase debe ser desplegable o reversible. No se activará una suscripción que entregue eventos al endpoint hasta que el consumidor y su autenticación estén desplegados.

## Contrato y capas de Access

### Contrato de integración

- Crear un tipo CloudEvents 1.0 en `apps/api/Modules/Access/DndCampaign.Modules.Access/Contracts` para `access.invitation-email.requested.v1`.
- Serializar solo `invitationId`, `encryptedToken`, `keyVersion`, `occurredAt` y `schemaVersion` en `data`.
- Validar tipo, versión, tamaño, identificador no vacío y versión de clave antes de tocar PostgreSQL.
- Admitir el formato de entrega de Event Grid como lote de un elemento y configurar la suscripción con `maxEventsPerBatch = 1` para que el caso de uso procese una unidad por petición.
- Mantener el identificador de correlación estable desde la publicación hasta Brevo; nunca serializar correo, nombre, campaña, enlace ni token en claro.

### Application

- Añadir un puerto `IInvitationEventPublisher` o equivalente en Application, sin referencias a Azure SDK.
- Cambiar los handlers de emisión y reenvío para crear el ledger, proteger el token y solicitar la publicación antes del commit local coordinado por `IAccessUnitOfWork`.
- Traducir resultados del adaptador a errores tipados: indisponibilidad/timeout como reintentable y rechazo de contrato/configuración como error no reintentable.
- Añadir un caso de uso `ProcessInvitationEmailEvent` que valide el estado funcional, consulte la invitación, componga el correo, invoque `ITransactionalEmailSender` y cierre el ledger.
- Si la invitación ya fue enviada, revocada, aceptada o caducada, cerrar el evento como obsoleto sin enviar correo. Si la fila todavía no es visible por una transacción en curso, devolver un resultado transitorio.
- Preservar los estados `pending`, `sent`, `discarded` y `failed` que usa `InvitationListItemDto`.

### Infrastructure

- Implementar el publicador con la API CloudEvents de Event Grid sobre `HttpClient` y `DefaultAzureCredential` (`https://eventgrid.azure.net/.default`), usando la identidad administrada de la Container App y el rol `EventGrid Data Sender`; así se evita añadir un SDK específico al dominio.
- Extraer/adaptar la lógica de `InvitationOutboxWorker` a un servicio de entrega invocable; eliminar la herencia de `BackgroundService` y el `Task.Delay` de cinco segundos.
- Usar una transacción de PostgreSQL para adquirir/cerrar el ledger durante el consumo, con concurrencia segura y sin bloquear el proceso mientras se espera a Event Grid.
- Reutilizar `InvitationEmailComposer` y `BrevoEmailSender`; pasar `CloudEvent.id` como correlación del correo.
- Mantener soporte para leer la clave de cifrado actual y anterior durante la retención de Event Grid.

### Endpoint interno y autorización

- Añadir un controlador/endpoint interno fuera del contrato `/api/v1` de usuario, con una ruta estable documentada para la suscripción.
- Registrar un esquema de autenticación Entra separado del bearer de sesiones y exigir emisor, audiencia y rol de aplicación asignado a Event Grid.
- Responder `204` para evento procesado, obsoleto o duplicado ya cerrado; `400` para schema/tipo inválido; `401/403` para autenticación inválida; `503` para base de datos, Brevo o configuración transitoriamente no disponible.
- No aceptar el endpoint desde el frontend ni permitir que un usuario autenticado lo invoque con su sesión normal.
- Deshabilitar el batching de la suscripción en la primera versión para mantener atómica la respuesta por evento.
- Exponer además `/internal/events/invitation-email/replay-pending` para un único repoblamiento de filas pendientes durante la transición; no se ejecutará periódicamente.

## Datos y migración

### Ledger existente

`InvitationOutboxMessage` permanecerá en la primera entrega como ledger de integración. Su clave primaria será el `CloudEvent.id`; `EncryptedToken`, `ProcessedAt`, `ProviderMessageId`, `Attempts`, `NextAttemptAt` y `LastErrorCode` conservarán la trazabilidad que hoy alimenta `DeliveryStatus`. El plan no elimina la tabla mientras sea necesaria para rollback y para visualizar estados históricos.

Si la implementación requiere distinguir publicación de entrega, añadirá campos explícitos y una migración compatible (por ejemplo `PublishedAt`, `EventType` y `SchemaVersion`) sin cambiar filas existentes. No se guardará una copia adicional del token en claro.

### Transición de pendientes

1. Inventariar mensajes no procesados y bloquear la emisión de nuevos mensajes durante la ventana de cambio.
2. Desplegar topic, suscripción deshabilitada, endpoint y consumer.
3. Publicar los pendientes conservando su `Id`; marcar los que sean obsoletos como descartados.
4. Verificar que no quedan pendientes sin evento y habilitar la suscripción.
5. Deshabilitar el worker antiguo y `Email__OutboxWorkerEnabled`; comprobar que no hay dos consumidores activos.
6. Mantener una ventana de rollback con el ledger y la configuración anterior antes de retirar cualquier columna.

La migración se probará sobre una base vacía y sobre una base con invitaciones pendientes, procesadas, fallidas, revocadas y caducadas.

## API pública y frontend

- No cambiar rutas, payloads, códigos HTTP ni reglas de autorización de emisión, reenvío, listado, revocación o aceptación.
- `apps/web` no tendrá cambios funcionales: continuará mostrando `DeliveryStatus` proveniente de los mismos DTO.
- Mantener pruebas de cliente y componentes como regresión para demostrar que la sustitución es transparente.
- Si se detecta una indisponibilidad del broker durante la emisión, traducirla al `ProblemDetails` existente sin filtrar el nombre del proveedor, el payload o los secretos.

## Infraestructura Azure y despliegue

### Event Grid

- Crear con Terraform el custom topic Basic, la identidad administrada del topic si se necesita para destinos, la suscripción filtrada por `eventType`, el TTL y el máximo de intentos aceptado.
- Configurar dead letter hacia un contenedor Blob privado separado de `character-images` y asignar el rol mínimo a Event Grid.
- Crear la App Registration de entrega, su app role y la asignación de la identidad administrada de Event Grid; documentar el prerrequisito de permisos Microsoft Graph si no puede gestionarse desde el provider Terraform disponible.
- Conceder a la identidad de la API únicamente publicación en el topic. No usar claves SAS en la aplicación.
- Verificar que la URL pública del Container App permite el callback HTTPS y que la ruta interna no queda publicada en el catálogo OpenAPI de usuario.

### Container Apps y secretos

- Cambiar API a `min_replicas = 0`; conservar un máximo de una réplica inicial y aumentar solo con evidencia.
- Eliminar `Email__OutboxWorkerEnabled` de Compose, `compose.deploy.yaml` y `scripts/deploy-azure.sh` después de completar la transición.
- Añadir únicamente los identificadores no secretos del topic, tenant y audiencia. La identidad administrada reemplaza secretos de publicación.
- Mantener Brevo, base de datos, cifrado y Grafana Cloud como secretos independientes; ninguno llega a Angular.

## Observabilidad y Grafana

### Instrumentación

- Añadir un meter `DndCampaign.Api.EventBroker` con los nombres definidos en el spec: publicación, entrega, latencia, reintentos, duplicados, obsoletos, descifrado, esquema, autenticación, dead letters y operaciones estimadas.
- Crear spans `broker.publish`, `broker.delivery` y `email.send` con `event.type`, `operation`, `outcome` y códigos de error acotados. Propagar `trace_id` y `CloudEvent.id` sin IDs funcionales en dimensiones.
- Registrar logs estructurados para cada transición relevante y para el resultado de health checks, sin payload, correo, token, URL con secreto ni respuesta completa de Brevo.
- No convertir Event Grid en una dependencia de `/health/live` o `/health/ready`; la disponibilidad del proveedor se observará por sus métricas y por entregas reales.

### Azure Monitor y Grafana

- Configurar en Grafana Cloud un datasource Azure Monitor con permisos de lectura limitados a los recursos Event Grid y Storage de dead letter; no usar la cuenta de publicación de dashboards para consultar recursos.
- Crear `infra/observability/grafana/dashboards/event-broker.json`, registrarlo en `infra/observability/grafana/dashboards.yaml` y publicarlo con `scripts/publish-grafana-dashboards.sh`.
- Incluir paneles de disponibilidad HTTP, `PublishSuccessCount`/`PublishFailCount`, `DeliverySuccessCount`/`DeliveryAttemptFailCount`, `DestinationProcessingDurationInMs`, volumen diario, p95/p99, Brevo, duplicados, obsoletos, dead letters y coste estimado.
- Hacer que los paneles toleren ausencia de tráfico sin pintar una caída: la tasa de disponibilidad solo se calcula cuando existen eventos coincidentes.
- Añadir enlaces de panel a Explore/Loki/Tempo y al runbook operativo de `docs/operations/dashboards-de-observabilidad.md`.
- Provisionar alertas para publicación fallida, 5xx del webhook, delivery fail, dead letter, p95 superior a dos minutos, ausencia de entrega con eventos publicados y consumo del 80 % de la cuota gratuita.

## Pruebas y verificación

### Application e infraestructura de Access

- Unitarios del contrato CloudEvents: serialización, límites, tipo, versión y ausencia de datos prohibidos.
- Unitarios del caso de uso: pendiente/vigente, obsoleto, duplicado, token ilegible, Brevo aceptado, Brevo fallido y estados de ledger.
- Tests de concurrencia con PostgreSQL real para dos entregas del mismo `CloudEvent.id`, emisión concurrente y callback antes del commit.
- Tests de fallo inyectado para publicación timeout, commit abortado, caída de PostgreSQL, caída de Brevo y reinicio entre envío y persistencia.
- Tests de autenticación del endpoint para rol correcto, audiencia/emisor incorrectos, sesión de usuario y payload inválido.
- Tests de contrato HTTP que verifiquen que las rutas públicas y `DeliveryStatus` no cambian.

### Migración y despliegue

- Tests de migración sobre base vacía y versión soportada con outbox pendiente.
- `terraform fmt -check`, `terraform validate` y revisión de que no se crean recursos Standard/Premium ni secretos en Terraform.
- `docker compose config --quiet` y arranque local sin credenciales Azure, usando adaptador determinista.
- Smoke test Azure: publicación real, callback autenticado, envío Brevo controlado, retry `503`, evento obsoleto y dead letter.
- Prueba de escala a cero y arranque en frío del Container App, verificando que Event Grid reintenta ante el primer timeout.

### Observabilidad

- Validar que el dashboard local carga junto a `platform-overview`, `dotnet-runtime` y `postgresql`.
- Publicar el dashboard en Grafana Cloud y comprobar que el datasource Azure Monitor devuelve métricas del topic y de la suscripción.
- Generar datos sintéticos y fallos controlados para comprobar cada alerta sin enviar correos reales ni registrar payloads.
- Confirmar que no aparecen correos, tokens, cuerpos, URLs secretas o payload cifrado en Prometheus, Loki, Tempo, logs de Event Grid ni paneles.

## Documentación afectada

- Actualizar [dashboards-de-observabilidad.md](../../operations/dashboards-de-observabilidad.md) con el nuevo dashboard, datasource Azure Monitor, paneles y runbook.
- Actualizar [despliegue-azure.md](../../operations/despliegue-azure.md) con topic, App Registration, RBAC, dead letter, escala a cero y smoke test.
- Actualizar [secretos-de-despliegue.md](../../operations/secretos-de-despliegue.md) solo para nuevas referencias de identidad/configuración; no documentar valores.
- Actualizar [diagrama de componentes](../../architecture/diagrama-de-componentes.md) y [diagrama de despliegue](../../architecture/diagrama-de-despliegue.md) para retirar el worker, mostrar Event Grid y Grafana/Azure Monitor.
- Marcar ADR-0002 como sustituido parcialmente mediante ADR-0009; no reescribir su decisión histórica.
- Crear `tasks.md` después de aprobar este plan, agrupando cada bloque en tareas pequeñas y verificables.

## Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Publicación antes del commit y callback prematuro | Responder `503` hasta que la fila sea visible; test de carrera y TTL para huérfanos |
| Duplicado después de aceptación de Brevo | Ledger idempotente, correlación estable y reconocimiento explícito de la ventana proveedor/proceso |
| Dos consumidores durante el cambio | Suscripción deshabilitada hasta migrar; bandera de transición y evidencia de un solo consumidor |
| App Registration/RBAC no provisionable por Terraform | Prerrequisito documentado, permisos mínimos y prueba de humo antes de activar la suscripción |
| Azure Monitor no disponible en Grafana local | Paneles OTLP completos localmente y paneles Azure condicionados al datasource productivo |
| Sobrepasar cuota gratuita por reintentos | Alertas al 80 %, métrica de operaciones estimadas y revisión antes de añadir suscripciones |
| Dead letter contiene ciphertext sensible | Contenedor privado, RBAC mínimo, retención limitada y ninguna vista de payload en Grafana |
| Event Grid no despierta la API tras cold start | Endpoint HTTPS público, timeout configurado, respuesta `503` y smoke test de escala a cero |

## Cierre

El plan se considerará ejecutado cuando los criterios de aceptación del spec estén cubiertos, el worker y su polling hayan desaparecido, el recorrido real de invitación funcione con Event Grid, Grafana muestre disponibilidad y estadísticas, y la documentación de operación permita diagnosticar publicación, entrega, Brevo y dead letters sin acceder a datos sensibles.
