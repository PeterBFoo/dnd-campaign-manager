#!/bin/sh
set -eu

if [ -z "${BASE_URL:-}" ]; then
  echo "BASE_URL is required, for example https://app.example.com" >&2
  exit 2
fi

base_url=${BASE_URL%/}

curl --fail --silent --show-error "${base_url}/" >/dev/null

live_payload=$(curl --fail --silent --show-error "${base_url}/health/live")
printf '%s' "$live_payload" | jq -e '.status == "healthy"' >/dev/null

ready_payload=$(curl --fail --silent --show-error "${base_url}/health/ready")
printf '%s' "$ready_payload" | jq -e '.status == "healthy" and (.checks | any(.name == "postgres" and .status == "healthy"))' >/dev/null

status_payload=$(curl --fail --silent --show-error "${base_url}/api/v1/platform/status")
printf '%s' "$status_payload" | jq -e '.status == "operational" and .dependencies.database == "connected" and .dependencies.telemetry == "otlp"' >/dev/null

if [ -n "${GRAFANA_URL:-}" ]; then
  grafana_url=${GRAFANA_URL%/}
  grafana_payload=$(curl --fail --silent --show-error "${grafana_url}/api/health")
  printf '%s' "$grafana_payload" | jq -e '.database == "ok"' >/dev/null
fi

echo "Deployment smoke test passed for ${base_url}"
