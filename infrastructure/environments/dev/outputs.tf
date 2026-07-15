output "resource_group_name" {
  description = "Shared resource group name."
  value       = data.azurerm_resource_group.shared.name
}

output "key_vault_id" {
  description = "Key Vault resource ID."
  value       = azurerm_key_vault.main.id
}

output "key_vault_uri" {
  description = "Key Vault URI for application access."
  value       = azurerm_key_vault.main.vault_uri
}

output "log_analytics_workspace_id" {
  description = "Log Analytics workspace ID for Application Insights connection."
  value       = azurerm_log_analytics_workspace.main.id
}

output "log_analytics_workspace_key" {
  description = "Log Analytics workspace instrumentation key."
  value       = azurerm_log_analytics_workspace.main.primary_shared_key
  sensitive   = true
}
