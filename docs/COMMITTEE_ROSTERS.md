# Committee Roster Updater

## Overview

The Committee Roster Updater is a weekly automated system that ingests, parses, and stores House committee membership data from official PDF sources published by the Clerk of the House.

## Purpose

- **Automated Committee Tracking**: Monitors and captures committee and subcommittee membership changes weekly
- **Full Provenance**: Every data point includes source date, page number, raw line, blob URI, and PDF hash
- **Change Detection**: Skips processing if PDF content hasn't changed (using cover date + hash comparison)
- **Historical Snapshots**: Preserves assignment history by including source date in keys
- **Quality Assurance**: Optional validation against the Official Alphabetical List (OAL)

## Architecture

### Data Flow

```
Timer Function (Weekly)
    ↓
Download SCSOAL PDF → Compute Hash → Extract Cover Date
    ↓
Check Sources Container (change detection)
    ↓ (if changed)
Upload to Blob Storage → Parse PDF → Extract Entities
    ↓
Upsert to Cosmos DB (Committees, Subcommittees, Members, Assignments)
    ↓
Record Source Document → Optional QA Validation
```

### Cosmos DB Containers

| Container | Partition Key | Purpose |
|-----------|--------------|---------|
| `committees` | `committeeKey` | Store committee metadata |
| `subcommittees` | `committeeKey` | Store subcommittee metadata (partitioned by parent) |
| `members` | `memberKey` | Store member information |
| `assignments` | `committeeKey` | Store member↔committee/subcommittee assignments |
| `sources` | `url` | Track processed PDFs for change detection |
| `qa-findings` | `sourceDate` | Log QA discrepancies (optional) |

### Key Schemas

#### Committee Key
```
Format: normalized-committee-name
Example: agriculture
```

#### Subcommittee Key
```
Format: committeeKey::normalized-subcommittee-name
Example: agriculture::livestock
```

#### Member Key
```
Format: normalized-name-state-district
Example: smith-jason-ca02
```

#### Assignment Key
```
Format: memberKey::committeeOrSubcommitteeKey::sourceDate
Example: smith-jason-ca02::agriculture::2025-09-16
```

## Configuration

### App Settings (local.settings.json)

```json
{
  "CommitteeRosters__Enabled": "true",
  "CommitteeRosters__SCSOALUrl": "https://clerk.house.gov/committee_info/scsoal.pdf",
  "CommitteeRosters__OALUrl": "https://clerk.house.gov/committee_info/oal.pdf",
  "CommitteeRosters__UseOALForQA": "false",
  "CommitteeRosters__EnableDocIntelFallback": "false",
  "CommitteeRosters__ParserVersion": "1.0.0",
  "CommitteeRosters__ChurnThresholdPercent": "0.25"
}
```

### Configuration Options

- **Enabled**: Feature flag to enable/disable the updater
- **SCSOALUrl**: URL to the Standing & Select Committees PDF (required)
- **OALUrl**: URL to the Official Alphabetical List PDF (optional, for QA)
- **UseOALForQA**: Enable QA validation (non-blocking)
- **EnableDocIntelFallback**: Enable Azure Document Intelligence fallback on degraded runs
- **ParserVersion**: Semantic version for parser (tracked in provenance)
- **ChurnThresholdPercent**: Alert threshold for assignment changes (default: 25%)

## Schedule

The function runs weekly on **Sundays at 2:00 AM UTC**.

NCRONTAB expression: `"0 0 2 * * 0"`
- Format: `second minute hour day month dayOfWeek`

### Manual/On-Demand Execution

For immediate testing or updates, use the HTTP-triggered function:

**Local Development:**
```bash
# Basic run (respects change detection)
curl -X POST http://localhost:7071/api/committee-rosters/update

# Force reprocess (bypasses change detection)
curl -X POST "http://localhost:7071/api/committee-rosters/update?force=true"
```

**Azure (using function key):**
```bash
# Get function key from Azure Portal or CLI
FUNCTION_KEY="your-function-key"

# Trigger update
curl -X POST "https://your-function-app.azurewebsites.net/api/committee-rosters/update?code=$FUNCTION_KEY"

# Force reprocess
curl -X POST "https://your-function-app.azurewebsites.net/api/committee-rosters/update?force=true&code=$FUNCTION_KEY"
```

**Response (Success):**
```json
{
  "status": "success",
  "sourceDate": "2025-09-16",
  "pdfHash": "abc123...",
  "blobUri": "https://storage.../scsoal.pdf",
  "forced": false,
  "counts": {
    "committees": 20,
    "subcommittees": 85,
    "members": 435,
    "assignments": 520
  },
  "churnPercent": "5.20%",
  "qaFindings": 2,
  "processedAt": "2025-10-23T10:30:00Z"
}
```

**Response (No Changes):**
```json
{
  "status": "skipped",
  "reason": "no_changes_detected",
  "sourceDate": "2025-09-16",
  "pdfHash": "abc123...",
  "message": "No changes since last run. Use ?force=true to reprocess."
}
```

## Parsing Logic

### Cover Date Extraction
- Searches page 1 for pattern: `MONTH DAY, YYYY` (e.g., "SEPTEMBER 16, 2025")
- Converts to ISO format: `YYYY-MM-DD`

### Committee Detection
- Looks for pattern: `COMMITTEE ON <NAME>`
- Captures committee name and creates normalized key

### Member Line Parsing
- Pattern: `<number>. <name>, of <state>, <role>`
- Extracts: position order, display name, state/district, role
- Roles: Chair, Ranking Member, Vice Chair, Ex Officio, Member

### Subcommittee Detection
- Pattern: `SUBCOMMITTEES OF THE COMMITTEE ON <NAME>`
- Parses subcommittee rosters using same member line logic

### Section Detection
- Tracks MAJORITY vs MINORITY sections for grouping

## Error Handling

### Degraded Runs
A run is marked "Degraded" if:
- Fewer than 8 committees found (expected ~20 standing committees)
- Zero subcommittees found

**Action**: Log warning, do NOT upsert data, optionally trigger DI fallback on next run

### Churn Alerts
If assignment count changes by more than the threshold (default 25%), emit:
- App Insights metric: `churn_percent`
- Warning log with details

### Retries
- Transient HTTP/network errors: Auto-retry (HttpClient built-in)
- Cosmos DB throttling: Auto-retry (Cosmos SDK built-in)
- Parsing exceptions: Fail run, emit `RunFailed` event

## Observability

### Application Insights Events

| Event | When | Properties |
|-------|------|-----------|
| `RunStarted` | Function begins | - |
| `SkippedNoChange` | PDF unchanged | `SourceDate`, `PdfHash` |
| `Parsed` | Parsing complete | `CommitteesCount`, `SubcommitteesCount`, `MembersCount`, `AssignmentsCount`, `Status` |
| `Upserted` | Entities stored | Entity counts |
| `QACompleted` | QA validation done | `FindingsCount` |
| `RunFailed` | Error occurred | `Reason`, `Message` |

### Metrics

- `committees_count`: Number of committees parsed
- `subcommittees_count`: Number of subcommittees parsed
- `members_seen`: Unique members in this run
- `assignments_count`: Total assignments created
- `qa_discrepancies`: QA findings count
- `churn_percent`: Assignment change percentage (if above threshold)

### Alerts (Recommended)

1. **RunFailed**: Any run failure (critical)
2. **High Churn**: `churn_percent > 0.25` (warning)
3. **Degraded**: `Status = "Degraded"` for 2+ consecutive runs (warning)

## QA Validation (Optional)

When enabled (`UseOALForQA=true`):
1. Downloads OAL PDF
2. Extracts member→committee mappings
3. Samples 10 random members
4. Compares SCSOAL vs OAL assignments
5. Logs discrepancies to `qa-findings` container

**Important**: QA is non-blocking. Discrepancies are logged but do not fail the run.

## Blob Storage

PDFs are stored in:
```
committee-rosters/
  2025-09-16/
    scsoal.pdf
  2025-09-23/
    scsoal.pdf
  ...
```

## Document Intelligence Fallback (Future)

If enabled and parser detects anomalies:
- Route PDF to Azure AI Document Intelligence
- Use layout extraction instead of text-based parsing
- Maintain identical DTO outputs and upserts

**Status**: Feature flag exists but implementation is TODO.

## Development

### Running Locally

1. Ensure Cosmos DB emulator or cloud instance is running
2. Configure `local.settings.json` with committee roster settings
3. Run function:
   ```bash
   cd src/CongressStockTrades.Functions
   func start
   ```
4. Trigger manually:
   ```bash
   # Use the HTTP endpoint for immediate testing
   curl -X POST http://localhost:7071/api/committee-rosters/update

   # Or force reprocess
   curl -X POST "http://localhost:7071/api/committee-rosters/update?force=true"
   ```

### Testing

Unit tests are in `CongressStockTrades.Tests/Services/CommitteeRosterParserTests.cs`:
```bash
dotnet test --filter "FullyQualifiedName~CommitteeRosterParser"
```

### Manual Reprocessing

To reprocess a specific date:
1. Delete the source document from `sources` container
2. Run the function (it will reprocess as "new")

### Rolling Back Bad Data

To remove a bad run:
1. Query assignments by `sourceDate`
2. Delete batch from Cosmos DB
3. Delete corresponding source document

## Production Deployment

### Prerequisites

1. Create Cosmos DB containers:
   ```bash
   az cosmosdb sql container create \
     --account-name <account> \
     --database-name CongressTrades \
     --name committees \
     --partition-key-path /committeeKey

   # Repeat for: subcommittees, members, assignments, sources, qa-findings
   ```

2. Add app settings to Azure Function App:
   ```bash
   az functionapp config appsettings set \
     --name <function-app> \
     --resource-group <rg> \
     --settings \
       CommitteeRosters__Enabled=true \
       CommitteeRosters__SCSOALUrl=https://clerk.house.gov/committee_info/scsoal.pdf
   ```

3. Configure alerts in Application Insights (see Alerts section)

### Bicep/IaC Updates

Add to `infra/main.bicep`:
- Cosmos DB containers (if not already existing)
- Blob storage container: `committee-rosters`
- Function app settings

## Data Model Examples

### Committee Document
```json
{
  "id": "agriculture",
  "committeeKey": "agriculture",
  "name": "Committee on Agriculture",
  "chamber": "House",
  "type": "Standing",
  "ratio": "26-22",
  "provenance": {
    "sourceDate": "2025-09-16",
    "pageNumber": 5,
    "blobUri": "https://...scsoal.pdf",
    "pdfHash": "abc123..."
  }
}
```

### Assignment Document
```json
{
  "id": "smith-jason-ca02::agriculture::2025-09-16",
  "committeeKey": "agriculture",
  "assignmentKey": "smith-jason-ca02::agriculture::2025-09-16",
  "memberKey": "smith-jason-ca02",
  "memberDisplayName": "Smith, Jason",
  "committeeAssignmentKey": "agriculture",
  "subcommitteeAssignmentKey": null,
  "role": "Chair",
  "group": "Majority",
  "positionOrder": 1,
  "provenance": {
    "sourceDate": "2025-09-16",
    "pageNumber": 5,
    "blobUri": "https://...scsoal.pdf",
    "pdfHash": "abc123...",
    "rawLine": "1. Smith, Jason, of Missouri, Chair"
  }
}
```

## Future Enhancements

1. **Document Intelligence Integration**: Robust fallback for format changes
2. **Party Affiliation Mapping**: Cross-reference with Congress.gov API
3. **Notification System**: Alert on significant membership changes
4. **API Endpoints**: Serve committee rosters via HTTP functions
5. **Historical Queries**: Enable time-based roster lookups

## References

- [House Committee Info](https://clerk.house.gov/committee_info)
- [SCSOAL PDF](https://clerk.house.gov/committee_info/scsoal.pdf)
- [OAL PDF](https://clerk.house.gov/committee_info/oal.pdf)
- [Congress.gov API](https://api.congress.gov/)

---

**Last Updated**: 2025-10-23
**Status**: ✅ Implemented, Ready for Testing
