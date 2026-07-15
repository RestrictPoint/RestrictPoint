locals {
  name_prefix = "restrictpoint-${var.environment}"

  tags = {
    project     = "RestrictPoint"
    environment = var.environment
    managed_by  = "terraform"
  }
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${local.name_prefix}"
  location = var.location
  tags     = local.tags
}
