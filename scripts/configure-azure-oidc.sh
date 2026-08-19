#!/bin/sh
set -eu

azure_cli=${AZURE_CLI:-/opt/homebrew/bin/az}
application_name=dnd-campaign-manager-github
resource_group_name=${AZURE_RESOURCE_GROUP:-dnd-campaign-manager-production}

application_id=$($azure_cli ad app list \
  --display-name "$application_name" \
  --query "[0].appId" \
  --output tsv)

if [ -z "$application_id" ]; then
  application_id=$($azure_cli ad app create \
    --display-name "$application_name" \
    --query appId \
    --output tsv)
fi

application_object_id=$($azure_cli ad app show \
  --id "$application_id" \
  --query id \
  --output tsv)

service_principal_id=$($azure_cli ad sp list \
  --filter "appId eq '$application_id'" \
  --query "[0].id" \
  --output tsv)

if [ -z "$service_principal_id" ]; then
  service_principal_id=$($azure_cli ad sp create \
    --id "$application_id" \
    --query id \
    --output tsv)
fi

federated_credential_id=$($azure_cli ad app federated-credential list \
  --id "$application_object_id" \
  --query "[?name=='github-production'].id | [0]" \
  --output tsv)

if [ -z "$federated_credential_id" ]; then
  $azure_cli ad app federated-credential create \
    --id "$application_object_id" \
    --parameters '{"name":"github-production","issuer":"https://token.actions.githubusercontent.com","subject":"repo:PeterBFoo/dnd-campaign-manager:environment:production","description":"GitHub Actions production environment","audiences":["api://AzureADTokenExchange"]}' \
    --output none
fi

resource_group_scope=$($azure_cli group show \
  --name "$resource_group_name" \
  --query id \
  --output tsv)

role_assignment_id=$($azure_cli role assignment list \
  --assignee "$service_principal_id" \
  --scope "$resource_group_scope" \
  --role Contributor \
  --query "[0].id" \
  --output tsv)

if [ -z "$role_assignment_id" ]; then
  $azure_cli role assignment create \
    --assignee-object-id "$service_principal_id" \
    --assignee-principal-type ServicePrincipal \
    --role Contributor \
    --scope "$resource_group_scope" \
    --output none
fi

tenant_id=$($azure_cli account show --query tenantId --output tsv)
subscription_id=$($azure_cli account show --query id --output tsv)

printf 'AZURE_CLIENT_ID=%s\n' "$application_id"
printf 'AZURE_TENANT_ID=%s\n' "$tenant_id"
printf 'AZURE_SUBSCRIPTION_ID=%s\n' "$subscription_id"
printf 'AZURE_RESOURCE_GROUP=%s\n' "$resource_group_name"
