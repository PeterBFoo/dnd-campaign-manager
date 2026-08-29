# Spec 021: Broker de eventos y entrega asíncrona de correo

- Estado: Aceptada
- Fecha: 2026-08-29
- Tipo: incremento técnico vertical
- Roadmap: [roadmap funcional](../../roadmap/product-roadmap.md)
- Requisitos relacionados: RF-070, RF-075, RF-076, RF-079, RF-080 y RF-081 (preserva sus contratos y reglas; no amplía el alcance funcional)
- Dependencias: [ADR-0001](../../adr/0001-monorepositorio-y-monolito-modular.md), [ADR-0002](../../adr/0002-identidad-invitaciones-y-correo-transaccional.md), [ADR-0003](../../adr/0003-bootstrap-sesiones-y-flujo-de-invitaciones.md), [ADR-0008](../../adr/0008-broker-eventos-y-observabilidad-grafana.md), [spec 002](../002-modularizacion-access/spec.md) y [spec 004](../004-creacion-campanas/spec.md)

## Problema

El envío de invitaciones se desacopla hoy mediante un outbox en PostgreSQL, pero `InvitationOutboxWorker` permanece activo dentro de la API y consulta la tabla cada cinco segundos cuando no hay trabajo. En producción esto obliga a mantener una réplica de Azure Container Apps siempre encendida, añade lecturas periódicas a Neon y hace que la latencia del correo dependa de un sondeo en lugar de la aparición de un mensaje.

El outbox actual sí resuelve una propiedad que debe conservarse: una invitación confirmada no se pierde cuando Brevo está temporalmente indisponible. Sustituir el worker por una publicación directa e independiente después del `commit` introduciría una escritura dual insegura entre PostgreSQL y el broker.

## Objetivo

Entregar el correo de invitación por reacción a un evento duradero, sin ningún proceso de la aplicación consultando periódicamente PostgreSQL, conservando la seguridad del token, los reintentos acotados, la trazabilidad y el comportamiento público actual de las invitaciones.

El incremento debe permitir que la API escale a cero cuando no recibe tráfico y que el coste ordinario del broker permanezca dentro de una franquicia gratuita adecuada al volumen inicial. La simplicidad de aprovisionamiento y operación es el segundo criterio de decisión.

## Estado actual verificado

- Access crea la invitación y `InvitationOutboxMessage` en la misma transacción local.
- El outbox conserva temporalmente el token cifrado y elimina el ciphertext al terminar o descartar el mensaje.
- `InvitationOutboxWorker` adquiere un mensaje pendiente, consulta después la invitación y llama a Brevo. Si no encuentra trabajo, espera cinco segundos y vuelve a consultar.
- El despliegue habilita `Email__OutboxWorkerEnabled=true` y fija una réplica mínima y máxima de la API.
- El frontend solo consume el estado funcional de las invitaciones; no conoce el outbox ni el proveedor de correo.

## Prioridades y restricciones

1. Coste fijo cero y operación ordinaria dentro de una franquicia gratuita permanente publicada. Una promoción temporal para cuentas nuevas no cuenta como gratuidad sostenible.
2. Configuración y operación sencillas dentro de la topología Azure ya existente.
3. Entrega duradera al menos una vez, reintentos y recuperación visible de mensajes no procesables.
4. Sin secretos nuevos en el navegador, las imágenes o el repositorio.
5. Sin direcciones de correo, nombres, cuerpos ni tokens utilizables en eventos, logs, métricas o dead letters.
6. Sin una transacción distribuida entre Neon PostgreSQL y Azure.
7. Sin introducir un framework genérico de eventos antes de que exista un segundo caso de uso real.

Los precios y franquicias son condiciones externas. Se verificaron el 2026-08-29 y deberán comprobarse de nuevo al aceptar el ADR y antes de aprovisionar producción.

## Alternativas evaluadas

| Opción | Gratuidad vigente | Simplicidad en esta topología | Capacidades y límites relevantes | Decisión |
|---|---|---|---|---|
| **Azure Event Grid Basic** | 100.000 operaciones/mes; publicar y cada intento de entrega consumen operaciones | Media; recurso Azure, RBAC para publicar y entrega HTTPS al Container App | Push, reintentos y dead letter; entrega al menos una vez y sin orden garantizado | **Seleccionada** |
| **Azure Service Bus Basic** | Pago por operación desde el uso ordinario; no se depende de la promoción temporal de Azure Free | Media-baja; buen SDK .NET, identidad administrada y emulador local, pero necesita consumidor o Container App Job | Cola duradera y `PeekLock`; Basic solo tiene colas y carece de topics, transacciones y detección de duplicados | Alternativa si el caso pasa a ser una cola de comandos estricta |
| **Azure Service Bus Standard** | Tiene cargo base horario tras cualquier promoción aplicable | Media-baja | Añade topics, suscripciones, transacciones y deduplicación | Descartada por coste fijo |
| **Azure Queue Storage** | Pago por almacenamiento y operaciones, aunque puede reutilizar la cuenta existente | Alta para aprovisionar; media para operar | Cola sencilla con entrega al menos una vez, pero lectura no bloqueante, sin dead letter automático y con polling directo o mediante KEDA | Descartada: no prima la gratuidad y aporta menos garantías |
| **Upstash QStash Free** | 1.000 mensajes/día y 50 GB/mes | Alta; publica y empuja HTTP sin consumidor residente | Reintentos, deduplicación y DLQ, región UE disponible; introduce cuenta, secretos y dependencia externa, sin SLA gratuito | Segunda alternativa si Event Grid deja de ser gratuito o resulta desproporcionado |
| **Cloudflare Queues Free** | 10.000 operaciones/día | Baja en esta solución | Retención gratuita de 24 horas; necesita un Worker consumidor o consumo HTTP pull y añade otro runtime | Descartada por complejidad transversal |
| **RabbitMQ o NATS autogestionado** | Software gratuito, infraestructura y operación no gratuitas | Baja | Control y portabilidad, a cambio de proceso siempre activo, actualizaciones, persistencia y monitorización propias | Descartada: contradice el objetivo operativo |

AWS SQS y Google Cloud Pub/Sub también ofrecen franquicias gratuitas útiles, pero se descartan en este incremento porque obligan a incorporar una segunda nube, otra identidad operativa y otro circuito de observabilidad sin aportar una ventaja funcional frente a Event Grid Basic.

### Valoración específica de Azure Service Bus

Service Bus es técnicamente la mejor opción si el mensaje se modela como un comando de trabajo para exactamente un consumidor, si se necesitan locks explícitos, sesiones, orden, transacciones internas del broker o una DLQ navegable como cola. Un receptor AMQP puede esperar de forma bloqueante y no hace busy polling contra PostgreSQL. Azure Container Apps también puede escalar un consumidor o un job a partir de la profundidad de una cola.

No se selecciona ahora porque las prioridades aceptadas invierten la decisión habitual: Basic no aporta una franquicia permanente, solo admite colas y no incluye deduplicación ni transacciones; Standard resuelve esas carencias con un cargo base. Para el volumen inicial de invitaciones, Event Grid ofrece las garantías necesarias, push nativo y coste cero dentro de 100.000 operaciones mensuales. La decisión se revisará si aparece alguno de los requisitos de cola estricta anteriores.

## Decisión propuesta

Se usará un **custom topic de Azure Event Grid Basic** para eventos de integración discretos. El primer y único contrato de este incremento será `access.invitation-email.requested.v1`, entregado mediante push HTTPS a un endpoint interno de Access en la misma API.

La identidad administrada actual de la Container App publicará con el rol mínimo `EventGrid Data Sender`. La suscripción entregará únicamente el tipo de evento admitido. El webhook no confiará en que una petición proceda de Event Grid por su forma: validará autenticación dedicada, audiencia y emisor, o un secreto rotatorio si el ADR demuestra que Entra ID introduce una complejidad operativa desproporcionada. No reutilizará tokens de sesión de usuarios.

Event Grid Basic se elige en lugar de Event Grid Namespace Standard: este incremento solo necesita eventos discretos con entrega push, no MQTT, pull delivery ni throughput reservado.

## Alcance

### Emisión

- Crear un puerto de publicación específico de Access y un adaptador de Event Grid, sin exponer tipos del SDK de Azure a Domain o Application.
- Publicar el evento como parte de la coordinación de emisión o reenvío de una invitación, sin dejar una invitación confirmada sin evento.
- Tratar indisponibilidad o resultado incierto del broker de forma explícita y recuperable; no responder éxito mientras exista riesgo conocido de haber confirmado una invitación sin entrega pendiente.
- Mantener estable el contrato HTTP público de emisión, reenvío, consulta, revocación y aceptación de invitaciones.

### Consumo

- Añadir un endpoint interno que acepte exclusivamente el contrato y versión admitidos.
- Cargar la invitación por identificador, verificar que sigue pendiente y vigente, componer el mensaje con los datos autoritativos de PostgreSQL y enviarlo con el adaptador Brevo existente.
- Responder éxito y cerrar la entrega cuando la invitación ya esté revocada, aceptada o caducada, sin enviar correo.
- Devolver un fallo reintentable cuando el estado necesario aún no sea visible o Brevo falle transitoriamente.
- Enviar a dead letter los mensajes que agoten la política de reintentos y hacer esa condición observable.

### Retirada del sondeo

- Eliminar `InvitationOutboxWorker` y la opción `Email__OutboxWorkerEnabled` cuando la migración de pendientes haya terminado.
- No sustituirlo por un `BackgroundService`, cron o bucle que consulte PostgreSQL periódicamente.
- Permitir `min_replicas = 0` para la API; el tráfico HTTP ordinario y el push de Event Grid serán los únicos activadores de la aplicación.
- Crear un contenedor privado separado para dead letters; no mezclar eventos fallidos con imágenes de personajes.

## Contrato mínimo del evento

El contrato usará CloudEvents 1.0 y contendrá únicamente:

- identificador estable del evento, usado para correlación y deduplicación de aplicación;
- tipo y versión `access.invitation-email.requested.v1`;
- identificador de la invitación;
- instante de emisión;
- token cifrado y versión de la clave necesaria para descifrarlo durante la retención máxima;
- versión del esquema de datos.

No contendrá correo, nombre del destinatario, nombre de campaña, token en claro, enlace de aceptación, cuerpo del mensaje ni credenciales. El tamaño permanecerá muy por debajo de 64 KB.

La rotación del cifrado conservará capacidad de lectura para eventos ya publicados hasta superar la retención y el máximo de reintentos. Una dead letter seguirá tratando su payload como secreto cifrado y tendrá retención y acceso mínimos.

## Consistencia y garantías

- El broker entrega al menos una vez. El consumidor debe hacer idempotente el estado de la invitación respecto del identificador del evento y tolerar duplicados y desorden; esto no elimina la ventana de duplicado frente a Brevo descrita abajo.
- La invitación en PostgreSQL continúa siendo la fuente de verdad funcional. El evento solo solicita un efecto de transporte y nunca concede acceso, cambia roles ni acepta la invitación.
- La coordinación entre PostgreSQL y Event Grid deberá evitar el hueco `commit correcto / publicación perdida`. Se acepta publicar antes del commit local y hacer que una entrega prematura responda como reintentable; un evento huérfano de una transacción abortada se descarta de forma segura después de una ventana acotada. El ADR comparará esta opción con cualquier alternativa de igual garantía que no reintroduzca polling.
- No se promete exactamente una vez frente a Brevo: existe una ventana inevitable si Brevo acepta el correo y el proceso falla antes de persistir el recibo. El mismo identificador de evento se reutilizará en todos los intentos y se aprovechará cualquier capacidad de idempotencia verificable del proveedor. Un duplicado técnico nunca crea una nueva invitación ni un token diferente.
- Una invitación aceptada, revocada o caducada antes del consumo no produce correo, aunque su evento llegue después.

## Migración y compatibilidad

- Antes de deshabilitar el worker, todo `InvitationOutboxMessage` pendiente debe quedar procesado, descartado o publicado con un identificador preservado y auditable.
- El despliegue impedirá que el worker antiguo y el consumidor nuevo procesen simultáneamente el mismo mensaje.
- Las filas históricas ya procesadas pueden conservarse durante la transición; la eliminación de la tabla y de la clave de configuración solo ocurrirá cuando no queden pendientes ni sea necesaria para rollback.
- Desarrollo local usará un adaptador determinista o un receptor HTTP de pruebas. La suite no dependerá de credenciales Azure. Una prueba de humo separada verificará topic, autenticación, webhook, reintento y dead letter en Azure.

## Ownership técnico

- `apps/api/Modules/Access` es propietario del contrato, publicación, consumo, validación del estado de invitación, idempotencia, composición y envío por Brevo.
- `apps/api` actúa como composition root para registrar el adaptador y exponer el endpoint interno sin incorporarlo al contrato público de usuario.
- `infra/azure` es propietario del custom topic Basic, la suscripción filtrada, identidad/RBAC, política de reintentos, dead letter, alertas y escala a cero.
- `infra/observability` y `docs/operations` son propietarios del dashboard versionado, la integración de Azure Monitor en Grafana, los paneles/alertas y el runbook de diagnóstico.
- Los scripts y documentación de despliegue son propietarios de la transición segura y de la eliminación de `Email__OutboxWorkerEnabled`.
- `apps/web` no cambia: no publica ni consume eventos, no recibe credenciales y los contratos HTTP y estados funcionales visibles permanecen iguales. Esta ausencia es deliberada y se verificará manteniendo verdes sus pruebas de invitaciones.

## Observabilidad, disponibilidad y coste

La observabilidad tendrá dos fuentes complementarias y se visualizará en Grafana. Las métricas de aplicación llegarán por OpenTelemetry al mismo backend Prometheus/Grafana Cloud ya utilizado por la plataforma. Las métricas propias del recurso Azure Event Grid se consultarán desde Azure Monitor mediante un datasource de solo lectura de Grafana; no se añadirá un proceso de sondeo a la API. En el entorno local, los paneles de Azure quedarán sin datos y las pruebas usarán el adaptador de broker determinista.

### Métricas de la aplicación

Access publicará un meter independiente, con dimensiones acotadas `event.type`, `operation` y `outcome`:

- `broker.publish.attempts`, `broker.publish.failures` y `broker.publish.duration`;
- `broker.delivery.requests`, `broker.delivery.successes` y `broker.delivery.failures`;
- `broker.delivery.duration` y `broker.delivery.age`, desde la emisión hasta la recepción;
- `broker.delivery.retries`, `broker.delivery.duplicates` y `broker.delivery.stale`;
- `broker.payload.decrypt.failures`, `broker.payload.schema.failures` y `broker.authentication.failures`;
- `broker.dead_letters` y `broker.cost.operations_estimated`;
- las métricas existentes `email.send.attempts`, `email.send.failures` y `email.send.duration`, correlacionadas por `event.type` y resultado.

Los contadores no incluirán identificadores de campaña, usuario, invitación o destinatario. El identificador de evento podrá aparecer en una traza o log de diagnóstico con retención acotada, nunca en una dimensión de métrica.

### Métricas de Azure Event Grid

Para el custom topic y su suscripción se mostrarán en Grafana las series de Azure Monitor disponibles para `Microsoft.EventGrid/topics` y `Microsoft.EventGrid/eventSubscriptions`, como mínimo:

- `PublishSuccessCount`, `PublishFailCount` y `PublishSuccessLatencyInMs`;
- `MatchedEventCount` y `UnmatchedEventCount`;
- `DeliverySuccessCount`, `DeliveryAttemptFailCount` y `DestinationProcessingDurationInMs`;
- `DeadLetteredCount` y `DroppedEventCount`.

Estas señales permiten separar un fallo de publicación en Access, un fallo de entrega HTTP, una respuesta lenta del webhook y un evento que termina en dead letter. Se conservarán las dimensiones de tipo de error que expone Azure Monitor, pero no se añadirán dimensiones de datos funcionales.

### Dashboard y paneles de Grafana

Se creará el dashboard versionado `dnd-event-broker` en `infra/observability/grafana/dashboards/event-broker.json` y se incorporará a [dashboards-de-observabilidad.md](../../operations/dashboards-de-observabilidad.md). Tendrá, como mínimo:

1. **Disponibilidad del recorrido:** estado de `/health/live` y `/health/ready`, tasa de 5xx del webhook, `PublishSuccessCount` frente a `PublishFailCount` y `DeliverySuccessCount` frente a fallos.
2. **Volumen y coste:** eventos publicados y entregados por hora/día, intentos totales, reintentos, operaciones estimadas y porcentaje de la franquicia gratuita consumida.
3. **Latencia:** p50/p95/p99 de publicación, procesamiento del destino y tiempo emisión→Brevo.
4. **Errores y seguridad:** descifrado, esquema, autenticación, Brevo, respuestas HTTP por código y duplicados/obsoletos descartados.
5. **Dead letters:** total, razón, antigüedad del más antiguo y enlace operativo al contenedor privado de Azure Blob sin mostrar el payload.
6. **Correlación:** enlaces desde una traza de emisión o webhook a los logs de Loki y a la traza de Brevo en Tempo, sin incluir cuerpos, correos ni tokens.

La disponibilidad del broker se calculará como entregas aceptadas respecto de eventos coincidentes, con una ventana explícita y sin interpretar ausencia de tráfico como una caída. La disponibilidad del endpoint se vigilará además con las métricas HTTP de la plataforma y una sonda sintética externa para `/health/ready`; la sonda no publicará correos ni eventos de prueba.

### Alertas y operación

Se provisionarán en Grafana/Azure Monitor, con enlaces a un runbook, estas alertas iniciales:

- `PublishFailCount > 0` o `broker.publish.failures > 0` durante cinco minutos;
- tasa de 5xx del webhook superior al 1 % durante cinco minutos;
- `DeliveryAttemptFailCount > 0` sostenido diez minutos;
- cualquier `DeadLetteredCount`, `broker.payload.*.failures` o fallo de autenticación;
- p95 de entrega o de emisión→Brevo por encima de dos minutos durante diez minutos;
- ausencia de `DeliverySuccessCount` cuando existen eventos publicados coincidentes durante diez minutos;
- consumo estimado superior al 80 % de las 100.000 operaciones gratuitas mensuales.

Los umbrales son iniciales y se revisarán con tráfico real. Un health check no hará llamadas de red al broker en cada petición: `/health/live` solo prueba el proceso y `/health/ready` la base de datos y configuración crítica; la disponibilidad del proveedor se deriva de sus métricas y de la entrega real.

Habrá una alerta antes de consumir el 80 % de las 100.000 operaciones gratuitas y otra inmediata para cualquier dead letter. Con una suscripción y sin reintentos, un correo consume al menos una publicación y un intento de entrega; el presupuesto debe contar los reintentos y filtros avanzados como operaciones adicionales. No se habilitarán Event Grid Standard, throughput units ni recursos con cargo fijo en este incremento.

Los logs podrán incluir identificador de evento, tipo, resultado y código de error. No incluirán payload cifrado, identificador de invitación completo cuando no sea necesario, correo, token, URL con secreto ni respuesta de Brevo.

## Criterios de aceptación

1. Emitir o reenviar una invitación confirmada publica exactamente un evento lógico con contrato versionado; el endpoint público conserva su respuesta y autorización actuales.
2. Con la API inicialmente escalada a cero, Event Grid activa el endpoint, Access envía el correo mediante Brevo y registra el recibo sin que exista un proceso residente consultando PostgreSQL.
3. Si Brevo falla transitoriamente, Event Grid reintenta y un intento posterior completa la entrega sin crear otra invitación ni rotar el token.
4. Si el mismo evento se entrega más de una vez, el estado funcional permanece correcto y todos los intentos conservan el mismo identificador de correlación.
5. Una invitación revocada, aceptada o caducada no genera correo cuando se consume su evento pendiente.
6. Un fallo entre publicación y commit no envía correo para una invitación inexistente; un fallo de publicación no deja confirmada una invitación sin trabajo pendiente.
7. Un mensaje no procesable agota una política acotada, llega al contenedor privado de dead letter y activa una alerta sin exponer datos personales ni tokens utilizables.
8. Peticiones sin autenticación interna válida, con emisor/audiencia incorrectos, tipos desconocidos o versiones no soportadas no ejecutan efectos y quedan registradas sin payload sensible.
9. Los pendientes del outbox anterior se migran o terminan antes de retirar el worker, sin pérdida y sin habilitar dos consumidores para el mismo mensaje.
10. No existen `InvitationOutboxWorker`, `Email__OutboxWorkerEnabled` ni otro bucle de aplicación que sondee la tabla; una prueba de arquitectura o composición impide reintroducirlos silenciosamente.
11. Terraform valida el topic Basic, suscripción filtrada, RBAC mínimo, dead letter, alertas y `min_replicas = 0`; el despliegue no añade secretos al frontend.
12. Las pruebas unitarias, de integración con PostgreSQL, de contrato HTTP, de fallos inyectados y de frontend permanecen verdes; una prueba de humo demuestra publicación y entrega reales en Azure.
13. Grafana muestra el dashboard `dnd-event-broker` con disponibilidad, volumen, latencia, errores, reintentos, dead letters y consumo de la franquicia; los paneles distinguen métricas de Access de métricas de Azure Event Grid.
14. Las alertas de publicación, entrega, dead letter, latencia, endpoint y coste se prueban con datos sintéticos o fallos inyectados y enlazan a un procedimiento operativo sin exponer payloads.

## Fuera de alcance

- Sustituir Brevo o modificar plantillas y contenido de correo.
- Cambiar reglas, estados, caducidad, rate limiting o permisos de invitaciones.
- Publicar eventos para todos los agregados o construir una abstracción universal de event sourcing.
- Webhooks de entrega, rebote o queja de Brevo.
- Entrega ordenada, exactamente una vez o transacciones distribuidas.
- Notificaciones push, tiempo real del combate, auditoría funcional o fan-out a otros módulos.
- Cambios visibles en Angular.

## Condiciones de revisión

La elección se revisará antes de añadir un segundo tipo de evento y también si ocurre cualquiera de estas condiciones:

- Event Grid elimina o reduce de forma material la franquicia gratuita;
- el volumen o los reintentos superan sostenidamente el 80 % de la franquicia;
- se necesita una cola de comandos con un único consumidor, orden, sesiones, transacciones internas o deduplicación del broker;
- la autenticación del webhook o las pruebas locales resultan más complejas de operar que un consumidor de Service Bus;
- la tasa de duplicados de correo o dead letters incumple los objetivos operativos medidos.

En esos casos, la primera alternativa será Azure Service Bus: Basic si bastan colas e idempotencia de aplicación, Standard si topics, transacciones o deduplicación justifican expresamente su cargo base.

## Fuentes de la evaluación

Consultadas el 2026-08-29:

- [Precios y operaciones incluidas de Azure Event Grid](https://azure.microsoft.com/en-us/pricing/details/event-grid/)
- [Precios y cómputo de operaciones de Azure Service Bus](https://azure.microsoft.com/en-us/pricing/details/service-bus/)
- [Niveles y capacidades de Azure Service Bus](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-premium-messaging)
- [Garantías de entrega y duplicados de Azure Service Bus](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-message-loss-and-duplicates)
- [Comparación de Azure Queue Storage y Service Bus](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-azure-and-service-bus-queues-compared-contrasted)
- [Autenticación de publicación en Event Grid con Microsoft Entra ID](https://learn.microsoft.com/en-us/azure/event-grid/authenticate-with-microsoft-entra-id)
- [Autenticación de entrega a webhooks de Event Grid](https://learn.microsoft.com/en-us/azure/event-grid/security-authentication)
- [Entrega duradera, reintentos y dead letter de Event Grid](https://learn.microsoft.com/en-us/azure/event-grid/delivery-and-retry)
- [Reintentos y dead letter de Event Grid](https://learn.microsoft.com/en-us/azure/event-grid/manage-event-delivery)
- [Métricas de publicación y entrega de topics de Event Grid](https://learn.microsoft.com/en-us/azure/azure-monitor/reference/supported-metrics/microsoft-eventgrid-topics-metrics)
- [Métricas de suscripciones de Event Grid](https://learn.microsoft.com/en-us/azure/azure-monitor/reference/supported-metrics/microsoft-eventgrid-eventsubscriptions-metrics)
- [Alertas para métricas de Event Grid](https://learn.microsoft.com/en-us/azure/event-grid/set-alerts)
- [Custom topics de Event Grid y entrega push](https://learn.microsoft.com/en-us/azure/event-grid/custom-topics)
- [Azure Container Apps Jobs activados por eventos](https://learn.microsoft.com/en-us/azure/container-apps/jobs)
- [Emulador local de Azure Service Bus](https://learn.microsoft.com/en-us/azure/service-bus-messaging/test-locally-with-service-bus-emulator)
- [Precios de Upstash QStash](https://upstash.com/pricing/qstash)
- [Regiones de Upstash QStash](https://upstash.com/docs/qstash/howto/multi-region)
- [Precios de Cloudflare Queues](https://developers.cloudflare.com/queues/platform/pricing/)
