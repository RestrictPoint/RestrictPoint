resource "azurerm_mssql_server" "main" {
  name                = var.server_name
  location            = var.location
  resource_group_name = var.resource_group_name
  version             = "12.0"
  minimum_tls_version = "1.2"

  # Entra ID authentication only (no SQL auth)
  azuread_administrator {
    login_username              = var.aad_admin_login
    object_id                   = var.aad_admin_object_id
    azuread_authentication_only = var.azuread_only_authentication
  }

  # Public network access allowed in dev (with firewall rules), disabled in prod (private endpoint)
  public_network_access_enabled = var.public_network_access_enabled

  tags = var.tags
}

# Main database (serverless for dev, provisioned for prod)
resource "azurerm_mssql_database" "main" {
  name           = var.database_name
  server_id      = azurerm_mssql_server.main.id
  collation      = "SQL_Latin1_General_CP1_CI_AS"
  max_size_gb    = var.max_size_gb
  sku_name       = var.sku_name
  zone_redundant = var.zone_redundant

  # Serverless-specific settings
  auto_pause_delay_in_minutes = var.auto_pause_delay_in_minutes
  min_capacity                = var.min_capacity

  # Transparent Data Encryption enabled by default
  transparent_data_encryption_enabled = true

  # Long-term retention
  long_term_retention_policy {
    weekly_retention  = var.long_term_retention_weekly
    monthly_retention = var.long_term_retention_monthly
    yearly_retention  = var.long_term_retention_yearly
    week_of_year      = var.long_term_retention_week_of_year
  }

  # Short-term backup retention
  short_term_retention_policy {
    retention_days = var.backup_retention_days
  }

  tags = var.tags
}

# Firewall rules for dev (allow Azure services + specific IPs)
resource "azurerm_mssql_firewall_rule" "azure_services" {
  count = var.public_network_access_enabled ? 1 : 0

  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0" # Special value for Azure services
}

resource "azurerm_mssql_firewall_rule" "additional" {
  for_each = { for rule in var.firewall_rules : rule.name => rule }

  name             = each.value.name
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = each.value.start_ip
  end_ip_address   = each.value.end_ip
}
