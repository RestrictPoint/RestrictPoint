variable "server_name" {
  description = "SQL Server name (must be globally unique)."
  type        = string
}

variable "database_name" {
  description = "SQL Database name."
  type        = string
  default     = "RestrictPoint"
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
  description = "Database SKU. Dev: GP_S_Gen5_1 (serverless), Prod: GP_Gen5_2+ or BC_Gen5_2+."
  type        = string
  default     = "GP_S_Gen5_1" # Serverless: 1 vCore, auto-pause
}

variable "max_size_gb" {
  description = "Maximum database size in GB."
  type        = number
  default     = 32
}

variable "min_capacity" {
  description = "Minimum vCores for serverless (0.5, 0.75, 1, 1.25, etc.)."
  type        = number
  default     = 0.5
}

variable "auto_pause_delay_in_minutes" {
  description = "Auto-pause delay in minutes for serverless. -1 = disabled, 60-10080 = enabled. Dev: 60, Prod: -1."
  type        = number
  default     = 60
}

variable "zone_redundant" {
  description = "Enable zone redundancy (prod only)."
  type        = bool
  default     = false
}

variable "aad_admin_login" {
  description = "Entra ID admin login name (user or service principal)."
  type        = string
}

variable "aad_admin_object_id" {
  description = "Entra ID admin object ID."
  type        = string
}

variable "azuread_only_authentication" {
  description = "Enforce Entra ID-only authentication (disable SQL auth)."
  type        = bool
  default     = true
}

variable "public_network_access_enabled" {
  description = "Enable public network access. Dev: true (with firewall), Prod: false (private endpoint)."
  type        = bool
  default     = true
}

variable "firewall_rules" {
  description = "Firewall rules for dev (public endpoint)."
  type = list(object({
    name     = string
    start_ip = string
    end_ip   = string
  }))
  default = []
}

variable "backup_retention_days" {
  description = "Point-in-time restore retention (7-35 days)."
  type        = number
  default     = 7
}

variable "long_term_retention_weekly" {
  description = "Weekly backup retention (e.g., 'P1W' = 1 week)."
  type        = string
  default     = null
}

variable "long_term_retention_monthly" {
  description = "Monthly backup retention (e.g., 'P1M' = 1 month)."
  type        = string
  default     = null
}

variable "long_term_retention_yearly" {
  description = "Yearly backup retention (e.g., 'P1Y' = 1 year)."
  type        = string
  default     = null
}

variable "long_term_retention_week_of_year" {
  description = "Week of year for yearly backup (1-52)."
  type        = number
  default     = 1
}

variable "tags" {
  description = "Resource tags."
  type        = map(string)
  default     = {}
}
