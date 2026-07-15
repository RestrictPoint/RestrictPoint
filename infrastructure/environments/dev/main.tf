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

# Phase 1: Service Bus for event-driven architecture
module "servicebus" {
  source = "../../modules/servicebus"

  namespace_name      = "${local.name_prefix}-servicebus"
  location            = data.azurerm_resource_group.shared.location
  resource_group_name = data.azurerm_resource_group.shared.name
  sku                 = "Standard" # Dev: Standard, Prod: Premium

  tags = local.tags
}

# Phase 1: Redis cache for session/license/config caching
module "redis" {
  source = "../../modules/redis"

  redis_name          = "${local.name_prefix}-redis-${substr(md5(data.azurerm_resource_group.shared.id), 0, 6)}"
  location            = data.azurerm_resource_group.shared.location
  resource_group_name = data.azurerm_resource_group.shared.name
  sku_name            = "Basic"    # Dev: Basic C0, Prod: Premium
  family              = "C"
  capacity            = 0          # C0 = 250MB

  public_network_access_enabled = true # Dev: public with firewall, Prod: private endpoint

  tags = local.tags
}

# Phase 1: Azure SQL for transactional data
module "sql" {
  source = "../../modules/sql"

  server_name         = "${local.name_prefix}-sql-${substr(md5("${data.azurerm_resource_group.shared.id}-centralus"), 0, 6)}"
  database_name       = "RestrictPoint"
  location            = "centralus"
  resource_group_name = data.azurerm_resource_group.shared.name
  sku_name            = "GP_S_Gen5_1" # Serverless: 1 vCore, auto-pause
  max_size_gb         = 32
  min_capacity        = 0.5
  auto_pause_delay_in_minutes = 60 # Dev: auto-pause after 1 hour idle

  aad_admin_login     = "RestrictPoint Service Principal"
  aad_admin_object_id = data.azurerm_client_config.current.object_id

  public_network_access_enabled = true # Dev: public with firewall, Prod: private endpoint

  tags = local.tags
}

# Phase 1: App Configuration for feature flags and non-sensitive config
module "appconfig" {
  source = "../../modules/appconfig"

  appconfig_name      = "${local.name_prefix}-appconfig"
  location            = data.azurerm_resource_group.shared.location
  resource_group_name = data.azurerm_resource_group.shared.name
  sku                 = "free" # Dev: free, Prod: standard

  tags = local.tags
}

# Phase 1: API Management gateway
module "apim" {
  source = "../../modules/apim"

  apim_name           = "${local.name_prefix}-apim"
  location            = data.azurerm_resource_group.shared.location
  resource_group_name = data.azurerm_resource_group.shared.name
  sku_name            = "Consumption_0" # Dev: Consumption, Prod: Standard/Premium
  publisher_name      = "RestrictPoint"
  publisher_email     = "admin@restrictpoint.com"

  tags = local.tags
}

# Phase 1: Front Door for global routing and WAF
module "frontdoor" {
  source = "../../modules/frontdoor"

  profile_name        = "${local.name_prefix}-fd"
  resource_group_name = data.azurerm_resource_group.shared.name
  sku_name            = "Standard_AzureFrontDoor" # Dev: Standard, Prod: Premium (managed OWASP WAF)
  enable_waf          = true
  waf_mode            = "Prevention"

  tags = local.tags
}

