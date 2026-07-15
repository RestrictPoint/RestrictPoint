resource "azurerm_redis_cache" "main" {
  name                = var.redis_name
  location            = var.location
  resource_group_name = var.resource_group_name
  capacity            = var.capacity
  family              = var.family
  sku_name            = var.sku_name

  # Basic/Standard: no redis config; Premium: enable persistence
  redis_configuration {
    maxmemory_policy               = "volatile-lru" # Evict keys with TTL when memory limit reached
    
    # Premium-only settings (ignored in Basic/Standard)
    aof_backup_enabled             = var.sku_name == "Premium" ? var.enable_aof_backup : null
    aof_storage_connection_string_0 = var.sku_name == "Premium" && var.enable_aof_backup ? var.aof_storage_connection_string : null
    rdb_backup_enabled             = var.sku_name == "Premium" ? var.enable_rdb_backup : null
    rdb_backup_frequency           = var.sku_name == "Premium" && var.enable_rdb_backup ? var.rdb_backup_frequency : null
    rdb_storage_connection_string  = var.sku_name == "Premium" && var.enable_rdb_backup ? var.rdb_storage_connection_string : null
  }

  # TLS 1.2 minimum
  minimum_tls_version = "1.2"

  # Premium: enable zones; Basic/Standard: not supported
  zones = var.sku_name == "Premium" && var.zones != null ? var.zones : null

  # Public network access: enabled in dev (with firewall), disabled in prod (private endpoint)
  public_network_access_enabled = var.public_network_access_enabled

  tags = var.tags
}

# Firewall rules for dev (public endpoint with IP restrictions)
# Premium: use private endpoint instead (not configured in this module)
resource "azurerm_redis_firewall_rule" "rules" {
  for_each = { for rule in var.firewall_rules : rule.name => rule }

  name                = each.value.name
  redis_cache_name    = azurerm_redis_cache.main.name
  resource_group_name = var.resource_group_name
  start_ip            = each.value.start_ip
  end_ip              = each.value.end_ip
}

