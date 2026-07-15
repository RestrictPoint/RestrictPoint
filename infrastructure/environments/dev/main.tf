locals {
  name_prefix = "rp-${var.environment}"

  tags = {
    project     = "RestrictPoint"
    environment = var.environment
    managed_by  = "terraform"
  }
}

# Use existing shared resource group created during Phase 0 bootstrap
data "azurerm_resource_group" "shared" {
  name = "RestrictPoint-Shared"
}

# Phase 0: Key Vault for secrets and signing keys
resource "azurerm_key_vault" "main" {
  name                       = "${local.name_prefix}-kv-${substr(md5(data.azurerm_resource_group.shared.id), 0, 6)}"
  location                   = data.azurerm_resource_group.shared.location
  resource_group_name        = data.azurerm_resource_group.shared.name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  soft_delete_retention_days = 30
  purge_protection_enabled   = false # Dev only; enable in prod

  rbac_authorization_enabled = true # Use RBAC instead of access policies

  tags = local.tags
}

# Phase 0: Log Analytics workspace for observability
resource "azurerm_log_analytics_workspace" "main" {
  name                = "${local.name_prefix}-logs"
  location            = data.azurerm_resource_group.shared.location
  resource_group_name = data.azurerm_resource_group.shared.name
  sku                 = "PerGB2018"
  retention_in_days   = 30 # Dev: 30 days; Prod: 90+
  daily_quota_gb      = 1  # Dev: cap ingestion to stay within free tier

  tags = local.tags
}

# Get current client (SP or user) for Key Vault RBAC
data "azurerm_client_config" "current" {}
