// Azure resources for Congress Stock Trading Tracker
@description('Azure region for resources')
param location string

@description('Environment name')
param environment string

@description('Telegram Bot Token')
@secure()
param telegramBotToken string

@description('Telegram Chat ID')
param telegramChatId string

@description('Congress.gov API Key')
@secure()
param congressApiKey string

@description('Alpha Vantage API Key')
@secure()
param alphaVantageApiKey string

@description('Document Intelligence Model ID')
param documentIntelligenceModelId string = 'ptr-extractor-v1'

var uniqueSuffix = uniqueString(resourceGroup().id)
var functionAppName = 'func-cst-${environment}-${take(uniqueSuffix, 8)}'
// Storage account name must be 3-24 chars, lowercase and numbers only
var storageAccountName = toLower('stcst${environment}${take(uniqueSuffix, 10)}')
var cosmosDbAccountName = 'cosmos-cst-${environment}-${take(uniqueSuffix, 8)}'
var signalRName = 'signalr-cst-${environment}-${take(uniqueSuffix, 8)}'
var docIntelName = 'docintel-cst-${environment}-${take(uniqueSuffix, 6)}'
var appInsightsName = 'ai-cst-${environment}-${take(uniqueSuffix, 8)}'
var logWorkspaceName = 'logs-cst-${environment}-${take(uniqueSuffix, 8)}'

// Storage Account for Azure Functions
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
  tags: {
    Environment: environment
  }
}

// Queue for filing processing
resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
}

resource filingsQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-01-01' = {
  parent: queueService
  name: 'filings-to-process'
}

// Cosmos DB Account (Serverless)
resource cosmosDbAccount 'Microsoft.DocumentDB/databaseAccounts@2023-11-15' = {
  name: cosmosDbAccountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
      }
    ]
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
  }
  tags: {
    Environment: environment
  }
}

// Cosmos DB Database
resource cosmosDb 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-11-15' = {
  parent: cosmosDbAccount
  name: 'CongressTrades'
  properties: {
    resource: {
      id: 'CongressTrades'
    }
  }
}

// Cosmos DB Container: transactions
resource transactionsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-11-15' = {
  parent: cosmosDb
  name: 'transactions'
  properties: {
    resource: {
      id: 'transactions'
      partitionKey: {
        paths: [
          '/filingId'
        ]
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        includedPaths: [
          {
            path: '/*'
          }
        ]
      }
    }
  }
}

// SignalR Service (Free F1 tier)
resource signalR 'Microsoft.SignalRService/signalR@2023-02-01' = {
  name: signalRName
  location: location
  sku: {
    name: 'Free_F1'
    tier: 'Free'
    capacity: 1
  }
  kind: 'SignalR'
  properties: {
    features: [
      {
        flag: 'ServiceMode'
        value: 'Serverless'
      }
    ]
    cors: {
      allowedOrigins: [
        '*'
      ]
    }
  }
  tags: {
    Environment: environment
  }
}

// Document Intelligence (S0 tier)
resource documentIntelligence 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: docIntelName
  location: location
  sku: {
    name: 'S0'
  }
  kind: 'FormRecognizer'
  properties: {
    customSubDomainName: docIntelName
    publicNetworkAccess: 'Enabled'
  }
  tags: {
    Environment: environment
  }
}

// Log Analytics Workspace
resource logWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logWorkspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
  tags: {
    Environment: environment
  }
}

// Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logWorkspace.id
  }
  tags: {
    Environment: environment
  }
}

// App Service Plan (Consumption Y1)
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: 'plan-cst-${environment}'
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {
    reserved: false
  }
  tags: {
    Environment: environment
  }
}

// Function App
resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppName)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'CosmosDb__Endpoint'
          value: cosmosDbAccount.properties.documentEndpoint
        }
        {
          name: 'CosmosDb__Key'
          value: cosmosDbAccount.listKeys().primaryMasterKey
        }
        {
          name: 'CosmosDb__DatabaseName'
          value: 'CongressTrades'
        }
        {
          name: 'DocumentIntelligence__Endpoint'
          value: documentIntelligence.properties.endpoint
        }
        {
          name: 'DocumentIntelligence__Key'
          value: documentIntelligence.listKeys().key1
        }
        {
          name: 'SignalR__ConnectionString'
          value: signalR.listKeys().primaryConnectionString
        }
        {
          name: 'HouseGov__BaseUrl'
          value: 'https://disclosures-clerk.house.gov'
        }
        {
          name: 'Telegram__BotToken'
          value: telegramBotToken
        }
        {
          name: 'Telegram__ChatId'
          value: telegramChatId
        }
        {
          name: 'CongressApi__ApiKey'
          value: congressApiKey
        }
        {
          name: 'AlphaVantage__ApiKey'
          value: alphaVantageApiKey
        }
        {
          name: 'DocumentIntelligence__ModelId'
          value: documentIntelligenceModelId
        }
      ]
      netFrameworkVersion: 'v8.0'
      cors: {
        allowedOrigins: [
          '*'
        ]
      }
    }
    httpsOnly: true
  }
  tags: {
    Environment: environment
  }
}

// Outputs
output functionAppName string = functionApp.name
output cosmosDbEndpoint string = cosmosDbAccount.properties.documentEndpoint
output signalREndpoint string = signalR.properties.hostName
output storageAccountName string = storageAccount.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output documentIntelligenceEndpoint string = documentIntelligence.properties.endpoint
