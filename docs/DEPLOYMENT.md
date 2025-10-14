# Congress Stock Trading Tracker - Deployment Guide

## Table of Contents
1. [Deployment Overview](#deployment-overview)
2. [Infrastructure as Code (Bicep)](#infrastructure-as-code-bicep)
3. [CI/CD Pipeline](#cicd-pipeline)
4. [Manual Deployment](#manual-deployment)
5. [Environment Management](#environment-management)
6. [Monitoring & Alerts](#monitoring--alerts)
7. [Troubleshooting](#troubleshooting)

---

## 1. Deployment Overview

### 1.1 Deployment Strategy
**Blue-Green Deployment with Slot Swapping**

- Deploy to **staging slot** first
- Run automated smoke tests
- Swap staging → production (zero downtime)
- Rollback by swapping back if issues detected

### 1.2 Deployment Methods

| Method | Use Case | Speed | Difficulty |
|--------|----------|-------|------------|
| **GitHub Actions (Recommended)** | Automated on git push | Fast (3-5 min) | Easy |
| **Azure Developer CLI (azd)** | Local development | Fast (5-7 min) | Easy |
| **Azure CLI + Bicep** | Manual control | Medium (10-15 min) | Medium |
| **Azure Portal** | Emergency fixes | Slow (20+ min) | Hard |

---

## 2. Infrastructure as Code (Bicep)

### 2.1 Main Bicep Template

#### File: `infra/main.bicep`
```bicep
targetScope = 'resourceGroup'

@description('Application name (used for resource naming)')
param appName string = 'congress-stock-trades'

@description('Azure region for resources')
param location string = resourceGroup().location

@description('Environment name (dev, staging, prod)')
@allowed(['dev', 'staging', 'prod'])
param environment string = 'prod'

@description('Document Intelligence API key')
@secure()
param documentIntelligenceKey string

@description('Tags for all resources')
param tags object = {
  Application: 'CongressStockTrades'
  Environment: environment
  ManagedBy: 'Bicep'
}

// Variables
var resourceNamePrefix = '${appName}-${environment}'
var storageAccountName = replace('${resourceNamePrefix}storage', '-', '')
var functionAppName = '${resourceNamePrefix}-func'
var cosmosDbAccountName = '${resourceNamePrefix}-cosmos'
var docIntelName = '${resourceNamePrefix}-docintel'
var signalrName = '${resourceNamePrefix}-signalr'
var appInsightsName = '${resourceNamePrefix}-insights'
var logAnalyticsName = '${resourceNamePrefix}-logs'
var keyVaultName = replace('${resourceNamePrefix}-kv', '-', '')

//==============================================================================
// Storage Account (for queues + function state)
//==============================================================================
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }

  resource queueService 'queueServices' = {
    name: 'default'

    resource filingsQueue 'queues' = {
      name: 'filings-to-process'
    }
  }
}

//==============================================================================
// Cosmos DB
//==============================================================================
resource cosmosDbAccount 'Microsoft.DocumentDB/databaseAccounts@2023-11-15' = {
  name: cosmosDbAccountName
  location: location
  tags: tags
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
    backupPolicy: {
      type: 'Continuous'
    }
  }

  resource database 'sqlDatabases' = {
    name: 'CongressTrades'
    properties: {
      resource: {
        id: 'CongressTrades'
      }
    }

    resource transactionsContainer 'containers' = {
      name: 'transactions'
      properties: {
        resource: {
          id: 'transactions'
          partitionKey: {
            paths: ['/filingId']
            kind: 'Hash'
          }
          indexingPolicy: {
            automatic: true
            indexingMode: 'consistent'
          }
        }
      }
    }

    resource processedFilingsContainer 'containers' = {
      name: 'processed-filings'
      properties: {
        resource: {
          id: 'processed-filings'
          partitionKey: {
            paths: ['/id']
            kind: 'Hash'
          }
        }
      }
    }
  }
}

//==============================================================================
// Document Intelligence
//==============================================================================
resource documentIntelligence 'Microsoft.CognitiveServices/accounts@2023-10-01-preview' = {
  name: docIntelName
  location: location
  tags: tags
  kind: 'FormRecognizer'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: docIntelName
    publicNetworkAccess: 'Enabled'
  }
}

//==============================================================================
// SignalR Service
//==============================================================================
resource signalr 'Microsoft.SignalRService/signalR@2023-02-01' = {
  name: signalrName
  location: location
  tags: tags
  sku: {
    name: environment == 'prod' ? 'Standard_S1' : 'Free_F1'
    capacity: 1
  }
  properties: {
    features: [
      {
        flag: 'ServiceMode'
        value: 'Serverless'
      }
    ]
    cors: {
      allowedOrigins: ['*'] // Configure specific origins in production
    }
  }
}

//==============================================================================
// Log Analytics Workspace
//==============================================================================
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

//==============================================================================
// Application Insights
//==============================================================================
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    IngestionMode: 'LogAnalytics'
  }
}

//==============================================================================
// Key Vault
//==============================================================================
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
  }

  resource docIntelKeySecret 'secrets' = {
    name: 'DocumentIntelligenceKey'
    properties: {
      value: documentIntelligenceKey
    }
  }

  resource cosmosConnectionString 'secrets' = {
    name: 'CosmosDbConnectionString'
    properties: {
      value: cosmosDbAccount.listConnectionStrings().connectionStrings[0].connectionString
    }
  }

  resource signalrConnectionString 'secrets' = {
    name: 'SignalRConnectionString'
    properties: {
      value: signalr.listKeys().primaryConnectionString
    }
  }
}

//==============================================================================
// App Service Plan (Consumption)
//==============================================================================
resource hostingPlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: '${resourceNamePrefix}-plan'
  location: location
  tags: tags
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {
    reserved: true // Linux
  }
}

//==============================================================================
// Function App
//==============================================================================
resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: functionAppName
  location: location
  tags: tags
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: hostingPlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${az.environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
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
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/DocumentIntelligenceKey/)'
        }
        {
          name: 'SignalR__ConnectionString'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/SignalRConnectionString/)'
        }
      ]
      cors: {
        allowedOrigins: ['*'] // Configure specific origins in production
        supportCredentials: false
      }
    }
  }

  // Staging slot for blue-green deployment
  resource stagingSlot 'slots' = if (environment == 'prod') {
    name: 'staging'
    location: location
    tags: tags
    kind: 'functionapp,linux'
    properties: {
      serverFarmId: hostingPlan.id
      siteConfig: {
        linuxFxVersion: 'DOTNET-ISOLATED|8.0'
        appSettings: [
          {
            name: 'AzureWebJobsStorage'
            value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${az.environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
          }
          {
            name: 'FUNCTIONS_EXTENSION_VERSION'
            value: '~4'
          }
          {
            name: 'FUNCTIONS_WORKER_RUNTIME'
            value: 'dotnet-isolated'
          }
        ]
      }
    }
  }
}

// Grant Function App access to Key Vault
resource keyVaultRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, functionApp.id, 'Key Vault Secrets User')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

//==============================================================================
// Outputs
//==============================================================================
output functionAppName string = functionApp.name
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output cosmosDbEndpoint string = cosmosDbAccount.properties.documentEndpoint
output documentIntelligenceEndpoint string = documentIntelligence.properties.endpoint
output storageAccountName string = storageAccount.name
output appInsightsInstrumentationKey string = appInsights.properties.InstrumentationKey
```

---

### 2.2 Parameters File

#### File: `infra/main.parameters.json`
```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "appName": {
      "value": "congress-stock-trades"
    },
    "environment": {
      "value": "prod"
    },
    "documentIntelligenceKey": {
      "reference": {
        "keyVault": {
          "id": "/subscriptions/{subscription-id}/resourceGroups/{rg-name}/providers/Microsoft.KeyVault/vaults/{kv-name}"
        },
        "secretName": "DocumentIntelligenceKey"
      }
    }
  }
}
```

---

## 3. CI/CD Pipeline

### 3.1 Infrastructure Pipeline

#### File: `.github/workflows/infrastructure.yml`
```yaml
name: Deploy Infrastructure

on:
  push:
    branches:
      - main
    paths:
      - 'infra/**'
      - '.github/workflows/infrastructure.yml'
  workflow_dispatch:
    inputs:
      environment:
        description: 'Environment to deploy'
        required: true
        default: 'prod'
        type: choice
        options:
          - dev
          - staging
          - prod

permissions:
  id-token: write
  contents: read

jobs:
  deploy-infrastructure:
    runs-on: ubuntu-latest
    environment: ${{ github.event.inputs.environment || 'prod' }}

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Azure Login (OIDC)
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Create Resource Group
        uses: azure/cli@v2
        with:
          inlineScript: |
            az group create \
              --name ${{ vars.AZURE_RESOURCE_GROUP }} \
              --location ${{ vars.AZURE_LOCATION }}

      - name: Deploy Bicep Template
        uses: azure/arm-deploy@v2
        with:
          scope: resourcegroup
          resourceGroupName: ${{ vars.AZURE_RESOURCE_GROUP }}
          template: ./infra/main.bicep
          parameters: >
            appName=${{ vars.APP_NAME }}
            environment=${{ github.event.inputs.environment || 'prod' }}
            documentIntelligenceKey=${{ secrets.DOCUMENT_INTELLIGENCE_KEY }}
          deploymentMode: Incremental
          failOnStdErr: false

      - name: Get Deployment Outputs
        id: outputs
        uses: azure/cli@v2
        with:
          inlineScript: |
            outputs=$(az deployment group show \
              --name main \
              --resource-group ${{ vars.AZURE_RESOURCE_GROUP }} \
              --query properties.outputs \
              --output json)
            echo "outputs=$outputs" >> $GITHUB_OUTPUT

      - name: Display Outputs
        run: |
          echo "Function App URL: ${{ fromJson(steps.outputs.outputs).functionAppUrl.value }}"
          echo "Cosmos DB Endpoint: ${{ fromJson(steps.outputs.outputs).cosmosDbEndpoint.value }}"
```

---

### 3.2 Functions Deployment Pipeline

#### File: `.github/workflows/functions.yml`
```yaml
name: Deploy Functions

on:
  push:
    branches:
      - main
    paths:
      - 'src/**'
      - '.github/workflows/functions.yml'
  workflow_dispatch:

permissions:
  id-token: write
  contents: read

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Cache NuGet packages
        uses: actions/cache@v3
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
          restore-path: ~/.nuget/packages

      - name: Restore dependencies
        run: dotnet restore src/CongressStockTrades.sln

      - name: Build
        run: dotnet build src/CongressStockTrades.sln --configuration Release --no-restore

      - name: Run tests
        run: dotnet test src/CongressStockTrades.Tests/ --no-build --verbosity normal --logger "trx"

      - name: Publish Functions
        run: |
          dotnet publish src/CongressStockTrades.Functions/CongressStockTrades.Functions.csproj \
            --configuration Release \
            --output ./publish

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: functions-package
          path: ./publish

  deploy-staging:
    needs: build
    runs-on: ubuntu-latest
    environment: staging
    steps:
      - name: Download artifact
        uses: actions/download-artifact@v4
        with:
          name: functions-package
          path: ./publish

      - name: Azure Login
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Deploy to staging slot
        uses: azure/functions-action@v1
        with:
          app-name: ${{ vars.FUNCTION_APP_NAME }}
          package: ./publish
          slot-name: staging

  smoke-test:
    needs: deploy-staging
    runs-on: ubuntu-latest
    steps:
      - name: Wait for deployment
        run: sleep 30

      - name: Health check
        run: |
          response=$(curl -s -o /dev/null -w "%{http_code}" \
            https://${{ vars.FUNCTION_APP_NAME }}-staging.azurewebsites.net/api/health)

          if [ $response -ne 200 ]; then
            echo "Health check failed with status $response"
            exit 1
          fi

          echo "Smoke test passed"

  swap-slots:
    needs: smoke-test
    runs-on: ubuntu-latest
    environment: production
    steps:
      - name: Azure Login
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Swap staging to production
        uses: azure/cli@v2
        with:
          inlineScript: |
            az functionapp deployment slot swap \
              --name ${{ vars.FUNCTION_APP_NAME }} \
              --resource-group ${{ vars.AZURE_RESOURCE_GROUP }} \
              --slot staging \
              --target-slot production

      - name: Create release annotation
        uses: azure/appinsights-release-annotation@v1
        with:
          instrumentationKey: ${{ secrets.APPINSIGHTS_INSTRUMENTATION_KEY }}
          releaseName: ${{ github.sha }}
          releaseProperties: |
            {
              "ReleaseDescription": "${{ github.event.head_commit.message }}",
              "TriggerBy": "${{ github.actor }}",
              "Branch": "${{ github.ref_name }}"
            }
```

---

### 3.3 PR Validation Pipeline

#### File: `.github/workflows/pr-validation.yml`
```yaml
name: PR Validation

on:
  pull_request:
    branches:
      - main
      - develop

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Cache NuGet packages
        uses: actions/cache@v3
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}

      - name: Restore dependencies
        run: dotnet restore src/CongressStockTrades.sln

      - name: Build
        run: dotnet build src/CongressStockTrades.sln --configuration Release --no-restore

      - name: Run tests
        run: dotnet test src/CongressStockTrades.Tests/ --no-build --logger "trx" --collect:"XPlat Code Coverage"

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: '**/*.trx'

      - name: Validate Bicep (if infrastructure changed)
        if: contains(github.event.pull_request.changed_files, 'infra/')
        run: |
          az bicep build --file infra/main.bicep
```

---

## 4. Manual Deployment

### 4.1 Using Azure Developer CLI (azd)

#### Step 1: Initialize Project
```bash
# Install azd (if not already installed)
brew install azure-dev  # macOS

# Login to Azure
azd auth login

# Initialize environment
azd init

# Select subscription and location
azd env new prod
azd env set AZURE_LOCATION eastus
```

#### Step 2: Deploy Everything
```bash
# Provision infrastructure + deploy code
azd up

# This will:
# 1. Create all Azure resources (Functions, Cosmos DB, etc.)
# 2. Build .NET code
# 3. Deploy to Azure Functions
# 4. Configure secrets and connections
```

#### Step 3: Verify Deployment
```bash
# Check status
azd monitor

# View logs
azd monitor --logs

# Get service endpoints
azd show
```

---

### 4.2 Using Azure CLI + Bicep

#### Step 1: Deploy Infrastructure
```bash
# Create resource group
az group create \
  --name congress-stock-trades-rg \
  --location eastus

# Deploy Bicep template
az deployment group create \
  --name congress-deployment \
  --resource-group congress-stock-trades-rg \
  --template-file infra/main.bicep \
  --parameters appName=congress-stock-trades \
               environment=prod \
               documentIntelligenceKey="your-key-here"

# Get outputs
az deployment group show \
  --name congress-deployment \
  --resource-group congress-stock-trades-rg \
  --query properties.outputs
```

#### Step 2: Build and Deploy Functions
```bash
# Build and publish
dotnet publish src/CongressStockTrades.Functions/CongressStockTrades.Functions.csproj \
  --configuration Release \
  --output ./publish

# Create ZIP package
cd publish
zip -r ../functions.zip .
cd ..

# Deploy to Azure Functions
az functionapp deployment source config-zip \
  --name congress-stock-trades-prod-func \
  --resource-group congress-stock-trades-rg \
  --src functions.zip
```

---

## 5. Environment Management

### 5.1 Multi-Environment Setup

| Environment | Purpose | Branch | Approval Required |
|-------------|---------|--------|-------------------|
| **Development** | Developer testing | `develop` | No |
| **Staging** | Pre-production testing | `main` (staging slot) | No |
| **Production** | Live system | `main` (production slot) | Yes |

### 5.2 Environment-Specific Configuration

#### Development
```bash
azd env new dev
azd env set AZURE_LOCATION eastus
azd env set ENVIRONMENT dev
azd up
```

#### Production
```bash
azd env new prod
azd env set AZURE_LOCATION eastus
azd env set ENVIRONMENT prod
azd up
```

---

## 6. Monitoring & Alerts

### 6.1 Application Insights Queries

#### Monitor Filing Processing
```kusto
traces
| where message contains "Processing filing"
| summarize count() by bin(timestamp, 1h)
| render timechart
```

#### Error Rate
```kusto
exceptions
| where timestamp > ago(24h)
| summarize ErrorCount=count() by cloud_RoleName, type
| order by ErrorCount desc
```

#### Performance Metrics
```kusto
requests
| where name == "GetLatestTransaction"
| summarize avg(duration), percentile(duration, 95) by bin(timestamp, 5m)
| render timechart
```

---

### 6.2 Alert Rules

#### File: `infra/alerts.bicep`
```bicep
resource highErrorRateAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'HighErrorRate'
  location: 'global'
  properties: {
    description: 'Alert when error rate exceeds 10%'
    severity: 2
    enabled: true
    scopes: [
      appInsights.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'ErrorRate'
          metricName: 'exceptions/count'
          operator: 'GreaterThan'
          threshold: 10
          timeAggregation: 'Count'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}
```

---

## 7. Troubleshooting

### 7.1 Common Deployment Issues

#### Issue: Bicep deployment fails
```bash
# Validate Bicep syntax
az bicep build --file infra/main.bicep

# Check for errors in deployment
az deployment group show \
  --name congress-deployment \
  --resource-group congress-stock-trades-rg \
  --query properties.error
```

#### Issue: Function app not starting
```bash
# View function logs
az functionapp log tail \
  --name congress-stock-trades-prod-func \
  --resource-group congress-stock-trades-rg

# Check configuration
az functionapp config appsettings list \
  --name congress-stock-trades-prod-func \
  --resource-group congress-stock-trades-rg
```

#### Issue: Queue messages not processing
```bash
# Check queue depth
az storage queue list \
  --account-name congressstorageprod \
  --auth-mode key

# View poison queue
az storage message peek \
  --queue-name filings-to-process-poison \
  --account-name congressstorageprod
```

---

### 7.2 Rollback Procedures

#### Rollback via Slot Swap
```bash
# Swap back to previous version
az functionapp deployment slot swap \
  --name congress-stock-trades-prod-func \
  --resource-group congress-stock-trades-rg \
  --slot production \
  --target-slot staging
```

#### Rollback via GitHub Actions
1. Navigate to Actions tab
2. Find last successful deployment
3. Click "Re-run jobs"

---

### 7.3 Emergency Fixes

#### Disable Timer Function (stop processing)
```bash
az functionapp config appsettings set \
  --name congress-stock-trades-prod-func \
  --resource-group congress-stock-trades-rg \
  --settings "AzureWebJobs.CheckNewFilingsTrigger.Disabled=true"
```

#### Clear Queue
```bash
az storage queue clear \
  --name filings-to-process \
  --account-name congressstorageprod \
  --auth-mode key
```

---

## 8. Cost Optimization

### 8.1 Monitor Costs
```bash
# View current costs
az consumption usage list \
  --start-date 2025-10-01 \
  --end-date 2025-10-31 \
  --query "[?contains(instanceName, 'congress')].{Name:instanceName, Cost:pretaxCost}" \
  --output table
```

### 8.2 Set Budget Alerts
```bash
az consumption budget create \
  --budget-name congress-monthly-budget \
  --amount 50 \
  --time-period-start 2025-10-01 \
  --time-period-end 2026-10-01 \
  --resource-group congress-stock-trades-rg
```

---

**Document Version**: 1.0
**Last Updated**: 2025-10-14
