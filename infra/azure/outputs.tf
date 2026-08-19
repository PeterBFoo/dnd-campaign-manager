output "api_url" {
  description = "Public base URL of the ASP.NET API."
  value       = "https://${azurerm_container_app.api.ingress[0].fqdn}"
}

output "container_app_name" {
  description = "Container App updated by the production workflow."
  value       = azurerm_container_app.api.name
}

output "resource_group_name" {
  description = "Resource group scope used for least-privilege deployment access."
  value       = azurerm_resource_group.production.name
}
