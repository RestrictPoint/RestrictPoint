# Application Insights for monitoring
resource "azurerm_application_insights" "main" {
  name                = var.app_insights_name
  location            = var.location
  resource_group_name = var.resource_group_name
  workspace_id        = var.log_analytics_workspace_id
  application_type    = "web"

  tags = var.tags
}

# Storage account for Function App runtime
resource "azurerm_storage_account" "main" {
  name                     = var.storage_account_name
  resource_group_name      = var.resource_group_name
  location                 = var.location
  account_tier             = "Standard"
  account_replication_type = "LRS" # Dev: LRS, Prod: GRS
  min_tls_version          = "TLS1_2"

  # Blob properties
  blob_properties {
    delete_retention_policy {
      days = 7
    }
  }

  tags = var.tags
}

# Function App Service Plan
resource "azurerm_service_plan" "main" {
  name                = var.service_plan_name
  location            = var.location
  resource_group_name = var.resource_group_name
  os_type             = "Linux"

  # Consumption (dev) uses sku_name = "Y1", Elastic Premium uses "EP1"/"EP2"/"EP3"
  sku_name = var.sku_name

  # Zone redundancy (Premium only)
  zone_balancing_enabled = var.sku_name != "Y1" && var.zone_redundant ? true : null

  tags = var.tags
}

# Function App
resource "azurerm_linux_function_app" "main" {
  name                       = var.function_app_name
  location                   = var.location
  resource_group_name        = var.resource_group_name
  service_plan_id            = azurerm_service_plan.main.id
  storage_account_name       = azurerm_storage_account.main.name
  storage_account_access_key = azurerm_storage_account.main.primary_access_key

  # System-assigned Managed Identity
  identity {
    type = "SystemAssigned"
  }

  # Application settings
  app_settings = merge({
    "APPLICATIONINSIGHTS_CONNECTION_STRING"  = azurerm_application_insights.main.connection_string
    "APPINSIGHTS_INSTRUMENTATIONKEY"         = azurerm_application_insights.main.instrumentation_key
    "FUNCTIONS_WORKER_RUNTIME"               = "dotnet-isolated"
    "WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED" = "1"
  }, var.app_settings)

  site_config {
    application_insights_connection_string = azurerm_application_insights.main.connection_string
    application_insights_key               = azurerm_application_insights.main.instrumentation_key

    # Always use 64-bit process
    use_32_bit_worker = false

    # HTTP/2 enabled
    http2_enabled = true

    # Minimum TLS version
    minimum_tls_version = "1.2"

    # Application stack
    application_stack {
      dotnet_version              = "8.0"
      use_dotnet_isolated_runtime = true
    }

    # CORS (if needed)
    dynamic "cors" {
      for_each = length(var.cors_allowed_origins) > 0 ? [1] : []
      content {
        allowed_origins = var.cors_allowed_origins
      }
    }
  }

  # HTTPS only
  https_only = true

  tags = var.tags
}

# Diagnostic settings to Log Analytics
resource "azurerm_monitor_diagnostic_setting" "function_app" {
  name                       = "${var.function_app_name}-diagnostics"
  target_resource_id         = azurerm_linux_function_app.main.id
  log_analytics_workspace_id = var.log_analytics_workspace_id

  enabled_log {
    category = "FunctionAppLogs"
  }

  enabled_metric {
    category = "AllMetrics"
  }
}

resource "azurerm_monitor_diagnostic_setting" "app_insights" {
  name                       = "${var.app_insights_name}-diagnostics"
  target_resource_id         = azurerm_application_insights.main.id
  log_analytics_workspace_id = var.log_analytics_workspace_id

  enabled_log {
    category = "AppTraces"
  }

  enabled_log {
    category = "AppRequests"
  }

  enabled_log {
    category = "AppExceptions"
  }

  enabled_log {
    category = "AppDependencies"
  }

  enabled_metric {
    category = "AllMetrics"
  }
}
