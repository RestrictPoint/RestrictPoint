resource "azurerm_api_management" "main" {
  name                = var.apim_name
  location            = var.location
  resource_group_name = var.resource_group_name
  publisher_name      = var.publisher_name
  publisher_email     = var.publisher_email
  sku_name            = var.sku_name # Consumption_0 (dev) or Standard_1/Premium_1 (prod)

  # Managed Identity for accessing Key Vault, Service Bus, etc.
  identity {
    type = "SystemAssigned"
  }

  # Consumption tier does not support VNet integration
  # Standard/Premium can use virtual_network_type = "Internal" or "External"

  # Minimum TLS version
  min_api_version = "2021-08-01"

  tags = var.tags
}

# Named values (variables accessible in policies)
resource "azurerm_api_management_named_value" "named_values" {
  for_each = var.named_values

  name                = each.key
  resource_group_name = var.resource_group_name
  api_management_name = azurerm_api_management.main.name
  display_name        = each.key
  value               = each.value.value
  secret              = each.value.secret
}
