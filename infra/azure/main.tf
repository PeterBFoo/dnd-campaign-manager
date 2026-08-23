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

  identity {
    type = "SystemAssigned"
  }

  template {
    min_replicas = 1
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

resource "azurerm_storage_account" "character_images" {
  name                            = var.character_storage_account_name
  resource_group_name             = azurerm_resource_group.production.name
  location                        = azurerm_resource_group.production.location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  account_kind                    = "StorageV2"
  access_tier                     = "Hot"
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
  tags                            = var.tags

  blob_properties {
    delete_retention_policy {
      days = 7
    }
    container_delete_retention_policy {
      days = 7
    }
  }
}

resource "azurerm_storage_container" "character_images" {
  name                  = "character-images"
  storage_account_id    = azurerm_storage_account.character_images.id
  container_access_type = "private"
}

resource "azurerm_role_assignment" "api_character_images" {
  scope                = azurerm_storage_account.character_images.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_container_app.api.identity[0].principal_id
}
