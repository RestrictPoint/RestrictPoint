variable "appconfig_name" {
  description = "App Configuration store name (must be globally unique)."
  type        = string
}

variable "location" {
  description = "Azure region."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group name."
  type        = string
}

variable "sku" {
  description = "App Configuration SKU: free (dev) or standard (prod)."
  type        = string
  default     = "free"

  validation {
    condition     = contains(["free", "standard"], var.sku)
    error_message = "SKU must be 'free' or 'standard'."
  }
}

variable "public_network_access_enabled" {
  description = "Enable public network access. Dev: true, Prod: false (private endpoint)."
  type        = bool
  default     = true
}

variable "soft_delete_retention_days" {
  description = "Soft delete retention period in days (1-7). Disabled when 0."
  type        = number
  default     = 7
}

variable "purge_protection_enabled" {
  description = "Enable purge protection (prevents deletion during retention period). Prod only."
  type        = bool
  default     = false
}

variable "tags" {
  description = "Resource tags."
  type        = map(string)
  default     = {}
}
