resource "azurerm_app_configuration" "main" {
  name                = var.appconfig_name
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = var.sku # Free (dev) or Standard (prod)

  # Disable local auth - use Managed Identity only
  local_auth_enabled = false

  # Soft delete retention
  soft_delete_retention_days = var.soft_delete_retention_days
  purge_protection_enabled   = var.purge_protection_enabled

  tags = var.tags
}
