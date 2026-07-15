output "function_app_id" {
  description = "Function App resource ID."
  value       = azurerm_linux_function_app.main.id
}

output "function_app_name" {
  description = "Function App name."
  value       = azurerm_linux_function_app.main.name
}

output "function_app_hostname" {
  description = "Function App default hostname."
  value       = azurerm_linux_function_app.main.default_hostname
}

output "principal_id" {
  description = "Managed Identity principal ID for RBAC assignments."
  value       = azurerm_linux_function_app.main.identity[0].principal_id
}

output "app_insights_id" {
  description = "Application Insights resource ID."
  value       = azurerm_application_insights.main.id
}

output "app_insights_instrumentation_key" {
  description = "Application Insights instrumentation key."
  value       = azurerm_application_insights.main.instrumentation_key
  sensitive   = true
}

output "app_insights_connection_string" {
  description = "Application Insights connection string."
  value       = azurerm_application_insights.main.connection_string
  sensitive   = true
}

output "storage_account_id" {
  description = "Storage account resource ID."
  value       = azurerm_storage_account.main.id
}

output "storage_account_name" {
  description = "Storage account name."
  value       = azurerm_storage_account.main.name
}
