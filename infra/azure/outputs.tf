output "api_url" {
  description = "Public base URL of the ASP.NET API."
  value       = "https://${azurerm_container_app.api.ingress[0].fqdn}"
}

output "container_app_name" {
  description = "Container App updated by the production workflow."
  value       = azurerm_container_app.api.name
}

output "postgres_exporter_container_app_name" {
  description = "Private Container App running PostgreSQL exporter and Grafana Alloy."
  value       = azurerm_container_app.postgres_exporter.name
}

output "resource_group_name" {
  description = "Resource group scope used for least-privilege deployment access."
  value       = azurerm_resource_group.production.name
}

output "character_storage_service_uri" {
  description = "Private character image Blob service endpoint consumed with managed identity."
  value       = azurerm_storage_account.character_images.primary_blob_endpoint
}
