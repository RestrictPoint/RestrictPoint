variable "key_vault_id" {
  description = "Key Vault resource ID."
  type        = string
}

variable "sql_server_id" {
  description = "SQL Server resource ID."
  type        = string
}

variable "servicebus_namespace_id" {
  description = "Service Bus namespace resource ID."
  type        = string
}

variable "appconfig_id" {
  description = "App Configuration resource ID."
  type        = string
}

variable "redis_cache_id" {
  description = "Redis Cache resource ID."
  type        = string
}

variable "key_vault_secrets_user_principal_ids" {
  description = "Map of principal IDs that need Key Vault Secrets User access (e.g., {identity = principal_id, licensing = principal_id})."
  type        = map(string)
  default     = {}
}

variable "key_vault_crypto_user_principal_ids" {
  description = "Map of principal IDs that need Key Vault Crypto User access for signing operations (e.g., {licensing = principal_id})."
  type        = map(string)
  default     = {}
}

variable "sql_db_contributor_principal_ids" {
  description = "Map of principal IDs that need SQL DB Contributor access."
  type        = map(string)
  default     = {}
}

variable "servicebus_data_sender_principal_ids" {
  description = "Map of principal IDs that need Service Bus Data Sender access."
  type        = map(string)
  default     = {}
}

variable "servicebus_data_receiver_principal_ids" {
  description = "Map of principal IDs that need Service Bus Data Receiver access."
  type        = map(string)
  default     = {}
}

variable "appconfig_data_reader_principal_ids" {
  description = "Map of principal IDs that need App Configuration Data Reader access."
  type        = map(string)
  default     = {}
}

variable "redis_contributor_principal_ids" {
  description = "Map of principal IDs that need Redis Cache Contributor access."
  type        = map(string)
  default     = {}
}
