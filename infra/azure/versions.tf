terraform {
  required_version = ">= 1.10.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "4.77.0"
    }
  }
}

provider "azurerm" {
  features {}

  # The subscription registers only the providers required by this ADR.
  # Avoid AzureRM's default registration of unrelated paid services.
  resource_provider_registrations = "none"
}
