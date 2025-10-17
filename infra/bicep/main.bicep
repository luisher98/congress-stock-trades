// Main Bicep template for Congress Stock Trading Tracker
// Deploys all Azure resources needed for the application

targetScope = 'subscription'

@description('Resource group name')
param resourceGroupName string = 'congress-stock-trades-rg'

@description('Azure region for resources')
param location string = 'centralus'

@description('Environment name (dev, staging, prod)')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environment string = 'dev'

// Create resource group
resource rg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: resourceGroupName
  location: location
  tags: {
    Environment: environment
    Application: 'CongressStockTrades'
    ManagedBy: 'Bicep'
  }
}

// Deploy resources
module resources 'resources.bicep' = {
  name: 'resources-deployment'
  scope: rg
  params: {
    location: location
    environment: environment
  }
}

// Outputs
output resourceGroupName string = rg.name
output functionAppName string = resources.outputs.functionAppName
output cosmosDbEndpoint string = resources.outputs.cosmosDbEndpoint
output signalREndpoint string = resources.outputs.signalREndpoint
output storageAccountName string = resources.outputs.storageAccountName
