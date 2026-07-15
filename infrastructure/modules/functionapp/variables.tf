variable "function_app_name" {
  description = "Function App name (must be globally unique)."
  type        = string
}

variable "service_plan_name" {
  description = "App Service Plan name."
  type        = string
}

variable "storage_account_name" {
  description = "Storage account name for Function App runtime (must be globally unique, 3-24 chars, lowercase alphanumeric)."
  type        = string

  validation {
    condition     = can(regex("^[a-z0-9]{3,24}$", var.storage_account_name))
    error_message = "Storage account name must be 3-24 lowercase alphanumeric characters."
  }
}

variable "app_insights_name" {
  description = "Application Insights name."
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

variable "log_analytics_workspace_id" {
  description = "Log Analytics workspace ID for diagnostic settings."
  type        = string
}

variable "sku_name" {
  description = "App Service Plan SKU. Dev: Y1 (Consumption), Prod: EP1/EP2/EP3 (Elastic Premium)."
  type        = string
  default     = "Y1"

  validation {
    condition     = can(regex("^(Y1|EP1|EP2|EP3)$", var.sku_name))
    error_message = "SKU must be Y1 (Consumption) or EP1/EP2/EP3 (Elastic Premium)."
  }
}

variable "zone_redundant" {
  description = "Enable zone redundancy (Premium only)."
  type        = bool
  default     = false
}

variable "app_settings" {
  description = "Additional application settings (merged with default settings)."
  type        = map(string)
  default     = {}
}

variable "cors_allowed_origins" {
  description = "CORS allowed origins."
  type        = list(string)
  default     = []
}

variable "tags" {
  description = "Resource tags."
  type        = map(string)
  default     = {}
}
