variable "apim_name" {
  description = "API Management service name (must be globally unique)."
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

variable "publisher_name" {
  description = "Publisher name (organization)."
  type        = string
  default     = "RestrictPoint"
}

variable "publisher_email" {
  description = "Publisher email address."
  type        = string
}

variable "sku_name" {
  description = "APIM SKU. Dev: Consumption_0, Prod: Standard_1 or Premium_1."
  type        = string
  default     = "Consumption_0"

  validation {
    condition     = can(regex("^(Consumption_0|Developer_1|Basic_[12]|Standard_[1-4]|Premium_[1-8])$", var.sku_name))
    error_message = "SKU must be a valid APIM SKU (e.g., Consumption_0, Standard_1, Premium_1)."
  }
}

variable "named_values" {
  description = "Named values (policy variables) for APIM."
  type = map(object({
    value  = string
    secret = bool
  }))
  default = {}
}

variable "tags" {
  description = "Resource tags."
  type        = map(string)
  default     = {}
}
