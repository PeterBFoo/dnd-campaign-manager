#!/bin/sh
set -eu

required_variables="AZURE_RESOURCE_GROUP AZURE_CONTAINER_APP API_IMAGE DATABASE_CONNECTION_STRING BREVO_API_KEY BREVO_SENDER_EMAIL FRONTEND_ORIGIN GRAFANA_CLOUD_OTLP_ENDPOINT GRAFANA_CLOUD_OTLP_HEADERS"
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
