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

resource "azurerm_container_app" "postgres_exporter" {
  name                         = var.postgres_exporter_name
  container_app_environment_id = azurerm_container_app_environment.production.id
  resource_group_name          = azurerm_resource_group.production.name
  revision_mode                = "Single"
  tags                         = var.tags

  # GitHub Actions owns the runtime secrets and the immutable Alloy image.
  # Terraform only establishes the private application boundary and safe
  # placeholders for the first revision.
  secret {
    name  = "postgres-dsn"
    value = "postgresql://placeholder:placeholder@example.invalid/postgres?sslmode=require"
  }

  secret {
    name  = "grafana-cloud-authorization"
    value = "Basic placeholder"
  }

  template {
    min_replicas = 1
    max_replicas = 1

    container {
      name   = "postgres-exporter"
      image  = "quay.io/prometheuscommunity/postgres-exporter:v0.20.1"
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name        = "DATA_SOURCE_NAME"
        secret_name = "postgres-dsn"
      }

      env {
        name  = "PG_EXPORTER_COLLECTION_TIMEOUT"
        value = "30s"
      }

      env {
        name  = "PG_EXPORTER_DISABLE_DEFAULT_METRICS"
        value = "false"
      }
    }

    container {
      name   = "alloy"
      image  = "grafana/alloy:v1.19.2"
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "GRAFANA_CLOUD_OTLP_ENDPOINT"
        value = "https://invalid.example.invalid/otlp"
      }

      env {
        name        = "GRAFANA_CLOUD_AUTHORIZATION"
        secret_name = "grafana-cloud-authorization"
      }
    }
  }

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

resource "azurerm_storage_container" "adventure_module_images" {
  name                  = "adventure-module-images"
  storage_account_id    = azurerm_storage_account.character_images.id
  container_access_type = "private"
}

resource "azurerm_role_assignment" "api_character_images" {
  scope                = azurerm_storage_account.character_images.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_container_app.api.identity[0].principal_id
}

resource "azurerm_eventgrid_topic" "invitation_events" {
  name                = "dnd-campaign-invitation-events"
  location            = azurerm_resource_group.production.location
  resource_group_name = azurerm_resource_group.production.name
  input_schema        = "CloudEventSchemaV1_0"
  tags                = var.tags
}

resource "azurerm_storage_container" "eventgrid_deadletters" {
  name                  = "eventgrid-deadletters"
  storage_account_id    = azurerm_storage_account.character_images.id
  container_access_type = "private"
}

resource "azurerm_eventgrid_event_subscription" "invitation_events" {
  name  = "dnd-campaign-api-invitation-email"
  scope = azurerm_eventgrid_topic.invitation_events.id

  included_event_types = ["All"]

  webhook_endpoint {
    url                            = "https://${azurerm_container_app.api.ingress[0].fqdn}/internal/events/invitation-email"
    active_directory_tenant_id     = var.eventgrid_webhook_tenant_id
    active_directory_app_id_or_uri = var.eventgrid_webhook_audience
  }

  retry_policy {
    max_delivery_attempts = 30
    event_time_to_live    = 1440
  }

  storage_blob_dead_letter_destination {
    storage_account_id          = azurerm_storage_account.character_images.id
    storage_blob_container_name = azurerm_storage_container.eventgrid_deadletters.name
  }
}

resource "azurerm_role_assignment" "api_eventgrid_sender" {
  scope                = azurerm_eventgrid_topic.invitation_events.id
  role_definition_name = "EventGrid Data Sender"
  principal_id         = azurerm_container_app.api.identity[0].principal_id
}

output "eventgrid_topic_endpoint" {
  value       = azurerm_eventgrid_topic.invitation_events.endpoint
  description = "Event Grid topic endpoint used by the API publisher."
}
