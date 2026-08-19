resource "azurerm_resource_group" "production" {
  name     = var.resource_group_name
  location = var.location
  tags     = var.tags
}

resource "azurerm_container_app_environment" "production" {
  name                = var.container_app_environment_name
  location            = azurerm_resource_group.production.location
  resource_group_name = azurerm_resource_group.production.name

  # Application telemetry is exported directly to Grafana Cloud. Omitting a
  # Log Analytics workspace avoids an always-on paid logging dependency.
  tags = var.tags
}

resource "azurerm_container_app" "api" {
  name                         = var.api_name
  container_app_environment_id = azurerm_container_app_environment.production.id
  resource_group_name          = azurerm_resource_group.production.name
  revision_mode                = "Single"
  tags                         = var.tags

  template {
    min_replicas = 0
    max_replicas = 1

    container {
      name   = "api"
      image  = "mcr.microsoft.com/dotnet/samples:aspnetapp"
      cpu    = 0.25
      memory = "0.5Gi"
    }
  }

  ingress {
    external_enabled           = true
    allow_insecure_connections = false
    target_port                = 8080
    transport                  = "auto"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  # GitHub Actions owns immutable application revisions and runtime secrets.
  # Terraform owns only the cost boundary and stable platform resources.
  lifecycle {
    ignore_changes = [template, secret]
  }
}
