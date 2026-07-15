output "namespace_id" {
  description = "Service Bus namespace ID."
  value       = azurerm_servicebus_namespace.main.id
}

output "namespace_name" {
  description = "Service Bus namespace name."
  value       = azurerm_servicebus_namespace.main.name
}

output "endpoint" {
  description = "Service Bus namespace endpoint."
  value       = azurerm_servicebus_namespace.main.endpoint
}

output "topic_ids" {
  description = "Map of topic names to their resource IDs."
  value       = { for k, v in azurerm_servicebus_topic.topics : k => v.id }
}

output "queue_ids" {
  description = "Map of queue names to their resource IDs."
  value       = { for k, v in azurerm_servicebus_queue.queues : k => v.id }
}
