resource "azurerm_servicebus_namespace" "main" {
  name                = var.namespace_name
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = var.sku # Standard (dev) or Premium (prod)

  # Premium-only features (ignored in Standard)
  capacity                     = var.sku == "Premium" ? var.capacity : null
  premium_messaging_partitions = var.sku == "Premium" ? var.premium_messaging_partitions : null

  local_auth_enabled = false # Use Managed Identity only

  tags = var.tags
}

# Topics for domain events (publish/subscribe pattern)
resource "azurerm_servicebus_topic" "topics" {
  for_each = toset(var.topics)

  name         = each.value
  namespace_id = azurerm_servicebus_namespace.main.id

  max_size_in_megabytes = var.sku == "Standard" ? 1024 : 5120

  # Message TTL: 14 days default
  default_message_ttl = "P14D"
}

# Queues for work distribution (competing consumers pattern)
resource "azurerm_servicebus_queue" "queues" {
  for_each = toset(var.queues)

  name         = each.value
  namespace_id = azurerm_servicebus_namespace.main.id

  max_size_in_megabytes = var.sku == "Standard" ? 1024 : 5120

  # Dead letter queue enabled by default
  dead_lettering_on_message_expiration = true
  max_delivery_count                   = 10

  # Message TTL: 14 days
  default_message_ttl = "P14D"

  # Lock duration: 5 minutes for processing
  lock_duration = "PT5M"
}
