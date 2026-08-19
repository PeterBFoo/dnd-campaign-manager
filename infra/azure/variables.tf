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

variable "frontend_origin" {
  description = "Public Angular origin allowed by the API CORS policy."
  type        = string
  default     = "https://peterbfoo.github.io"

  validation {
    condition     = startswith(var.frontend_origin, "https://") && !endswith(var.frontend_origin, "/")
    error_message = "frontend_origin must be an HTTPS origin without a trailing slash."
  }
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
