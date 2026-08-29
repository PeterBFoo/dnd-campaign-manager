#!/bin/sh
set -eu

required_variables="AZURE_RESOURCE_GROUP AZURE_CONTAINER_APP API_IMAGE DATABASE_CONNECTION_STRING BREVO_API_KEY BREVO_SENDER_EMAIL IDENTITY_BOOTSTRAP_TOKEN OUTBOX_ENCRYPTION_KEY CHARACTER_STORAGE_SERVICE_URI ADVENTURE_CATALOG_STORAGE_SERVICE_URI FRONTEND_ORIGIN FRONTEND_BASE_URL EVENTGRID_TOPIC_ENDPOINT EVENTGRID_TENANT_ID EVENTGRID_AUDIENCE GRAFANA_CLOUD_OTLP_ENDPOINT GRAFANA_CLOUD_OTLP_HEADERS"
for variable_name in $required_variables; do
  eval "variable_value=\${$variable_name:-}"
  if [ -z "$variable_value" ]; then
    echo "$variable_name is required" >&2
    exit 2
  fi
done

az containerapp secret set \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --name "$AZURE_CONTAINER_APP" \
  --secrets \
    "database-connection-string=$DATABASE_CONNECTION_STRING" \
    "brevo-api-key=$BREVO_API_KEY" \
    "brevo-sender-email=$BREVO_SENDER_EMAIL" \
    "identity-bootstrap-token=$IDENTITY_BOOTSTRAP_TOKEN" \
    "outbox-encryption-key=$OUTBOX_ENCRYPTION_KEY" \
    "grafana-cloud-otlp-headers=$GRAFANA_CLOUD_OTLP_HEADERS" \
  --output none

az containerapp update \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --name "$AZURE_CONTAINER_APP" \
  --image "$API_IMAGE" \
  --min-replicas 0 \
  --max-replicas 1 \
  --cpu 0.25 \
  --memory 0.5Gi \
  --set-env-vars \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Campaigns=secretref:database-connection-string \
    Email__Brevo__ApiKey=secretref:brevo-api-key \
    Email__Brevo__SenderEmail=secretref:brevo-sender-email \
    EventGrid__Enabled=true \
    "EventGrid__TopicEndpoint=$EVENTGRID_TOPIC_ENDPOINT" \
    "EventGrid__TenantId=$EVENTGRID_TENANT_ID" \
    "EventGrid__Audience=$EVENTGRID_AUDIENCE" \
    Identity__BootstrapToken=secretref:identity-bootstrap-token \
    Identity__OutboxEncryptionKey=secretref:outbox-encryption-key \
    Database__ApplyMigrations=true \
    "Storage__Characters__ServiceUri=$CHARACTER_STORAGE_SERVICE_URI" \
    "Storage__AdventureCatalog__ServiceUri=$ADVENTURE_CATALOG_STORAGE_SERVICE_URI" \
    "Frontend__BaseUrl=$FRONTEND_BASE_URL" \
    "Email__Brevo__SenderName=${BREVO_SENDER_NAME:-D&D Campaign Manager}" \
    "Cors__AllowedOrigins__0=$FRONTEND_ORIGIN" \
    "OTEL_EXPORTER_OTLP_ENDPOINT=$GRAFANA_CLOUD_OTLP_ENDPOINT" \
    OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf \
    OTEL_EXPORTER_OTLP_HEADERS=secretref:grafana-cloud-otlp-headers \
    "OTEL_RESOURCE_ATTRIBUTES=deployment.environment=production,cloud.provider=azure,cloud.platform=azure_container_apps" \
  --output none

api_fqdn=$(az containerapp show \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --name "$AZURE_CONTAINER_APP" \
  --query properties.configuration.ingress.fqdn \
  --output tsv)

printf 'API deployed at https://%s\n' "$api_fqdn"
