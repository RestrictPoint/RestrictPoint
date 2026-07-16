variable "redis_name" {
  description = "Redis cache name (must be globally unique)."
  type        = string
}

variable "entra_authentication_enabled" {
  description = "Enable Microsoft Entra token-based authentication for data-plane access."
  type        = bool
  default     = true
}

variable "location" {
  description = "Azure region."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group name."
  type        = string
}

variable "sku_name" {
  description = "Redis SKU: Basic (dev, no HA), Standard (dev, HA), Premium (prod, persistence + zones)."
  type        = string
  default     = "Basic"

  validation {
    condition     = contains(["Basic", "Standard", "Premium"], var.sku_name)
    error_message = "SKU must be Basic, Standard, or Premium."
  }
}

variable "family" {
  description = "Redis family: C (Basic/Standard) or P (Premium)."
  type        = string
  default     = "C"

  validation {
    condition     = contains(["C", "P"], var.family)
    error_message = "Family must be C (Basic/Standard) or P (Premium)."
  }
}

variable "capacity" {
  description = "Redis cache capacity (0-6 for C family, 1-5 for P family)."
  type        = number
  default     = 0 # C0 = 250MB

  validation {
    condition     = var.capacity >= 0 && var.capacity <= 6
    error_message = "Capacity must be 0-6."
  }
}

variable "zones" {
  description = "Availability zones (Premium SKU only)."
  type        = list(string)
  default     = null
}

variable "enable_aof_backup" {
  description = "Enable AOF (Append-Only File) persistence (Premium only)."
  type        = bool
  default     = false
}

variable "aof_storage_connection_string" {
  description = "Storage account connection string for AOF backups (Premium only)."
  type        = string
  default     = null
  sensitive   = true
}

variable "enable_rdb_backup" {
  description = "Enable RDB snapshot backups (Premium only)."
  type        = bool
  default     = false
}

variable "rdb_backup_frequency" {
  description = "RDB backup frequency in minutes: 15, 30, 60, 360, 720, 1440 (Premium only)."
  type        = number
  default     = 60
}

variable "rdb_storage_connection_string" {
  description = "Storage account connection string for RDB backups (Premium only)."
  type        = string
  default     = null
  sensitive   = true
}

variable "firewall_rules" {
  description = "List of firewall rules for dev (public endpoint). Empty in prod (private endpoint)."
  type = list(object({
    name     = string
    start_ip = string
    end_ip   = string
  }))
  default = []
}

variable "public_network_access_enabled" {
  description = "Enable public network access (true for dev, false for prod with private endpoint)."
  type        = bool
  default     = true
}

variable "tags" {
  description = "Resource tags."
  type        = map(string)
  default     = {}
}
