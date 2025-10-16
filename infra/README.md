# Infrastructure Deployment

This directory contains Infrastructure as Code (IaC) templates for deploying the Congress Stock Trading Tracker to Azure.

## Prerequisites

- Azure Subscription
- Azure CLI installed ([Install guide](https://docs.microsoft.com/cli/azure/install-azure-cli))
- Contributor access to the subscription
- GitHub repository with required secrets configured

## Architecture

```
┌─────────────────────────────────────────────────────┐
│                Azure Subscription                    │
├─────────────────────────────────────────────────────┤
│                                                       │
│  Resource Group: congress-stock-trades-rg            │
│  ├── Function App (Consumption Y1)                   │
│  ├── Storage Account (Standard LRS)                  │
│  │   └── Queue: filings-to-process                   │
│  ├── Cosmos DB (Serverless)                          │
│  │   ├── Database: CongressTrades                    │
│  │   ├── Container: transactions                     │
│  │   └── Container: processed-filings                │
│  ├── SignalR Service (Free F1)                       │
│  ├── Document Intelligence (S0)                      │
│  ├── Application Insights                            │
│  └── Log Analytics Workspace                         │
│                                                       │
└─────────────────────────────────────────────────────┘
```

## Resources Created

| Resource | SKU/Tier | Monthly Cost (Estimate) | Purpose |
|----------|----------|-------------------------|---------|
| Function App | Consumption (Y1) | ~$10 | Serverless compute |
| Storage Account | Standard LRS | ~$1 | Function state & queue |
| Cosmos DB | Serverless | ~$25 | Document database |
| SignalR Service | Free F1 | $0 | Real-time notifications |
| Document Intelligence | S0 | ~$1.50 | PDF processing |
| Application Insights | Pay-as-you-go | ~$0 | Monitoring & logs |
| **Total** | | **~$37.50/month** | |

## GitHub Actions Secrets

Configure these secrets in your GitHub repository:

### Required Secrets

1. **AZURE_CREDENTIALS**
   ```bash
   az ad sp create-for-rbac \
     --name "github-actions-congress-trades" \
     --role contributor \
     --scopes /subscriptions/{subscription-id} \
     --sdk-auth
   ```
   Copy the entire JSON output to this secret.

2. **AZURE_SUBSCRIPTION_ID**
   ```bash
   az account show --query id -o tsv
   ```

## Deployment via GitHub Actions

### Automatic Deployment (Recommended)

Deployments trigger automatically on push to `main` branch:

```bash
git push origin main
```

The workflow will:
1. Build the .NET solution
2. Run tests
3. Deploy infrastructure (Bicep)
4. Deploy function code
5. Run health checks
6. Provide deployment summary

### Manual Deployment

Trigger deployment manually from GitHub Actions:

1. Go to **Actions** tab
2. Select **Deploy to Azure** workflow
3. Click **Run workflow**
4. Select environment (dev/staging/prod)
5. Click **Run workflow**

## Manual Deployment (Alternative)

If you need to deploy manually without GitHub Actions:

### 1. Login to Azure

```bash
az login
az account set --subscription "Your Subscription Name"
```

### 2. Deploy Infrastructure

```bash
az deployment sub create \
  --name congress-trades-$(date +%Y%m%d-%H%M%S) \
  --location eastus \
  --template-file infra/bicep/main.bicep \
  --parameters environment=dev location=eastus
```

### 3. Get Resource Names

```bash
# Get the deployment name from output above
DEPLOYMENT_NAME="congress-trades-20250116-123456"

# Extract outputs
RESOURCE_GROUP=$(az deployment sub show \
  --name $DEPLOYMENT_NAME \
  --query properties.outputs.resourceGroupName.value -o tsv)

FUNCTION_APP=$(az deployment sub show \
  --name $DEPLOYMENT_NAME \
  --query properties.outputs.functionAppName.value -o tsv)
```

### 4. Build and Deploy Function Code

```bash
# Build and publish
dotnet publish src/CongressStockTrades.Functions \
  --configuration Release \
  --output ./publish

# Create deployment package
cd publish
zip -r ../deploy.zip .
cd ..

# Deploy to Azure Functions
az functionapp deployment source config-zip \
  --resource-group $RESOURCE_GROUP \
  --name $FUNCTION_APP \
  --src deploy.zip
```

### 5. Verify Deployment

```bash
# Check function app status
az functionapp show \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP \
  --query state -o tsv

# Test health endpoint
curl https://$FUNCTION_APP.azurewebsites.net/api/health

# View logs
az webapp log tail \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP
```

## Environments

### Development (dev)
- Auto-deploys on push to `main`
- Uses Free/Low-cost tiers
- Relaxed monitoring

### Staging (staging)
- Manual deployment via GitHub Actions
- Production-like configuration
- Full monitoring enabled

### Production (prod)
- Manual deployment with approval
- Production tiers
- Full monitoring & alerting
- Budget alerts configured

## Configuration

All configuration is managed through Bicep templates. Key settings:

### Function App Settings

Set in `resources.bicep` under `functionApp.properties.siteConfig.appSettings`:

- `FUNCTIONS_WORKER_RUNTIME`: dotnet-isolated
- `FUNCTIONS_EXTENSION_VERSION`: ~4
- `CosmosDb__*`: Cosmos DB connection
- `DocumentIntelligence__*`: Document Intelligence API
- `SignalR__ConnectionString`: SignalR connection

### Cosmos DB

- **Mode**: Serverless (autoscaling, pay-per-request)
- **Consistency**: Session
- **Containers**:
  - `transactions`: Partition key `/filingId`
  - `processed-filings`: Partition key `/id`

### Storage Account

- **Queue**: `filings-to-process`
- **Retention**: 7 days
- **Max Dequeue Count**: 5

## Monitoring

### Application Insights

Access metrics and logs:

```bash
# Get connection string
az monitor app-insights component show \
  --app $FUNCTION_APP-ai \
  --resource-group $RESOURCE_GROUP \
  --query connectionString -o tsv
```

View in Azure Portal:
- Navigate to Application Insights
- View Live Metrics, Logs, Failures

### Alerts

Configure budget alerts:

```bash
az consumption budget create \
  --resource-group $RESOURCE_GROUP \
  --budget-name monthly-budget \
  --amount 50 \
  --time-grain Monthly \
  --time-period "$(date +%Y-%m-01)" \
  --notifications ...
```

## Troubleshooting

### Deployment Failures

```bash
# View deployment details
az deployment sub show --name $DEPLOYMENT_NAME

# View operations
az deployment sub operation list \
  --name $DEPLOYMENT_NAME \
  --query "[?properties.provisioningState=='Failed']"
```

### Function App Issues

```bash
# Restart function app
az functionapp restart \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP

# View logs in real-time
az webapp log tail \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP

# Check function status
az functionapp function show \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP \
  --function-name CheckNewFilings
```

### Cosmos DB Issues

```bash
# Check connection
az cosmosdb show \
  --name $COSMOS_DB_NAME \
  --resource-group $RESOURCE_GROUP

# View metrics
az monitor metrics list \
  --resource $COSMOS_DB_ID \
  --metric TotalRequests
```

## Cost Management

### Monitor Costs

```bash
# View current costs
az consumption usage list \
  --start-date $(date -d "30 days ago" +%Y-%m-%d) \
  --end-date $(date +%Y-%m-%d) \
  --query "[?contains(instanceName,'congress')]"
```

### Optimize Costs

1. **Cosmos DB**: Largest cost driver (~70%)
   - Use batch operations
   - Optimize queries
   - Consider provisioned throughput for predictable workload

2. **Function App**: Second largest (~25%)
   - Timer frequency (current: 5 min)
   - Reduce to 15 min to save ~60%

3. **Document Intelligence**: (~5%)
   - Already optimized with S0 tier

## Cleanup

To delete all resources:

```bash
az group delete \
  --name $RESOURCE_GROUP \
  --yes \
  --no-wait
```

## Support

For issues or questions:
- Review GitHub Actions logs
- Check Azure Portal diagnostics
- Review Application Insights traces

---

**Last Updated**: 2025-01-16
**Version**: 1.0
