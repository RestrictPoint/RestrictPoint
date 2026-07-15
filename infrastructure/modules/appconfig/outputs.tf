output "appconfig_id" {
  description = "App Configuration resource ID."
  value       = azurerm_app_configuration.main.id
}

output "appconfig_name" {
  description = "App Configuration store name."
  value       = azurerm_app_configuration.main.name
}

output "endpoint" {
  description = "App Configuration endpoint."
  value       = azurerm_app_configuration.main.endpoint
}

output "primary_read_key" {
  description = "Primary read key (use Managed Identity instead when possible)."
  value       = azurerm_app_configuration.main.primary_read_key
  sensitive   = true
}

output "primary_write_key" {
  description = "Primary write key (use Managed Identity instead when possible)."
  value       = azurerm_app_configuration.main.primary_write_key
  sensitive   = true
}
