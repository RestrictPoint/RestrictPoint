variable "namespace_name" {
  description = "Service Bus namespace name (must be globally unique)."
  type        = string
}

variable "location" {
  description = "Azure region."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group name."
  type        = string
}

variable "sku" {
  description = "Service Bus SKU: Standard (dev) or Premium (prod)."
  type        = string
  default     = "Standard"

  validation {
    condition     = contains(["Standard", "Premium"], var.sku)
    error_message = "SKU must be Standard or Premium."
  }
}

variable "capacity" {
  description = "Premium tier capacity (messaging units). Only applies to Premium SKU."
  type        = number
  default     = 1

  validation {
    condition     = var.capacity >= 1 && var.capacity <= 16
    error_message = "Capacity must be between 1 and 16 messaging units."
  }
}

variable "premium_messaging_partitions" {
  description = "Number of premium messaging partitions (1, 2, or 4). Only applies to Premium SKU."
  type        = number
  default     = 1

  validation {
    condition     = contains([1, 2, 4], var.premium_messaging_partitions)
    error_message = "Premium messaging partitions must be 1, 2, or 4."
  }
}

variable "zone_redundant" {
  description = "Enable zone redundancy (Premium SKU only)."
  type        = bool
  default     = false
}

variable "topics" {
  description = "List of Service Bus topics to create."
  type        = list(string)
  default = [
    "IdentityEvents",
    "OrganizationEvents",
    "ProjectEvents",
    "LicenseEvents",
    "BillingEvents",
    "MarketplaceEvents",
    "NotificationEvents",
    "AnalyticsEvents",
    "AuditEvents"
  ]
}

variable "queues" {
  description = "List of Service Bus queues to create."
  type        = list(string)
  default = [
    "Email",
    "Webhook",
    "InvoiceGeneration",
    "UsageAggregation",
    "Cleanup",
    "Retry",
    "DeadLetterProcessing"
  ]
}

variable "tags" {
  description = "Resource tags."
  type        = map(string)
  default     = {}
}
