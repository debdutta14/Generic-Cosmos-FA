data "azurerm_resource_group" "rg-cosmosdb-fa" {
  name     = "rg-pl-a-001"
}

data "azurerm_cosmosdb_account" "cdb-cosmosdb-fa" {
  name                = "cdb-pl-a-001"
  resource_group_name = data.azurerm_resource_group.rg-cosmosdb-fa.name
}

data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "kv-main-dev-deb-001"{
    name = "kv-main-dev-deb-001"
    location = data.azurerm_resource_group.rg-cosmosdb-fa.location
    resource_group_name = data.azurerm_resource_group.rg-cosmosdb-fa.name
    tenant_id = data.azurerm_client_config.current.tenant_id
    sku_name = "standard"
    tags = {
        "purpose" = "development"
    }
}

resource "azurerm_key_vault_access_policy" "kv-access-policy-user" {
  key_vault_id = azurerm_key_vault.kv-main-dev-deb-001.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = data.azurerm_client_config.current.object_id

  secret_permissions = [
    "Get",
    "List",
    "Set",
    "Delete",
    "Purge",
    "Recover"
  ]
}

resource "azurerm_key_vault_access_policy" "kv-access-policy-funcapp" {
  key_vault_id = azurerm_key_vault.kv-main-dev-deb-001.id
  tenant_id    = azurerm_linux_function_app.funcapp-cosmosdb-fa.identity[0].tenant_id
  object_id    = azurerm_linux_function_app.funcapp-cosmosdb-fa.identity[0].principal_id

  secret_permissions = [
    "Get",
    "List"
  ]
}


resource "azurerm_key_vault_secret" "kv-secret-cosmosdb-connection-string" {
  name         = "CosmosDBConnectionString"
  value        = data.azurerm_cosmosdb_account.cdb-cosmosdb-fa.primary_sql_connection_string
  key_vault_id = azurerm_key_vault.kv-main-dev-deb-001.id

  depends_on = [
    azurerm_key_vault_access_policy.kv-access-policy-user
  ]
}



resource "azurerm_storage_account" "strCosmosdb-fa" {
  name                     = "strcosmosdbfa001"
  resource_group_name      = data.azurerm_resource_group.rg-cosmosdb-fa.name
  location                 = data.azurerm_resource_group.rg-cosmosdb-fa.location
  account_tier             = "Standard"
  account_replication_type = "LRS"

  tags = {
    "purpose" = "development"
  }
}

resource "azurerm_service_plan" "serviceplan-cosmosdb-fa" {
  name                = "asp-cosmosdb-fa"
  resource_group_name = data.azurerm_resource_group.rg-cosmosdb-fa.name
  location            = data.azurerm_resource_group.rg-cosmosdb-fa.location
  os_type             = "Linux"
  sku_name            = "Y1"
}



resource "azurerm_linux_function_app" "funcapp-cosmosdb-fa" {
  name                = "fa-generic-cosmosdb-001"
  resource_group_name = data.azurerm_resource_group.rg-cosmosdb-fa.name
  location            = data.azurerm_resource_group.rg-cosmosdb-fa.location

  storage_account_name       = azurerm_storage_account.strCosmosdb-fa.name
  storage_account_access_key = azurerm_storage_account.strCosmosdb-fa.primary_access_key
  service_plan_id            = azurerm_service_plan.serviceplan-cosmosdb-fa.id

  identity {
    type = "SystemAssigned"
  }

  site_config {
    application_stack {
      dotnet_version = "8.0"
      use_dotnet_isolated_runtime = true
    }
  }
  app_settings = {
    //"CosmosDBConnectionString" = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.kv-secret-cosmosdb-connection-string.id})"
    "CosmosDBConnectionString" = "@Microsoft.KeyVault(VaultName=${azurerm_key_vault.kv-main-dev-deb-001.name};SecretName=${azurerm_key_vault_secret.kv-secret-cosmosdb-connection-string.name})"
    "CosmosDatabaseName": "price-product-cis"
    "CosmosContainerName": "price-product-cis-container"
  }
}