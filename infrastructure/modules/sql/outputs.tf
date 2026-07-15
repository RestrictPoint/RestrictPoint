output "server_id" {
  description = "SQL Server resource ID."
  value       = azurerm_mssql_server.main.id
}

output "server_name" {
  description = "SQL Server name."
  value       = azurerm_mssql_server.main.name
}

output "server_fqdn" {
  description = "SQL Server fully qualified domain name."
  value       = azurerm_mssql_server.main.fully_qualified_domain_name
}

output "database_id" {
  description = "SQL Database resource ID."
  value       = azurerm_mssql_database.main.id
}

output "database_name" {
  description = "SQL Database name."
  value       = azurerm_mssql_database.main.name
}

output "connection_string" {
  description = "ADO.NET connection string (use Managed Identity for authentication)."
  value       = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Initial Catalog=${azurerm_mssql_database.main.name};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Default;"
  sensitive   = true
}
