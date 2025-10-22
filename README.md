# Congress Stock Trading Tracker

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Azure Functions](https://img.shields.io/badge/Azure-Functions-0062AD)](https://azure.microsoft.com/en-us/products/functions/)
[![License: ISC](https://img.shields.io/badge/License-ISC-blue.svg)](https://opensource.org/licenses/ISC)

> Serverless real-time monitoring and extraction of U.S. Congress stock trading disclosures using Azure AI.

A cloud-native application that automatically detects, processes, and serves Periodic Transaction Report (PTR) filings from the House of Representatives, extracting structured transaction data from PDFs using Azure AI Document Intelligence.

---

## 🚀 Features

- **Automated Monitoring**: Checks House.gov every 5 minutes for new PTR filings
- **AI-Powered Extraction**: Azure Document Intelligence extracts structured data from PDF forms
- **Committee Enrichment**: Automatically fetches committee memberships from Congress.gov API
- **Real-Time Notifications**: SignalR pushes updates to connected clients instantly
- **REST API**: Query latest transaction data via HTTP endpoints
- **Serverless Architecture**: Auto-scaling Azure Functions with pay-per-execution pricing
- **Zero Downtime Deployments**: Blue-green deployment with staging slots
- **Production Observability**: Application Insights with custom metrics and alerts

---

## 📋 Table of Contents

- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Documentation](#documentation)
- [API Reference](#api-reference)
- [Deployment](#deployment)
- [Cost Estimate](#cost-estimate)
- [Development](#development)
- [Migration from Node.js](#migration-from-nodejs)
- [Contributing](#contributing)
- [License](#license)

---

## 🏗️ Architecture

**Serverless Event-Driven Architecture**

```
House.gov Website
        ↓
Timer Function (every 5 min) → Storage Queue → Queue Function → Document Intelligence
        ↓                                              ↓
   Cosmos DB (check processed)              Cosmos DB (store data)
                                                      ↓
                                             SignalR Service
                                                      ↓
                                                Web Clients
```

### Core Components

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **Timer Function** | Azure Functions | Detect new filings every 5 minutes |
| **Queue Function** | Azure Functions | Process PDFs asynchronously |
| **HTTP Function** | Azure Functions | Serve REST API |
| **Document Intelligence** | Azure AI | Extract structured data from PDFs |
| **Cosmos DB** | Azure Cosmos DB | Store transactions & track processed filings |
| **SignalR Service** | Azure SignalR | Real-time client notifications |
| **Storage Queue** | Azure Storage | Decouple detection from processing |
| **Application Insights** | Azure Monitor | Logging, metrics, and alerts |

### Technology Stack

- **Backend**: C# 12 / .NET 8
- **Compute**: Azure Functions (Consumption Plan)
- **Database**: Azure Cosmos DB (Serverless)
- **AI**: Azure AI Document Intelligence
- **Real-time**: Azure SignalR Service
- **Infrastructure**: Bicep (Infrastructure as Code)
- **CI/CD**: GitHub Actions
- **Testing**: xUnit, Moq, FluentAssertions

---

## ✅ Prerequisites

### Required Tools
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
- [Azure Developer CLI (azd)](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/)

### Azure Subscription
- Active Azure subscription with Contributor role
- Resource providers registered:
  - `Microsoft.Web`
  - `Microsoft.DocumentDB`
  - `Microsoft.CognitiveServices`
  - `Microsoft.SignalRService`

---

## 🚀 Quick Start

### 1. Clone Repository
```bash
git clone https://github.com/yourusername/congress-stock-trades.git
cd congress-stock-trades
```

### 2. Install Dependencies
```bash
dotnet restore src/CongressStockTrades.sln
```

### 3. Get Congress.gov API Key
Sign up for a free API key at [https://api.congress.gov/sign-up/](https://api.congress.gov/sign-up/)
- Free tier includes 5,000 requests per hour
- Used to fetch committee membership information for members

### 4. Configure Local Settings
Create `src/CongressStockTrades.Functions/local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "CosmosDb__Endpoint": "https://localhost:8081/",
    "CosmosDb__Key": "your-cosmos-emulator-key",
    "CosmosDb__DatabaseName": "CongressTrades",
    "DocumentIntelligence__Endpoint": "https://your-docintel.cognitiveservices.azure.com/",
    "DocumentIntelligence__Key": "your-doc-intel-key",
    "SignalR__ConnectionString": "Endpoint=https://your-signalr.service.signalr.net;AccessKey=xxx",
    "CongressApi__ApiKey": "your-congress-api-key-from-api-congress-gov"
  }
}
```

### 5. Run Locally
```bash
# Start Azurite (Storage Emulator)
azurite --silent &

# Start Cosmos DB Emulator (or use cloud instance)

# Run Functions
cd src/CongressStockTrades.Functions
func start
```

### 6. Test Endpoints
```bash
# Health check
curl http://localhost:7071/api/health

# Get latest transaction
curl http://localhost:7071/api/latest
```

---

## 📚 Documentation

Comprehensive documentation is available in the [`docs/`](docs/) directory:

- **[Requirements Document](docs/REQUIREMENTS.md)** - Complete functional and non-functional requirements
- **[System Architecture](docs/ARCHITECTURE.md)** - Architecture diagrams, component details, and design decisions
- **[Implementation Plan](docs/IMPLEMENTATION_PLAN.md)** - Step-by-step development guide (8 phases)
- **[Deployment Guide](docs/DEPLOYMENT.md)** - Infrastructure as Code, CI/CD pipelines, and operations

### Key Diagrams

All sequence diagrams and architecture visuals use Mermaid and are included in the [Architecture document](docs/ARCHITECTURE.md).

---

## 🔌 API Reference

### REST API

#### Get Latest Transaction
```http
GET /api/latest
```

**Response** (200 OK):
```json
{
  "filingId": "20250123456",
  "pdfUrl": "https://disclosures-clerk.house.gov/public_disc/ptr-pdfs/2025/20250123456.pdf",
  "processedAt": "2025-10-14T10:30:00Z",
  "Filing_Information": {
    "Name": "Doe, John",
    "Status": "Filed",
    "State_District": "CA12",
    "Committees": [
      {
        "CommitteeCode": "HSBA",
        "CommitteeName": "Committee on Financial Services",
        "Chamber": "House",
        "Role": "Member",
        "Rank": 5
      }
    ]
  },
  "Transactions": [
    {
      "Asset": "Apple Inc (AAPL)",
      "Transaction_Type": "Purchase",
      "Date": "2025-01-15",
      "Amount": "$15,001 - $50,000"
    }
  ]
}
```

#### Health Check
```http
GET /api/health
```

**Response** (200 OK):
```json
{
  "status": "healthy",
  "timestamp": "2025-10-14T18:00:00Z"
}
```

---

### Real-Time (SignalR)

#### Connect to SignalR
```javascript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
  .withUrl("https://your-function-app.azurewebsites.net/api")
  .build();

connection.on("newFiling", (data) => {
  console.log("New filing received:", data);
  // Update UI with new transaction data
});

await connection.start();
```

---

## 🚀 Deployment

### Option 1: Automated (GitHub Actions)
1. Configure GitHub Secrets (see [Deployment Guide](docs/DEPLOYMENT.md#prerequisites))
2. Push to `main` branch:
   ```bash
   git push origin main
   ```
3. GitHub Actions automatically:
   - Runs tests
   - Deploys infrastructure (Bicep)
   - Deploys code to staging slot
   - Runs smoke tests
   - Swaps to production

### Option 2: Azure Developer CLI (azd)
```bash
# Login to Azure
azd auth login

# Provision + Deploy (one command)
azd up

# Monitor deployment
azd monitor --logs
```

### Option 3: Manual (Azure CLI + Bicep)
```bash
# Deploy infrastructure
az deployment group create \
  --resource-group congress-stock-trades-rg \
  --template-file infra/main.bicep

# Deploy functions
cd src/CongressStockTrades.Functions
func azure functionapp publish congress-stock-trades-prod-func
```

See [Deployment Guide](docs/DEPLOYMENT.md) for detailed instructions.

---

## 💰 Cost Estimate

**Estimated Monthly Cost**: ~$37.60/month

| Service | SKU | Cost |
|---------|-----|------|
| Azure Functions | Consumption Plan | $10 |
| Cosmos DB | Serverless | $25 |
| Document Intelligence | S0 Standard | $1.50 |
| Storage Account | Standard LRS | $1 |
| SignalR Service | Free F1 | $0 |
| Application Insights | Free tier (<5GB) | $0 |
| Key Vault | Standard | $0.10 |

**Cost Drivers**:
- Cosmos DB serverless (scales with usage)
- Function executions (~15K/month)
- Document Intelligence ($1.50 per 1000 pages)

**Scaling**: ~$75/month at 5000 filings/month

---

## 🛠️ Development

### Project Structure
```
congress-stock-trades/
├── src/
│   ├── CongressStockTrades.Functions/     # Azure Functions (triggers)
│   ├── CongressStockTrades.Core/          # Business logic & models
│   ├── CongressStockTrades.Infrastructure/ # Data access & external services
│   └── CongressStockTrades.Tests/         # Unit & integration tests
├── infra/
│   ├── main.bicep                         # Infrastructure definition
│   └── main.parameters.json               # Environment parameters
├── docs/
│   ├── REQUIREMENTS.md
│   ├── ARCHITECTURE.md
│   ├── IMPLEMENTATION_PLAN.md
│   └── DEPLOYMENT.md
├── .github/workflows/                     # CI/CD pipelines
├── legacy-nodejs/                         # Original Node.js implementation (reference)
└── README.md                              # This file
```

### Build & Test
```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build src/CongressStockTrades.sln

# Run tests
dotnet test src/CongressStockTrades.Tests/

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Local Development
```bash
# Watch mode (auto-rebuild on changes)
dotnet watch --project src/CongressStockTrades.Functions

# Debug in VS Code
# Press F5 with Azure Functions extension installed
```

---

## 🔄 Migration from Node.js

This project was migrated from a Node.js/Express implementation to C# Azure Functions.

### Legacy Code Reference
The original Node.js codebase is preserved in [`legacy-nodejs/`](legacy-nodejs/) for reference.

### Key Improvements
- ✅ **Serverless**: Auto-scaling vs single Express server
- ✅ **State Management**: Cosmos DB vs in-memory (`oldPDFUrl`)
- ✅ **PDF Processing**: Document Intelligence (20x cheaper) vs OpenAI
- ✅ **Reliability**: Queue-based with retries vs direct processing
- ✅ **Observability**: Application Insights vs console logs
- ✅ **Deployment**: GitHub Actions + IaC vs manual

See [Architecture Document](docs/ARCHITECTURE.md#architecture-decision-records-adrs) for detailed ADRs.

---

## 🧪 Testing

### Run All Tests
```bash
dotnet test src/CongressStockTrades.Tests/
```

### Test Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage-report"
```

**Target**: >80% code coverage

### Integration Tests
```bash
# Requires live Azure resources
dotnet test --filter "Category=Integration"
```

---

## 📊 Monitoring

### Application Insights Queries (KQL)

#### Filing Processing Rate
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

#### API Performance
```kusto
requests
| where name == "GetLatestTransaction"
| summarize avg(duration), percentile(duration, 95) by bin(timestamp, 5m)
```

See [Deployment Guide](docs/DEPLOYMENT.md#monitoring--alerts) for more queries.

---

## 🤝 Contributing

Contributions are welcome! Please follow these steps:

1. **Fork the repository**
2. **Create a feature branch**: `git checkout -b feature/amazing-feature`
3. **Commit changes**: `git commit -m 'Add amazing feature'`
4. **Push to branch**: `git push origin feature/amazing-feature`
5. **Open Pull Request**

### Pull Request Guidelines
- Include tests for new functionality
- Update documentation as needed
- Ensure all tests pass (`dotnet test`)
- Follow C# coding conventions
- Add entry to CHANGELOG (if applicable)

---

## 📝 License

This project is licensed under the **ISC License**.

```
Copyright (c) 2025 Luis Hernández Martín

Permission to use, copy, modify, and/or distribute this software for any
purpose with or without fee is hereby granted, provided that the above
copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
```

---

## 📧 Contact

**Luis Hernández Martín**
- Email: luisheratm@gmail.com
- GitHub: [@luisher98](https://github.com/luisher98)

**Project Repository**: [https://github.com/luisher98/congress-stock-trades](https://github.com/luisher98/congress-stock-trades)

---

## 🙏 Acknowledgments

- **House of Representatives** - For providing public disclosure data
- **Azure Functions Team** - For excellent serverless platform
- **Document Intelligence Team** - For robust PDF extraction capabilities
- **Open Source Community** - For invaluable tools and libraries

---

## 📈 Project Status

**Status**: 🚧 Active Development (Migration to Azure in progress)

- [x] Requirements documentation
- [x] Architecture design
- [x] Implementation plan
- [x] Infrastructure as Code (Bicep)
- [x] CI/CD pipelines
- [ ] Core services implementation (in progress)
- [ ] Azure Functions development
- [ ] Testing & QA
- [ ] Production deployment

**Current Phase**: Phase 2 - Core Services Implementation

See [Implementation Plan](docs/IMPLEMENTATION_PLAN.md) for detailed roadmap.

---

## 🔗 Related Links

- [House Financial Disclosures](https://disclosures-clerk.house.gov/FinancialDisclosure)
- [Azure Functions Documentation](https://learn.microsoft.com/azure/azure-functions/)
- [Azure AI Document Intelligence](https://learn.microsoft.com/azure/ai-services/document-intelligence/)
- [Azure Cosmos DB](https://learn.microsoft.com/azure/cosmos-db/)
- [Azure SignalR Service](https://learn.microsoft.com/azure/azure-signalr/)

---

<div align="center">

**Built with ❤️ using Azure and .NET**

[⬆ Back to Top](#congress-stock-trading-tracker)

</div>
