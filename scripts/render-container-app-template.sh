#!/bin/sh
set -eu

required_variables="AZURE_SUBSCRIPTION_ID AZURE_RESOURCE_GROUP AZURE_CONTAINER_APP CONTAINER_APP_REVISION_SUFFIX API_IMAGE ALLOY_IMAGE CHARACTER_STORAGE_SERVICE_URI ADVENTURE_CATALOG_STORAGE_SERVICE_URI FRONTEND_ORIGIN FRONTEND_BASE_URL EVENTGRID_TOPIC_ENDPOINT EVENTGRID_TENANT_ID EVENTGRID_AUDIENCE GRAFANA_CLOUD_OTLP_ENDPOINT"
for variable_name in $required_variables; do
  eval "variable_value=\${$variable_name:-}"
  if [ -z "$variable_value" ]; then
    echo "$variable_name is required" >&2
    exit 2
  fi
done

container_app_source=${CONTAINER_APP_TEMPLATE_PATH:-infra/azure/container-app-api.yaml}
if [ ! -f "$container_app_source" ]; then
  echo "$container_app_source is required" >&2
  exit 2
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "jq is required to render the Container App template" >&2
  exit 2
fi

jq \
  --arg app_name "$AZURE_CONTAINER_APP" \
  --arg azure_subscription_id "$AZURE_SUBSCRIPTION_ID" \
  --arg resource_group "$AZURE_RESOURCE_GROUP" \
  --arg revision_suffix "$CONTAINER_APP_REVISION_SUFFIX" \
  --arg api_image "$API_IMAGE" \
  --arg alloy_image "$ALLOY_IMAGE" \
  --arg brevo_sender_name "${BREVO_SENDER_NAME:-D&D Campaign Manager}" \
  --arg eventgrid_topic_endpoint "$EVENTGRID_TOPIC_ENDPOINT" \
  --arg eventgrid_tenant_id "$EVENTGRID_TENANT_ID" \
  --arg eventgrid_audience "$EVENTGRID_AUDIENCE" \
  --arg character_storage_service_uri "$CHARACTER_STORAGE_SERVICE_URI" \
  --arg adventure_catalog_storage_service_uri "$ADVENTURE_CATALOG_STORAGE_SERVICE_URI" \
  --arg frontend_base_url "$FRONTEND_BASE_URL" \
  --arg frontend_origin "$FRONTEND_ORIGIN" \
  --arg grafana_cloud_otlp_endpoint "$GRAFANA_CLOUD_OTLP_ENDPOINT" \
  '
    def set_container_image($container_name; $image):
      .properties.template.containers |= map(
        if .name == $container_name then .image = $image else . end
      );
    def set_env_value($container_name; $env_name; $env_value):
      .properties.template.containers |= map(
        if .name == $container_name then
          .env |= map(if .name == $env_name then .value = $env_value else . end)
        else . end
      );
    .name = $app_name
    | .resourceGroup = $resource_group
    | .properties.template.revisionSuffix = $revision_suffix
    | set_container_image("api"; $api_image)
    | set_container_image("alloy"; $alloy_image)
    | set_env_value("api"; "Email__Brevo__SenderName"; $brevo_sender_name)
    | set_env_value("api"; "EventGrid__TopicEndpoint"; $eventgrid_topic_endpoint)
    | set_env_value("api"; "EventGrid__TenantId"; $eventgrid_tenant_id)
    | set_env_value("api"; "EventGrid__Audience"; $eventgrid_audience)
    | set_env_value("api"; "Storage__Characters__ServiceUri"; $character_storage_service_uri)
    | set_env_value("api"; "Storage__AdventureCatalog__ServiceUri"; $adventure_catalog_storage_service_uri)
    | set_env_value("api"; "Frontend__BaseUrl"; $frontend_base_url)
    | set_env_value("api"; "Cors__AllowedOrigins__0"; $frontend_origin)
    | set_env_value("api"; "OTEL_EXPORTER_OTLP_ENDPOINT"; $grafana_cloud_otlp_endpoint)
    | set_env_value("alloy"; "AZURE_SUBSCRIPTION_ID"; $azure_subscription_id)
    | set_env_value("alloy"; "GRAFANA_CLOUD_OTLP_ENDPOINT"; $grafana_cloud_otlp_endpoint)
  ' \
  "$container_app_source"
