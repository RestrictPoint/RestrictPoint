# Key Vault role assignments
resource "azurerm_role_assignment" "key_vault_secrets_user" {
  for_each = var.key_vault_secrets_user_principal_ids

  scope                = var.key_vault_id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = each.value
}

resource "azurerm_role_assignment" "key_vault_crypto_user" {
  for_each = var.key_vault_crypto_user_principal_ids

  scope                = var.key_vault_id
  role_definition_name = "Key Vault Crypto User"
  principal_id         = each.value
}

# SQL Database role assignments (requires SQL Server scope)
resource "azurerm_role_assignment" "sql_db_contributor" {
  for_each = var.sql_db_contributor_principal_ids

  scope                = var.sql_server_id
  role_definition_name = "SQL DB Contributor"
  principal_id         = each.value
}

# Service Bus role assignments
resource "azurerm_role_assignment" "servicebus_data_sender" {
  for_each = var.servicebus_data_sender_principal_ids

  scope                = var.servicebus_namespace_id
  role_definition_name = "Azure Service Bus Data Sender"
  principal_id         = each.value
}

resource "azurerm_role_assignment" "servicebus_data_receiver" {
  for_each = var.servicebus_data_receiver_principal_ids

  scope                = var.servicebus_namespace_id
  role_definition_name = "Azure Service Bus Data Receiver"
  principal_id         = each.value
}

# App Configuration role assignments
resource "azurerm_role_assignment" "appconfig_data_reader" {
  for_each = var.appconfig_data_reader_principal_ids

  scope                = var.appconfig_id
  role_definition_name = "App Configuration Data Reader"
  principal_id         = each.value
}

# Redis Cache data-plane access via access policy assignments.
# ARM RBAC roles (e.g. Redis Cache Contributor) do NOT grant data access;
# Entra token authentication requires an access policy assignment.
resource "azurerm_redis_cache_access_policy_assignment" "data_contributor" {
  for_each = var.redis_contributor_principal_ids

  name               = "data-contributor-${each.key}"
  redis_cache_id     = var.redis_cache_id
  access_policy_name = "Data Contributor"
  object_id          = each.value
  object_id_alias    = "func-${each.key}"
}
