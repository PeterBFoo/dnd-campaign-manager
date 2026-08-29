variable "location" {
  description = "Azure region used by the production platform."
  type        = string
  default     = "spaincentral"
}

variable "resource_group_name" {
  description = "Resource group containing the ADR-0001 production resources."
  type        = string
  default     = "dnd-campaign-manager-production"
}

variable "container_app_environment_name" {
  description = "Azure Container Apps consumption environment name."
  type        = string
  default     = "dnd-campaign-production"
}

variable "api_name" {
  description = "ASP.NET Container App name."
  type        = string
  default     = "dnd-campaign-api"
}

variable "character_storage_account_name" {
  description = "Globally unique Azure Storage account used for private character images."
  type        = string
  default     = "dndcampaignpbfimages"
}

variable "frontend_origin" {
  description = "Public Angular origin allowed by the API CORS policy."
  type        = string
  default     = "https://peterbfoo.github.io"

  validation {
    condition     = startswith(var.frontend_origin, "https://") && !endswith(var.frontend_origin, "/")
    error_message = "frontend_origin must be an HTTPS origin without a trailing slash."
  }
}

variable "eventgrid_webhook_tenant_id" {
  description = "Microsoft Entra tenant ID used to authenticate Event Grid webhook delivery."
  type        = string
  default     = ""
}

variable "eventgrid_webhook_audience" {
  description = "App ID URI/audience of the Event Grid webhook App Registration."
  type        = string
  default     = ""
}

variable "tags" {
  description = "Common resource tags."
  type        = map(string)
  default = {
    application = "dnd-campaign-manager"
    environment = "production"
    adr         = "ADR-0001"
    managed-by  = "terraform"
  }
}
