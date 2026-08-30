#!/bin/sh
set -eu

required_variables="AZURE_SUBSCRIPTION_ID AZURE_RESOURCE_GROUP AZURE_CONTAINER_APP API_IMAGE ALLOY_IMAGE DATABASE_CONNECTION_STRING BREVO_API_KEY BREVO_SENDER_EMAIL IDENTITY_BOOTSTRAP_TOKEN OUTBOX_ENCRYPTION_KEY CHARACTER_STORAGE_SERVICE_URI ADVENTURE_CATALOG_STORAGE_SERVICE_URI FRONTEND_ORIGIN FRONTEND_BASE_URL EVENTGRID_TOPIC_ENDPOINT EVENTGRID_TENANT_ID EVENTGRID_AUDIENCE GRAFANA_CLOUD_OTLP_ENDPOINT GRAFANA_CLOUD_OTLP_HEADERS"
for variable_name in $required_variables; do
  eval "variable_value=\${$variable_name:-}"
  if [ -z "$variable_value" ]; then
    echo "$variable_name is required" >&2
    exit 2
  fi
done

case "$GRAFANA_CLOUD_OTLP_HEADERS" in
  Authorization=Basic%20*) ;;
  *)
    echo "GRAFANA_CLOUD_OTLP_HEADERS must use Authorization=Basic%20<base64> format." >&2
    exit 2
    ;;
esac

grafana_cloud_authorization=$(printf '%s' "${GRAFANA_CLOUD_OTLP_HEADERS#Authorization=}" | sed 's/%20/ /g')
postgres_exporter_connection_string=$(printf '%s' "$DATABASE_CONNECTION_STRING" | sed 's/-pooler\\././')
deploy_revision_id=$(printf '%s' "${DEPLOY_SHA:-$(date +%s)}" | cut -c1-12)
container_app_rendered=$(mktemp "${TMPDIR:-/tmp}/dnd-container-app.XXXXXX.yaml")
trap 'rm -f "$container_app_rendered"' EXIT

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
    "postgres-dsn=$postgres_exporter_connection_string" \
    "grafana-cloud-authorization=$grafana_cloud_authorization" \
  --output none

CONTAINER_APP_REVISION_SUFFIX="app-$deploy_revision_id" \
  sh scripts/render-container-app-template.sh > "$container_app_rendered"

az containerapp update \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --name "$AZURE_CONTAINER_APP" \
  --yaml "$container_app_rendered" \
  --output none

container_names=$(az containerapp show \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --name "$AZURE_CONTAINER_APP" \
  --query "join(' ', sort(properties.template.containers[].name))" \
  --output tsv)

if [ "$container_names" != "alloy api postgres-exporter" ]; then
  echo "Unexpected deployed containers: $container_names" >&2
  exit 1
fi

scale_min=$(az containerapp show \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --name "$AZURE_CONTAINER_APP" \
  --query properties.template.scale.minReplicas \
  --output tsv)

scale_max=$(az containerapp show \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --name "$AZURE_CONTAINER_APP" \
  --query properties.template.scale.maxReplicas \
  --output tsv)

if [ "$scale_min" != "0" ] || [ "$scale_max" != "1" ]; then
  echo "Unexpected replica limits: min=$scale_min max=$scale_max" >&2
  exit 1
fi

api_fqdn=$(az containerapp show \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --name "$AZURE_CONTAINER_APP" \
  --query properties.configuration.ingress.fqdn \
  --output tsv)

printf 'API deployed at https://%s\n' "$api_fqdn"
