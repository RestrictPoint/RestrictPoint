terraform {
  required_version = ">= 1.9.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.20"
    }
  }

  backend "azurerm" {
    resource_group_name  = "RestrictPoint-Shared"
    storage_account_name = "rpdevtfstate5507"
    container_name       = "tfstate"
    key                  = "dev.tfstate"
    use_oidc             = true
  }
}

provider "azurerm" {
  features {}
  use_oidc = true
}
