output "redis_id" {
  description = "Redis cache resource ID."
  value       = azurerm_redis_cache.main.id
}

output "redis_name" {
  description = "Redis cache name."
  value       = azurerm_redis_cache.main.name
}

output "hostname" {
  description = "Redis hostname."
  value       = azurerm_redis_cache.main.hostname
}

output "port" {
  description = "Redis port."
  value       = azurerm_redis_cache.main.port
}

output "ssl_port" {
  description = "Redis SSL port."
  value       = azurerm_redis_cache.main.ssl_port
}

output "primary_access_key" {
  description = "Primary access key (use Managed Identity instead when possible)."
  value       = azurerm_redis_cache.main.primary_access_key
  sensitive   = true
}

output "primary_connection_string" {
  description = "Primary connection string."
  value       = azurerm_redis_cache.main.primary_connection_string
  sensitive   = true
}
