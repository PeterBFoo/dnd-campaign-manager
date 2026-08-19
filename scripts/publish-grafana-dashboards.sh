#!/bin/sh
set -eu

if [ -z "${GRAFANA_URL:-}" ]; then
  echo "GRAFANA_URL is required." >&2
  exit 2
fi

token_file=${GRAFANA_TOKEN_FILE:-}
if [ -z "$token_file" ] || [ ! -s "$token_file" ]; then
  echo "GRAFANA_TOKEN_FILE must point to a readable service-account token." >&2
  exit 2
fi

dashboard_dir=${DASHBOARD_DIR:-infra/observability/grafana/dashboards}
folder_uid=${GRAFANA_FOLDER_UID:-dnd-campaign-companion}
grafana_url=${GRAFANA_URL%/}
token=$(tr -d '\r\n' < "$token_file")
temporary_dir=$(mktemp -d)
trap 'rm -rf "$temporary_dir"' EXIT

datasources_file="$temporary_dir/datasources.json"
curl \
  --fail \
  --silent \
  --show-error \
  --header "Authorization: Bearer ${token}" \
  "${grafana_url}/api/datasources" > "$datasources_file"

prometheus_uid=$(jq -r '
  [.[] | select(.type == "prometheus")]
  | sort_by(if .isDefault then 0 else 1 end)
  | .[0].uid // empty
' "$datasources_file")

if [ -z "$prometheus_uid" ]; then
  echo "Grafana does not expose a Prometheus datasource to this service account." >&2
  exit 1
fi

folder_payload=$(jq -n \
  --arg uid "$folder_uid" \
  --arg title "D&D Campaign Companion" \
  '{uid: $uid, title: $title}')

folder_response="$temporary_dir/folder-response.json"
folder_status=$(curl \
  --silent \
  --show-error \
  --output "$folder_response" \
  --write-out '%{http_code}' \
  --request POST \
  --header "Authorization: Bearer ${token}" \
  --header "Content-Type: application/json" \
  --data "$folder_payload" \
  "${grafana_url}/api/folders")

case "$folder_status" in
  200|409) ;;
  *)
    jq -r '.message // "Grafana folder creation failed."' "$folder_response" >&2
    exit 1
    ;;
esac

for dashboard in "$dashboard_dir"/*.json
do
  normalized_dashboard="$temporary_dir/$(basename "$dashboard")"
  payload_file="$temporary_dir/$(basename "$dashboard" .json)-payload.json"
  jq \
    --arg prometheus_uid "$prometheus_uid" \
    'walk(
      if type == "object" and .type? == "prometheus" and has("uid")
      then .uid = $prometheus_uid
      else .
      end
    )' \
    "$dashboard" > "$normalized_dashboard"

  jq \
    --arg folder_uid "$folder_uid" \
    '{dashboard: ., folderUid: $folder_uid, overwrite: true, message: "Provisioned from dnd-campaign-manager"}' \
    "$normalized_dashboard" > "$payload_file"

  curl \
    --fail \
    --silent \
    --show-error \
    --request POST \
    --header "Authorization: Bearer ${token}" \
    --header "Content-Type: application/json" \
    --data-binary "@${payload_file}" \
    "${grafana_url}/api/dashboards/db" >/dev/null

  echo "Published dashboard: $(basename "$dashboard")"
done
