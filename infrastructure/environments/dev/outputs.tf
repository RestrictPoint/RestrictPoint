output "resource_group_name" {
  description = "Dev environment resource group name."
  value       = azurerm_resource_group.dev.name
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

# Phase 1 outputs
output "servicebus_namespace_name" {
  description = "Service Bus namespace name."
  value       = module.servicebus.namespace_name
}

output "servicebus_endpoint" {
  description = "Service Bus endpoint."
  value       = module.servicebus.endpoint
}

output "redis_hostname" {
  description = "Redis hostname."
  value       = module.redis.hostname
}

output "redis_ssl_port" {
  description = "Redis SSL port."
  value       = module.redis.ssl_port
}

output "sql_server_fqdn" {
  description = "SQL Server FQDN."
  value       = module.sql.server_fqdn
}

output "sql_database_name" {
  description = "SQL Database name."
  value       = module.sql.database_name
}

output "appconfig_endpoint" {
  description = "App Configuration endpoint."
  value       = module.appconfig.endpoint
}

output "apim_gateway_url" {
  description = "API Management gateway URL."
  value       = module.apim.gateway_url
}

output "apim_principal_id" {
  description = "APIM Managed Identity principal ID for RBAC assignments."
  value       = module.apim.principal_id
}

output "frontdoor_endpoint" {
  description = "Front Door placeholder endpoint (replace when backends exist)."
  value       = module.frontdoor.endpoint_hostname
}

# Phase 2 outputs - Function Apps
output "func_identity_hostname" {
  description = "Identity Function App hostname."
  value       = module.func_identity.function_app_hostname
}

output "func_identity_principal_id" {
  description = "Identity Function App Managed Identity principal ID."
  value       = module.func_identity.principal_id
}

output "func_licensing_hostname" {
  description = "Licensing Function App hostname."
  value       = module.func_licensing.function_app_hostname
}

output "func_licensing_principal_id" {
  description = "Licensing Function App Managed Identity principal ID."
  value       = module.func_licensing.principal_id
}

output "func_billing_hostname" {
  description = "Billing Function App hostname."
  value       = module.func_billing.function_app_hostname
}

output "func_billing_principal_id" {
  description = "Billing Function App Managed Identity principal ID."
  value       = module.func_billing.principal_id
}

output "func_marketplace_hostname" {
  description = "Marketplace Function App hostname."
  value       = module.func_marketplace.function_app_hostname
}

output "func_marketplace_principal_id" {
  description = "Marketplace Function App Managed Identity principal ID."
  value       = module.func_marketplace.principal_id
}

output "func_notifications_hostname" {
  description = "Notifications Function App hostname."
  value       = module.func_notifications.function_app_hostname
}

output "func_notifications_principal_id" {
  description = "Notifications Function App Managed Identity principal ID."
  value       = module.func_notifications.principal_id
}

output "func_analytics_hostname" {
  description = "Analytics Function App hostname."
  value       = module.func_analytics.function_app_hostname
}

output "func_analytics_principal_id" {
  description = "Analytics Function App Managed Identity principal ID."
  value       = module.func_analytics.principal_id
}

