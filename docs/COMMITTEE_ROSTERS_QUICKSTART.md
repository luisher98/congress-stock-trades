# Committee Roster Updater - Quick Start Guide

## Immediate Testing (No Wait!)

You don't need to wait for the weekly timer. Use the manual HTTP endpoint instead:

### 🚀 Quick Test (5 minutes)

1. **Start the function app locally:**
   ```bash
   cd src/CongressStockTrades.Functions
   func start
   ```

2. **Trigger the update immediately:**
   ```bash
   curl -X POST http://localhost:7071/api/committee-rosters/update
   ```

3. **Watch the logs** for:
   - PDF download
   - Cover date extraction
   - Parsing progress (committees, subcommittees, members, assignments)
   - Upsert operations

4. **Check the response** - you'll get JSON with:
   ```json
   {
     "status": "success",
     "sourceDate": "2025-09-16",
     "counts": {
       "committees": 20,
       "subcommittees": 85,
       "members": 435,
       "assignments": 520
     }
   }
   ```

### 🔄 Force Reprocess

If you've already run it once and want to test again:
```bash
curl -X POST "http://localhost:7071/api/committee-rosters/update?force=true"
```

This bypasses change detection and always reprocesses the PDF.

## What Gets Created

The function will:

1. **Download** the latest SCSOAL PDF from clerk.house.gov
2. **Extract** the cover date (e.g., "SEPTEMBER 16, 2025" → "2025-09-16")
3. **Parse** all committees, subcommittees, members, and assignments
4. **Upload** the PDF to blob storage: `committee-rosters/2025-09-16/scsoal.pdf`
5. **Upsert** to Cosmos DB containers:
   - `committees` - 20+ committees
   - `subcommittees` - 80+ subcommittees
   - `members` - 400+ unique members
   - `assignments` - 500+ member assignments with roles
   - `sources` - 1 source document for change detection

## Verify Results

### Check Cosmos DB

**Azure Portal:**
1. Navigate to your Cosmos DB account
2. Open Data Explorer
3. Check the new containers: `committees`, `subcommittees`, `members`, `assignments`, `sources`

**Azure CLI:**
```bash
# Query committees
az cosmosdb sql query \
  --account-name cosmos-cst-dev-xxx \
  --database-name CongressTrades \
  --container-name committees \
  --query-text "SELECT * FROM c"
```

### Check Blob Storage

**Azure Portal:**
1. Navigate to your Storage Account
2. Open Containers → `committee-rosters`
3. You should see folders by date with PDFs inside

**Azure CLI:**
```bash
az storage blob list \
  --account-name stcstdevxxx \
  --container-name committee-rosters \
  --output table
```

## Troubleshooting

### "Committee roster updater is disabled"

Set in `local.settings.json`:
```json
"CommitteeRosters__Enabled": "true"
```

### "Container 'committees' not found"

You need to create the Cosmos DB containers first:
```bash
# Option 1: Deploy infrastructure
cd infra/bicep
az deployment group create \
  --resource-group congress-stock-trades-rg \
  --template-file main.bicep

# Option 2: Create containers manually (for testing)
# See docs/COMMITTEE_ROSTERS.md "Production Deployment" section
```

### "Blob container not found"

The blob container is created automatically by BlobStorageService. If it fails:
```bash
az storage container create \
  --name committee-rosters \
  --account-name stcstdevxxx
```

### Parsing returns 0 committees

Check the PDF URL is accessible:
```bash
curl -I https://clerk.house.gov/committee_info/scsoal.pdf
```

If it's a different format, the parser might need adjustments.

## Production Deployment

Once tested locally, deploy to Azure:

1. **Deploy infrastructure** (if not already):
   ```bash
   cd infra/bicep
   az deployment sub create \
     --location centralus \
     --template-file main.bicep \
     --parameters environment=prod
   ```

2. **Deploy function code**:
   ```bash
   cd src/CongressStockTrades.Functions
   func azure functionapp publish func-cst-prod-xxx
   ```

3. **Trigger manually in Azure**:
   ```bash
   # Get function key
   FUNC_KEY=$(az functionapp keys list \
     --name func-cst-prod-xxx \
     --resource-group congress-stock-trades-rg \
     --query functionKeys.default -o tsv)

   # Trigger
   curl -X POST "https://func-cst-prod-xxx.azurewebsites.net/api/admin/update-committee-rosters?code=$FUNC_KEY"
   ```

## Weekly Automation

Once deployed, the timer function runs automatically every **Sunday at 2 AM UTC**:
- Checks for new PDF editions
- Skips if no changes
- Processes and stores new rosters
- Emits metrics to Application Insights

## Monitoring

**Application Insights queries (KQL):**

```kusto
// Recent runs
traces
| where message contains "UpdateCommitteeRosters"
| order by timestamp desc

// Entity counts
customMetrics
| where name in ("committees_count", "subcommittees_count", "assignments_count")
| summarize avg(value), max(value) by name, bin(timestamp, 1d)

// Failures
exceptions
| where outerMessage contains "CommitteeRoster"
| order by timestamp desc
```

## Next Steps

- ✅ Test locally (you can do this now!)
- ✅ Verify Cosmos DB data
- ✅ Deploy to Azure
- ✅ Configure alerts for failures
- ✅ Monitor weekly runs

For full details, see [COMMITTEE_ROSTERS.md](COMMITTEE_ROSTERS.md).
