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

La validación del webhook de Event Grid (`Microsoft.EventGrid.SubscriptionValidationEvent`)
se responde automáticamente; los eventos de correo requieren el rol de aplicación y
se rechazan si el lote no contiene exactamente un CloudEvent conocido.

Tras desplegar por primera vez, se puede repoblar una vez el tópico con los mensajes
pendientes que ya existían en PostgreSQL:

```sh
curl --fail-with-body -X POST "$API_BASE_URL/internal/events/invitation-email/replay-pending" \
  -H "Authorization: Bearer $EVENTGRID_REPLAY_TOKEN"
```

El endpoint es idempotente por el identificador del ledger; no debe programarse como
un proceso periódico.
