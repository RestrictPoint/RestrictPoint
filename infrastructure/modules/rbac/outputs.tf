output "key_vault_secrets_user_assignments" {
  description = "Key Vault Secrets User role assignment IDs."
  value       = { for k, v in azurerm_role_assignment.key_vault_secrets_user : k => v.id }
}

output "key_vault_crypto_user_assignments" {
  description = "Key Vault Crypto User role assignment IDs."
  value       = { for k, v in azurerm_role_assignment.key_vault_crypto_user : k => v.id }
}

output "sql_db_contributor_assignments" {
  description = "SQL DB Contributor role assignment IDs."
  value       = { for k, v in azurerm_role_assignment.sql_db_contributor : k => v.id }
}

output "servicebus_data_sender_assignments" {
  description = "Service Bus Data Sender role assignment IDs."
  value       = { for k, v in azurerm_role_assignment.servicebus_data_sender : k => v.id }
}

output "servicebus_data_receiver_assignments" {
  description = "Service Bus Data Receiver role assignment IDs."
  value       = { for k, v in azurerm_role_assignment.servicebus_data_receiver : k => v.id }
}

output "appconfig_data_reader_assignments" {
  description = "App Configuration Data Reader role assignment IDs."
  value       = { for k, v in azurerm_role_assignment.appconfig_data_reader : k => v.id }
}

output "redis_contributor_assignments" {
  description = "Redis Cache Contributor role assignment IDs."
  value       = { for k, v in azurerm_role_assignment.redis_contributor : k => v.id }
}
