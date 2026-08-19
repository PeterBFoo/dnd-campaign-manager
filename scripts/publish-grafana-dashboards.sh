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

folder_payload=$(jq -n \
  --arg uid "$folder_uid" \
  --arg title "D&D Campaign Companion" \
  '{uid: $uid, title: $title}')

folder_status=$(curl \
  --silent \
  --show-error \
  --output /tmp/dnd-grafana-folder-response.json \
  --write-out '%{http_code}' \
  --request POST \
  --header "Authorization: Bearer ${token}" \
  --header "Content-Type: application/json" \
  --data "$folder_payload" \
  "${grafana_url}/api/folders")

case "$folder_status" in
  200|409) ;;
  *)
    cat /tmp/dnd-grafana-folder-response.json >&2
    exit 1
    ;;
esac

for dashboard in "$dashboard_dir"/*.json
do
  payload_file=$(mktemp)
  jq \
    --arg folder_uid "$folder_uid" \
    '{dashboard: ., folderUid: $folder_uid, overwrite: true, message: "Provisioned from dnd-campaign-manager"}' \
    "$dashboard" > "$payload_file"

  curl \
    --fail \
    --silent \
    --show-error \
    --request POST \
    --header "Authorization: Bearer ${token}" \
    --header "Content-Type: application/json" \
    --data-binary "@${payload_file}" \
    "${grafana_url}/api/dashboards/db" >/dev/null

  rm -f "$payload_file"
  echo "Published dashboard: $(basename "$dashboard")"
done
