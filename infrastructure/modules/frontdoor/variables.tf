variable "profile_name" {
  description = "Front Door profile name (must be globally unique)."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group name."
  type        = string
}

variable "sku_name" {
  description = "Front Door SKU: Standard_AzureFrontDoor (dev) or Premium_AzureFrontDoor (prod, managed WAF rules)."
  type        = string
  default     = "Standard_AzureFrontDoor"

  validation {
    condition     = contains(["Standard_AzureFrontDoor", "Premium_AzureFrontDoor"], var.sku_name)
    error_message = "SKU must be Standard_AzureFrontDoor or Premium_AzureFrontDoor."
  }
}

variable "enable_waf" {
  description = "Enable Web Application Firewall."
  type        = bool
  default     = true
}

variable "waf_mode" {
  description = "WAF mode: Prevention (block) or Detection (log only)."
  type        = string
  default     = "Prevention"

  validation {
    condition     = contains(["Prevention", "Detection"], var.waf_mode)
    error_message = "WAF mode must be Prevention or Detection."
  }
}

variable "waf_redirect_url" {
  description = "URL to redirect blocked requests (optional)."
  type        = string
  default     = null
}

variable "tags" {
  description = "Resource tags."
  type        = map(string)
  default     = {}
}
