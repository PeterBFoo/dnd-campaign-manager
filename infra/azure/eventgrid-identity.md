# Identidad de entrega de Event Grid

La suscripción usa el endpoint interno de la Container App y la API valida un token
de Microsoft Entra ID con la audiencia configurada en `EventGrid__Audience` y el rol
`AzureEventGridSecureWebhookSubscriber`. La App Registration se crea una sola vez por entorno:

```sh
app_id=$(az ad app create --display-name dnd-campaign-eventgrid-delivery --query appId --output tsv)
az ad sp create --id "$app_id"
az ad app update --id "$app_id" --identifier-uris "api://dnd-campaign-eventgrid"
```

El propietario debe definir un app role con valor `AzureEventGridSecureWebhookSubscriber`, asignarlo
al service principal de Event Grid y guardar únicamente el `tenantId` y el identificador
de audiencia como configuración no secreta. Los secretos de Brevo y PostgreSQL siguen
siendo referencias secretas de Container Apps. La identidad administrada de la API
recibe `EventGrid Data Sender` únicamente sobre el tópico provisionado por Terraform.

La validación CloudEvents se responde mediante `OPTIONS`; los eventos de correo requieren
el rol de aplicación y el endpoint recibe un único objeto CloudEvent estructurado.

## Crear tópicos adicionales

Los tópicos no se crean manualmente desde el portal. Se añade una entrada a
`eventgrid_topics` en el fichero `tfvars` del entorno, siguiendo el ejemplo de
`terraform.tfvars.example`, y se ejecutan `terraform plan` y `terraform apply` desde
`infra/azure`. Cada entrada crea conjuntamente:

- el topic con esquema de entrada CloudEvents 1.0;
- la suscripción al webhook autenticado de la API;
- la política de 30 reintentos y TTL de 24 horas;
- el destino privado de dead-letter;
- `EventGrid Data Sender` para la identidad de la API;
- `Monitoring Reader` para Alloy, de modo que el nuevo topic aparezca en Grafana.

Antes del `apply` debe existir el endpoint indicado por `webhook_path`, aceptar un solo
objeto CloudEvent y responder correctamente al `OPTIONS` de validación. La clave del mapa
es estable y no debe renombrarse después de crear el recurso; el filtro
`included_event_types` permite limitar los tipos entregados.

Tras desplegar por primera vez, se puede repoblar una vez el tópico con los mensajes
pendientes que ya existían en PostgreSQL:

```sh
curl --fail-with-body -X POST "$API_BASE_URL/internal/events/invitation-email/replay-pending" \
  -H "Authorization: Bearer $EVENTGRID_REPLAY_TOKEN"
```

El endpoint es idempotente por el identificador del ledger; no debe programarse como
un proceso periódico.
