resource "azurerm_cdn_frontdoor_profile" "main" {
  name                = var.profile_name
  resource_group_name = var.resource_group_name
  sku_name            = var.sku_name # Standard_AzureFrontDoor (dev) or Premium_AzureFrontDoor (prod)

  tags = var.tags
}

# WAF policy (Standard: custom rules only; Premium: managed OWASP rules)
resource "azurerm_cdn_frontdoor_firewall_policy" "main" {
  count = var.enable_waf ? 1 : 0

  name                              = "${replace(var.profile_name, "-", "")}waf"
  resource_group_name               = var.resource_group_name
  sku_name                          = var.sku_name
  enabled                           = true
  mode                              = var.waf_mode # Prevention or Detection
  redirect_url                      = var.waf_redirect_url
  custom_block_response_status_code = 403
  custom_block_response_body        = base64encode("Access denied by WAF policy")

  tags = var.tags
}

# Endpoint placeholder (origins and routes added in later phases when backends exist)
resource "azurerm_cdn_frontdoor_endpoint" "placeholder" {
  name                     = "placeholder"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.main.id

  tags = var.tags
}
