locals {
  name_prefix = "rp-${var.environment}"

  # Deterministic suffix for globally-unique resource names (Key Vault, Redis, SQL)
  hash_suffix = substr(md5("RestrictPoint-${var.environment}"), 0, 6)

  tags = {
    project     = "RestrictPoint"
    environment = var.environment
    managed_by  = "terraform"
  }
}

# Environment-scoped resource group. RestrictPoint-Shared holds only cross-environment
# resources (DNS zone, Terraform state storage, Entra External ID link).
resource "azurerm_resource_group" "dev" {
  name     = "RestrictPoint-Dev"
  location = var.location

  tags = local.tags
}

# Phase 0: Key Vault for secrets and signing keys
resource "azurerm_key_vault" "main" {
  name                       = "${local.name_prefix}-kv-${local.hash_suffix}"
  location                   = azurerm_resource_group.dev.location
  resource_group_name        = azurerm_resource_group.dev.name
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
  location            = azurerm_resource_group.dev.location
  resource_group_name = azurerm_resource_group.dev.name
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
  location            = azurerm_resource_group.dev.location
  resource_group_name = azurerm_resource_group.dev.name
  sku                 = "Standard" # Dev: Standard, Prod: Premium

  tags = local.tags
}

# Phase 1: Redis cache for session/license/config caching
module "redis" {
  source = "../../modules/redis"

  redis_name          = "${local.name_prefix}-redis-${local.hash_suffix}"
  location            = azurerm_resource_group.dev.location
  resource_group_name = azurerm_resource_group.dev.name
  sku_name            = "Basic" # Dev: Basic C0, Prod: Premium
  family              = "C"
  capacity            = 0 # C0 = 250MB

  public_network_access_enabled = true # Dev: public with firewall, Prod: private endpoint

  tags = local.tags
}

# Phase 1: Azure SQL for transactional data
module "sql" {
  source = "../../modules/sql"

  server_name                 = "${local.name_prefix}-sql-${local.hash_suffix}"
  database_name               = "RestrictPoint"
  location                    = "centralus"
  resource_group_name         = azurerm_resource_group.dev.name
  sku_name                    = "GP_S_Gen5_1" # Serverless: 1 vCore, auto-pause
  max_size_gb                 = 32
  min_capacity                = 0.5
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
  location            = azurerm_resource_group.dev.location
  resource_group_name = azurerm_resource_group.dev.name
  sku                 = "free" # Dev: free, Prod: standard

  tags = local.tags
}

# Phase 1: API Management gateway
module "apim" {
  source = "../../modules/apim"

  apim_name           = "${local.name_prefix}-apim"
  location            = azurerm_resource_group.dev.location
  resource_group_name = azurerm_resource_group.dev.name
  sku_name            = "Consumption_0" # Dev: Consumption, Prod: Standard/Premium
  publisher_name      = "RestrictPoint"
  publisher_email     = "admin@restrictpoint.com"

  tags = local.tags
}

# Phase 1: Front Door for global routing and WAF
module "frontdoor" {
  source = "../../modules/frontdoor"

  profile_name        = "${local.name_prefix}-fd"
  resource_group_name = azurerm_resource_group.dev.name
  sku_name            = "Standard_AzureFrontDoor" # Dev: Standard, Prod: Premium (managed OWASP WAF)
  enable_waf          = true
  waf_mode            = "Prevention"

  tags = local.tags
}

# Phase 2: Function Apps with Managed Identity

# Identity Service
module "func_identity" {
  source = "../../modules/functionapp"

  function_app_name          = "${local.name_prefix}-func-identity"
  service_plan_name          = "${local.name_prefix}-plan-identity"
  storage_account_name       = "${replace(local.name_prefix, "-", "")}stidentity"
  app_insights_name          = "${local.name_prefix}-ai-identity"
  location                   = "centralus" # MPN subscription: Y1 quota unavailable in eastus
  resource_group_name        = azurerm_resource_group.dev.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  sku_name                   = "Y1" # Consumption

  tags = local.tags
}

# Licensing Service
module "func_licensing" {
  source = "../../modules/functionapp"

  function_app_name          = "${local.name_prefix}-func-licensing"
  service_plan_name          = "${local.name_prefix}-plan-licensing"
  storage_account_name       = "${replace(local.name_prefix, "-", "")}stlicensing"
  app_insights_name          = "${local.name_prefix}-ai-licensing"
  location                   = "centralus"
  resource_group_name        = azurerm_resource_group.dev.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  sku_name                   = "Y1"

  tags = local.tags
}

# Billing Service
module "func_billing" {
  source = "../../modules/functionapp"

  function_app_name          = "${local.name_prefix}-func-billing"
  service_plan_name          = "${local.name_prefix}-plan-billing"
  storage_account_name       = "${replace(local.name_prefix, "-", "")}stbilling"
  app_insights_name          = "${local.name_prefix}-ai-billing"
  location                   = "centralus"
  resource_group_name        = azurerm_resource_group.dev.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  sku_name                   = "Y1"

  tags = local.tags
}

# Marketplace Service
module "func_marketplace" {
  source = "../../modules/functionapp"

  function_app_name          = "${local.name_prefix}-func-marketplace"
  service_plan_name          = "${local.name_prefix}-plan-marketplace"
  storage_account_name       = "${replace(local.name_prefix, "-", "")}stmarketplace"
  app_insights_name          = "${local.name_prefix}-ai-marketplace"
  location                   = "centralus"
  resource_group_name        = azurerm_resource_group.dev.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  sku_name                   = "Y1"

  tags = local.tags
}

# Notifications Service
module "func_notifications" {
  source = "../../modules/functionapp"

  function_app_name          = "${local.name_prefix}-func-notifications"
  service_plan_name          = "${local.name_prefix}-plan-notifications"
  storage_account_name       = "${replace(local.name_prefix, "-", "")}stnotifications"
  app_insights_name          = "${local.name_prefix}-ai-notifications"
  location                   = "centralus"
  resource_group_name        = azurerm_resource_group.dev.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  sku_name                   = "Y1"

  tags = local.tags
}

# Analytics Service
module "func_analytics" {
  source = "../../modules/functionapp"

  function_app_name          = "${local.name_prefix}-func-analytics"
  service_plan_name          = "${local.name_prefix}-plan-analytics"
  storage_account_name       = "${replace(local.name_prefix, "-", "")}stanalytics"
  app_insights_name          = "${local.name_prefix}-ai-analytics"
  location                   = "centralus"
  resource_group_name        = azurerm_resource_group.dev.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  sku_name                   = "Y1"

  tags = local.tags
}

# Phase 2: RBAC assignments for Function Apps
module "rbac" {
  source = "../../modules/rbac"

  key_vault_id            = azurerm_key_vault.main.id
  sql_server_id           = module.sql.server_id
  servicebus_namespace_id = module.servicebus.namespace_id
  appconfig_id            = module.appconfig.appconfig_id
  redis_cache_id          = module.redis.redis_id

  # All Function Apps need Key Vault Secrets User access
  key_vault_secrets_user_principal_ids = {
    identity      = module.func_identity.principal_id
    licensing     = module.func_licensing.principal_id
    billing       = module.func_billing.principal_id
    marketplace   = module.func_marketplace.principal_id
    notifications = module.func_notifications.principal_id
    analytics     = module.func_analytics.principal_id
  }

  # Licensing Function needs Key Vault Crypto User for ES256 signing
  key_vault_crypto_user_principal_ids = {
    licensing = module.func_licensing.principal_id
  }

  # All Function Apps need SQL access (each has its own schema)
  sql_db_contributor_principal_ids = {
    identity      = module.func_identity.principal_id
    licensing     = module.func_licensing.principal_id
    billing       = module.func_billing.principal_id
    marketplace   = module.func_marketplace.principal_id
    notifications = module.func_notifications.principal_id
    analytics     = module.func_analytics.principal_id
  }

  # All Function Apps can send and receive Service Bus messages
  servicebus_data_sender_principal_ids = {
    identity      = module.func_identity.principal_id
    licensing     = module.func_licensing.principal_id
    billing       = module.func_billing.principal_id
    marketplace   = module.func_marketplace.principal_id
    notifications = module.func_notifications.principal_id
    analytics     = module.func_analytics.principal_id
  }

  servicebus_data_receiver_principal_ids = {
    identity      = module.func_identity.principal_id
    licensing     = module.func_licensing.principal_id
    billing       = module.func_billing.principal_id
    marketplace   = module.func_marketplace.principal_id
    notifications = module.func_notifications.principal_id
    analytics     = module.func_analytics.principal_id
  }

  # All Function Apps need App Configuration access
  appconfig_data_reader_principal_ids = {
    identity      = module.func_identity.principal_id
    licensing     = module.func_licensing.principal_id
    billing       = module.func_billing.principal_id
    marketplace   = module.func_marketplace.principal_id
    notifications = module.func_notifications.principal_id
    analytics     = module.func_analytics.principal_id
  }

  # Function Apps that need Redis access (Identity for sessions, Licensing for license cache)
  redis_contributor_principal_ids = {
    identity  = module.func_identity.principal_id
    licensing = module.func_licensing.principal_id
  }
}

