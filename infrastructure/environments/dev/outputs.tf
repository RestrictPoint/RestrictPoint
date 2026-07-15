output "resource_group_name" {
  description = "Primary resource group for the environment."
  value       = azurerm_resource_group.main.name
}
