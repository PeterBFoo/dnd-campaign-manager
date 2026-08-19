#!/bin/sh
set -eu

deployment_root=${DEPLOYMENT_ROOT:-/opt/dnd-campaign-manager}
release_dir=${RELEASE_DIR:-${deployment_root}/release}
environment_file=${ENVIRONMENT_FILE:-${deployment_root}/config/production.env}
compose_file=${release_dir}/compose.deploy.yaml

for required_file in \
  "$compose_file" \
  "$environment_file" \
  "${deployment_root}/secrets/postgres_password" \
  "${deployment_root}/secrets/grafana_cloud_otlp_authorization"
do
  if [ ! -s "$required_file" ]; then
    echo "Required deployment file is missing or empty: $required_file" >&2
    exit 2
  fi
done

if [ -z "${API_IMAGE:-}" ] || [ -z "${WEB_IMAGE:-}" ]; then
  echo "API_IMAGE and WEB_IMAGE must identify immutable release images." >&2
  exit 2
fi

case "${API_IMAGE}:${WEB_IMAGE}" in
  *:latest*)
    echo "Production deployment refuses mutable latest image tags." >&2
    exit 2
    ;;
esac

cd "$release_dir"

export API_IMAGE WEB_IMAGE
export SECRETS_DIR="${deployment_root}/secrets"

docker compose --env-file "$environment_file" -f "$compose_file" config --quiet
docker compose --env-file "$environment_file" -f "$compose_file" pull
docker compose --env-file "$environment_file" -f "$compose_file" up \
  --detach \
  --remove-orphans \
  --wait \
  --wait-timeout 240

docker compose --env-file "$environment_file" -f "$compose_file" ps
