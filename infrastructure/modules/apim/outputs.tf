output "apim_id" {
  description = "API Management resource ID."
  value       = azurerm_api_management.main.id
}

output "apim_name" {
  description = "API Management service name."
  value       = azurerm_api_management.main.name
}

output "gateway_url" {
  description = "API Management gateway URL."
  value       = azurerm_api_management.main.gateway_url
}

output "developer_portal_url" {
  description = "API Management developer portal URL."
  value       = azurerm_api_management.main.developer_portal_url
}

output "management_api_url" {
  description = "API Management management API URL."
  value       = azurerm_api_management.main.management_api_url
}

output "principal_id" {
  description = "Managed Identity principal ID for RBAC assignments."
  value       = azurerm_api_management.main.identity[0].principal_id
}

output "public_ip_addresses" {
  description = "APIM public IP addresses."
  value       = azurerm_api_management.main.public_ip_addresses
}
