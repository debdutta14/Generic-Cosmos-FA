terraform{
    backend "azurerm" {
    resource_group_name  = "rg-pl-a-001"
    storage_account_name = "ailearningstorageacnt"
    container_name       = "ailearning"
    key                  = "cosmosdb-fa-development.terraform.tfstate"
  }
}