#!/bin/sh
set -eu

if [ -z "${API_BASE_URL:-}" ]; then
  echo "API_BASE_URL is required" >&2
  exit 2
fi

api_base_url=${API_BASE_URL%/}

live_payload=$(curl --retry 8 --retry-all-errors --retry-delay 5 --fail --silent --show-error "${api_base_url}/health/live")
printf '%s' "$live_payload" | jq -e '.status == "healthy"' >/dev/null

ready_payload=$(curl --fail --silent --show-error "${api_base_url}/health/ready")
printf '%s' "$ready_payload" | jq -e '.status == "healthy" and (.checks | any(.name == "postgres" and .status == "healthy"))' >/dev/null

status_payload=$(curl --fail --silent --show-error "${api_base_url}/api/v1/platform/status")
printf '%s' "$status_payload" | jq -e '.status == "operational" and .dependencies.database == "connected" and .dependencies.telemetry == "otlp"' >/dev/null

echo "API smoke test passed for ${api_base_url}"
