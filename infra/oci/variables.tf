variable "tenancy_ocid" {
  description = "OCID de la tenancy de OCI."
  type        = string
}

variable "compartment_ocid" {
  description = "OCID del compartment donde se crearán los recursos. Puede ser la tenancy raíz."
  type        = string
}

variable "region" {
  description = "Región principal de OCI, por ejemplo eu-madrid-1."
  type        = string
}

variable "availability_domain" {
  description = "Availability Domain opcional. Si se omite se usa el primero disponible."
  type        = string
  default     = ""
}

variable "ssh_public_key" {
  description = "Clave pública SSH autorizada para el usuario ubuntu."
  type        = string
}

variable "ssh_allowed_cidr" {
  description = "CIDR autorizado a acceder por SSH; debe limitarse normalmente a una IP /32."
  type        = string

  validation {
    condition     = can(cidrnetmask(var.ssh_allowed_cidr)) && var.ssh_allowed_cidr != "0.0.0.0/0"
    error_message = "ssh_allowed_cidr debe ser un CIDR válido y no puede abrir SSH a todo Internet."
  }
}

variable "instance_ocpus" {
  description = "OCPU Ampere A1. El máximo Always Free actual de la tenancy es 2."
  type        = number
  default     = 2

  validation {
    condition     = var.instance_ocpus > 0 && var.instance_ocpus <= 2
    error_message = "instance_ocpus debe estar entre 1 y 2 para permanecer en Always Free."
  }
}

variable "instance_memory_gbs" {
  description = "Memoria de la instancia Ampere A1. El máximo Always Free actual es 12 GB."
  type        = number
  default     = 12

  validation {
    condition     = var.instance_memory_gbs >= 6 && var.instance_memory_gbs <= 12
    error_message = "instance_memory_gbs debe estar entre 6 y 12 GB."
  }
}

variable "boot_volume_size_gbs" {
  description = "Tamaño del volumen de arranque. Debe dejar margen dentro de los 200 GB Always Free."
  type        = number
  default     = 100

  validation {
    condition     = var.boot_volume_size_gbs >= 50 && var.boot_volume_size_gbs <= 150
    error_message = "boot_volume_size_gbs debe estar entre 50 y 150 GB."
  }
}
