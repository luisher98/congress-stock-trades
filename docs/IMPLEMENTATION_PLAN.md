# Congress Stock Trading Tracker - Implementation Plan

## Table of Contents
1. [Project Overview](#project-overview)
2. [Prerequisites](#prerequisites)
3. [Phase 1: Project Setup](#phase-1-project-setup)
4. [Phase 2: Core Services Implementation](#phase-2-core-services-implementation)
5. [Phase 3: Azure Functions Development](#phase-3-azure-functions-development)
6. [Phase 4: Infrastructure as Code](#phase-4-infrastructure-as-code)
7. [Phase 5: CI/CD Pipeline](#phase-5-cicd-pipeline)
8. [Phase 6: Testing & Quality Assurance](#phase-6-testing--quality-assurance)
9. [Phase 7: Deployment & Migration](#phase-7-deployment--migration)
10. [Phase 8: Monitoring & Optimization](#phase-8-monitoring--optimization)

---

## 1. Project Overview

### 1.1 Migration Goals
Convert Node.js/Express application to C# Azure Functions with the following improvements:
- Serverless architecture for cost efficiency
- Better PDF processing with Document Intelligence
- Scalable real-time notifications via SignalR
- Infrastructure as Code for reproducible deployments
- Automated CI/CD with GitHub Actions

### 1.2 Timeline Estimate
**Total Duration**: 4-6 weeks (assuming 1 developer, part-time)

| Phase | Duration | Dependencies |
|-------|----------|--------------|
| Phase 1: Setup | 1-2 days | None |
| Phase 2: Core Services | 3-5 days | Phase 1 |
| Phase 3: Functions | 4-6 days | Phase 2 |
| Phase 4: Infrastructure | 2-3 days | Phase 1 |
| Phase 5: CI/CD | 2-3 days | Phase 3, 4 |
| Phase 6: Testing | 3-5 days | Phase 3 |
| Phase 7: Deployment | 2-3 days | Phase 5, 6 |
| Phase 8: Monitoring | 2-3 days | Phase 7 |

---

## 2. Prerequisites

### 2.1 Required Tools & Accounts

#### Development Tools
- [ ] **.NET SDK 8.0** - Download from https://dot.net
- [ ] **Visual Studio 2022** or **VS Code** with C# extension
- [ ] **Azure Functions Core Tools v4** - `npm install -g azure-functions-core-tools@4`
- [ ] **Git** - Version control
- [ ] **Postman** or **REST Client** - API testing

#### Azure Tools
- [ ] **Azure CLI** - `brew install azure-cli` (macOS)
- [ ] **Azure Developer CLI (azd)** - `brew install azure-dev` (macOS)
- [ ] **Bicep CLI** - Included with Azure CLI
- [ ] **Azure Storage Explorer** - For queue inspection

#### Local Emulators
- [ ] **Azurite** - `npm install -g azurite` (Azure Storage emulator)
- [ ] **Cosmos DB Emulator** (Windows) or use cloud instance for Mac

#### Accounts & Subscriptions
- [ ] **Azure Subscription** with Owner or Contributor role
- [ ] **GitHub Account** with repository access
- [ ] **Azure DevOps** (optional, if not using GitHub Actions)

---

### 2.2 Azure Subscription Preparation

#### Step 1: Verify Subscription Limits
```bash
# Check available quota
az vm list-usage --location eastus --output table
```

#### Step 2: Create Service Principal for GitHub Actions
```bash
# Create service principal
az ad sp create-for-rbac \
  --name "CongressTrades-GitHub-SP" \
  --role contributor \
  --scopes /subscriptions/{subscription-id} \
  --sdk-auth

# Save output JSON to GitHub Secrets as AZURE_CREDENTIALS
```

#### Step 3: Register Resource Providers
```bash
az provider register --namespace Microsoft.Web
az provider register --namespace Microsoft.DocumentDB
az provider register --namespace Microsoft.CognitiveServices
az provider register --namespace Microsoft.SignalRService
az provider register --namespace Microsoft.Storage
az provider register --namespace Microsoft.Insights
```

---

### 2.3 GitHub Repository Setup

#### Step 1: Create Repository
```bash
cd congress-stock-trades
git init
git remote add origin https://github.com/yourusername/congress-stock-trades-csharp.git
```

#### Step 2: Configure GitHub Secrets
Navigate to **Settings → Secrets and variables → Actions** and add:

| Secret Name | Value | Source |
|-------------|-------|--------|
| `AZURE_CLIENT_ID` | `xxx-xxx-xxx` | Service Principal |
| `AZURE_TENANT_ID` | `xxx-xxx-xxx` | Service Principal |
| `AZURE_SUBSCRIPTION_ID` | `xxx-xxx-xxx` | Azure Portal |

#### Step 3: Configure GitHub Variables
Navigate to **Settings → Secrets and variables → Actions → Variables**:

| Variable Name | Value |
|--------------|-------|
| `AZURE_ENV_NAME` | `congress-prod` |
| `AZURE_LOCATION` | `eastus` |
| `AZURE_RESOURCE_GROUP` | `congress-stock-trades-rg` |

---

## 3. Phase 1: Project Setup

### 3.1 Create Solution Structure

#### Step 1: Initialize .NET Solution
```bash
# Navigate to project root
cd congress-stock-trades

# Create solution
dotnet new sln -n CongressStockTrades

# Create directory structure
mkdir -p src/{CongressStockTrades.Functions,CongressStockTrades.Core,CongressStockTrades.Infrastructure,CongressStockTrades.Tests}
mkdir -p infra/resources
mkdir -p docs
mkdir -p .github/workflows
```

#### Step 2: Create Projects
```bash
# Azure Functions project
dotnet new func \
  -n CongressStockTrades.Functions \
  -o src/CongressStockTrades.Functions \
  --worker-runtime dotnet-isolated

# Core business logic library
dotnet new classlib \
  -n CongressStockTrades.Core \
  -o src/CongressStockTrades.Core \
  -f net8.0

# Infrastructure library
dotnet new classlib \
  -n CongressStockTrades.Infrastructure \
  -o src/CongressStockTrades.Infrastructure \
  -f net8.0

# Test project
dotnet new xunit \
  -n CongressStockTrades.Tests \
  -o src/CongressStockTrades.Tests \
  -f net8.0

# Add projects to solution
dotnet sln add src/CongressStockTrades.Functions/CongressStockTrades.Functions.csproj
dotnet sln add src/CongressStockTrades.Core/CongressStockTrades.Core.csproj
dotnet sln add src/CongressStockTrades.Infrastructure/CongressStockTrades.Infrastructure.csproj
dotnet sln add src/CongressStockTrades.Tests/CongressStockTrades.Tests.csproj
```

#### Step 3: Add Project References
```bash
# Functions depends on Core and Infrastructure
cd src/CongressStockTrades.Functions
dotnet add reference ../CongressStockTrades.Core/CongressStockTrades.Core.csproj
dotnet add reference ../CongressStockTrades.Infrastructure/CongressStockTrades.Infrastructure.csproj

# Infrastructure depends on Core
cd ../CongressStockTrades.Infrastructure
dotnet add reference ../CongressStockTrades.Core/CongressStockTrades.Core.csproj

# Tests reference all projects
cd ../CongressStockTrades.Tests
dotnet add reference ../CongressStockTrades.Core/CongressStockTrades.Core.csproj
dotnet add reference ../CongressStockTrades.Infrastructure/CongressStockTrades.Infrastructure.csproj
dotnet add reference ../CongressStockTrades.Functions/CongressStockTrades.Functions.csproj
```

---

### 3.2 Install NuGet Packages

#### Core Project Dependencies
```bash
cd src/CongressStockTrades.Core

# No external dependencies for core models
```

#### Infrastructure Project Dependencies
```bash
cd src/CongressStockTrades.Infrastructure

# Azure SDK packages
dotnet add package Azure.AI.FormRecognizer --version 4.1.0
dotnet add package Microsoft.Azure.Cosmos --version 3.39.0
dotnet add package Azure.Storage.Queues --version 12.18.0
dotnet add package HtmlAgilityPack --version 1.11.59

# Configuration
dotnet add package Microsoft.Extensions.Configuration --version 8.0.0
dotnet add package Microsoft.Extensions.Configuration.Abstractions --version 8.0.0

# Logging
dotnet add package Microsoft.Extensions.Logging.Abstractions --version 8.0.0

# HTTP
dotnet add package Microsoft.Extensions.Http --version 8.0.0
```

#### Functions Project Dependencies
```bash
cd src/CongressStockTrades.Functions

# Azure Functions packages (should already be included)
dotnet add package Microsoft.Azure.Functions.Worker --version 1.21.0
dotnet add package Microsoft.Azure.Functions.Worker.Sdk --version 1.17.0

# SignalR binding
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.SignalRService --version 1.9.0

# Storage Queue binding
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues --version 6.2.0

# Timer trigger
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.Timer --version 4.3.0

# Application Insights
dotnet add package Microsoft.ApplicationInsights.WorkerService --version 2.22.0

# Dependency Injection
dotnet add package Microsoft.Extensions.DependencyInjection --version 8.0.0
```

#### Test Project Dependencies
```bash
cd src/CongressStockTrades.Tests

# Testing frameworks
dotnet add package xUnit --version 2.6.6
dotnet add package xunit.runner.visualstudio --version 2.5.6
dotnet add package Moq --version 4.20.70
dotnet add package FluentAssertions --version 6.12.0
dotnet add package Microsoft.NET.Test.Sdk --version 17.9.0
```

---

### 3.3 Create Configuration Files

#### File: `src/CongressStockTrades.Functions/host.json`
```json
{
  "version": "2.0",
  "logging": {
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true,
        "maxTelemetryItemsPerSecond": 20
      }
    }
  },
  "extensions": {
    "queues": {
      "maxDequeueCount": 5,
      "visibilityTimeout": "00:05:00",
      "batchSize": 1,
      "newBatchThreshold": 0
    }
  },
  "functionTimeout": "00:05:00"
}
```

#### File: `src/CongressStockTrades.Functions/local.settings.json`
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "CosmosDb__Endpoint": "https://localhost:8081/",
    "CosmosDb__Key": "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
    "CosmosDb__DatabaseName": "CongressTrades",
    "DocumentIntelligence__Endpoint": "https://your-docintel.cognitiveservices.azure.com/",
    "DocumentIntelligence__Key": "your-key-here",
    "SignalR__ConnectionString": "Endpoint=https://your-signalr.service.signalr.net;AccessKey=xxx",
    "HouseGov__BaseUrl": "https://disclosures-clerk.house.gov",
    "APPLICATIONINSIGHTS_CONNECTION_STRING": ""
  }
}
```

**Important**: Add `local.settings.json` to `.gitignore`

#### File: `.gitignore`
```
# Azure Functions
local.settings.json
bin/
obj/
.vs/
.vscode/
*.user
*.suo

# .NET
*.dll
*.exe
*.pdb
*.cache

# Azure
.azure/
.azd/

# Secrets
*.pfx
*.key
secrets.json
```

---

### 3.4 Create Global Configuration File

#### File: `global.json`
```json
{
  "sdk": {
    "version": "8.0.100",
    "rollForward": "latestMinor"
  }
}
```

---

### 3.5 Verify Setup

```bash
# Build entire solution
dotnet build

# Run tests (should pass with empty test class)
dotnet test

# Verify Functions project
cd src/CongressStockTrades.Functions
func start
```

**Expected Output**:
```
Azure Functions Core Tools
Core Tools Version: 4.x.x
Function Runtime Version: 4.x.x

[timestamp] Executing 'Functions.Startup' (Reason='Host startup')
[timestamp] Executed 'Functions.Startup' (Succeeded)
```

---

## 4. Phase 2: Core Services Implementation

### 4.1 Define Data Models

#### File: `src/CongressStockTrades.Core/Models/Filing.cs`
```csharp
namespace CongressStockTrades.Core.Models;

/// <summary>
/// Represents a filing metadata from the House website
/// </summary>
public class Filing
{
    /// <summary>
    /// Unique filing identifier extracted from PDF URL
    /// Example: "20250123456"
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Politician's name from website listing
    /// Example: "Doe, John"
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Office/District information
    /// Example: "CA12"
    /// </summary>
    public required string Office { get; set; }

    /// <summary>
    /// Year of filing
    /// </summary>
    public required string FilingYear { get; set; }

    /// <summary>
    /// Full URL to PDF document
    /// </summary>
    public required string PdfUrl { get; set; }
}
```

#### File: `src/CongressStockTrades.Core/Models/FilingMessage.cs`
```csharp
namespace CongressStockTrades.Core.Models;

/// <summary>
/// Message payload for Storage Queue
/// </summary>
public class FilingMessage
{
    public required string FilingId { get; set; }
    public required string PdfUrl { get; set; }
    public required string Name { get; set; }
    public required string Office { get; set; }
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
}
```

#### File: `src/CongressStockTrades.Core/Models/TransactionDocument.cs`
```csharp
using System.Text.Json.Serialization;

namespace CongressStockTrades.Core.Models;

/// <summary>
/// Complete transaction document stored in Cosmos DB
/// </summary>
public class TransactionDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public required string FilingId { get; set; }
    public required string PdfUrl { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public required FilingInformation Filing_Information { get; set; }
    public required List<Transaction> Transactions { get; set; }
}

public class FilingInformation
{
    public required string Name { get; set; }
    public required string Status { get; set; }
    public required string State_District { get; set; }
}

public class Transaction
{
    public required string ID_Owner { get; set; }
    public required string Asset { get; set; }
    public required string Transaction_Type { get; set; }
    public required string Date { get; set; }
    public required string Amount { get; set; }
}
```

#### File: `src/CongressStockTrades.Core/Models/ProcessedFiling.cs`
```csharp
using System.Text.Json.Serialization;

namespace CongressStockTrades.Core.Models;

/// <summary>
/// Lightweight document to track processed filings
/// </summary>
public class ProcessedFiling
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    public required string PdfUrl { get; set; }
    public required string Politician { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public required string Status { get; set; } // "completed" | "failed"
    public string? ErrorMessage { get; set; }
}
```

---

### 4.2 Implement Filing Fetcher Service

#### File: `src/CongressStockTrades.Core/Services/IFilingFetcher.cs`
```csharp
namespace CongressStockTrades.Core.Services;

public interface IFilingFetcher
{
    /// <summary>
    /// Fetches the latest filing for a given year
    /// </summary>
    Task<Filing?> GetLatestFilingAsync(int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches all filings for a given year
    /// </summary>
    Task<List<Filing>> GetFilingsAsync(int year, CancellationToken cancellationToken = default);
}
```

#### File: `src/CongressStockTrades.Infrastructure/Services/FilingFetcher.cs`
```csharp
using CongressStockTrades.Core.Models;
using CongressStockTrades.Core.Services;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace CongressStockTrades.Infrastructure.Services;

public class FilingFetcher : IFilingFetcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FilingFetcher> _logger;
    private const string BaseUrl = "https://disclosures-clerk.house.gov";

    public FilingFetcher(HttpClient httpClient, ILogger<FilingFetcher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Filing?> GetLatestFilingAsync(int year, CancellationToken cancellationToken = default)
    {
        var filings = await GetFilingsAsync(year, cancellationToken);
        return filings.FirstOrDefault();
    }

    public async Task<List<Filing>> GetFilingsAsync(int year, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching filings for year {Year}", year);

        var url = $"{BaseUrl}/FinancialDisclosure/ViewMemberSearchResult?filingYear={year}";
        var response = await _httpClient.PostAsync(url, null, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to fetch filings: {StatusCode}", response.StatusCode);
            return new List<Filing>();
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseHtml(html, year);
    }

    private List<Filing> ParseHtml(string html, int year)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var rows = doc.DocumentNode.SelectNodes("//tbody//tr");
        if (rows == null)
        {
            _logger.LogWarning("No table rows found in HTML");
            return new List<Filing>();
        }

        var filings = new List<Filing>();

        foreach (var row in rows)
        {
            try
            {
                var nameElement = row.SelectSingleNode(".//td[@data-label='Name']//a");
                var officeElement = row.SelectSingleNode(".//td[@data-label='Office']");
                var filingYearElement = row.SelectSingleNode(".//td[@data-label='Filing Year']");
                var filingTypeElement = row.SelectSingleNode(".//td[@data-label='Filing']");

                // Skip if not PTR filing
                if (filingTypeElement == null || !filingTypeElement.InnerText.Contains("PTR"))
                    continue;

                if (nameElement == null || officeElement == null || filingYearElement == null)
                    continue;

                var href = nameElement.GetAttributeValue("href", string.Empty);
                if (string.IsNullOrEmpty(href) || !href.Contains("ptr-pdfs"))
                    continue;

                // Extract filing ID from URL
                var match = Regex.Match(href, @"/(\d+)\.pdf$");
                if (!match.Success)
                {
                    _logger.LogWarning("Could not extract filing ID from href: {Href}", href);
                    continue;
                }

                var filingId = match.Groups[1].Value;
                var name = nameElement.InnerText.Trim();
                var office = officeElement.InnerText.Trim();
                var filingYear = filingYearElement.InnerText.Trim();

                var cleanHref = href.StartsWith("/") ? href : "/" + href;
                var pdfUrl = new Uri(new Uri(BaseUrl), cleanHref).ToString();

                filings.Add(new Filing
                {
                    Id = filingId,
                    Name = name,
                    Office = office,
                    FilingYear = filingYear,
                    PdfUrl = pdfUrl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing filing row");
            }
        }

        // Sort by ID descending (most recent first)
        return filings.OrderByDescending(f => long.Parse(f.Id)).ToList();
    }
}
```

---

### 4.3 Implement PDF Processor Service

#### File: `src/CongressStockTrades.Core/Services/IPdfProcessor.cs`
```csharp
namespace CongressStockTrades.Core.Services;

public interface IPdfProcessor
{
    /// <summary>
    /// Downloads and processes a PDF to extract transaction data
    /// </summary>
    Task<TransactionDocument> ProcessPdfAsync(
        string pdfUrl,
        string filingId,
        string expectedName,
        string expectedOffice,
        CancellationToken cancellationToken = default);
}
```

#### File: `src/CongressStockTrades.Infrastructure/Services/PdfProcessor.cs`
```csharp
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using CongressStockTrades.Core.Models;
using CongressStockTrades.Core.Services;
using Microsoft.Extensions.Logging;

namespace CongressStockTrades.Infrastructure.Services;

public class PdfProcessor : IPdfProcessor
{
    private readonly HttpClient _httpClient;
    private readonly DocumentAnalysisClient _docIntelClient;
    private readonly ILogger<PdfProcessor> _logger;

    public PdfProcessor(
        HttpClient httpClient,
        DocumentAnalysisClient docIntelClient,
        ILogger<PdfProcessor> logger)
    {
        _httpClient = httpClient;
        _docIntelClient = docIntelClient;
        _logger = logger;
    }

    public async Task<TransactionDocument> ProcessPdfAsync(
        string pdfUrl,
        string filingId,
        string expectedName,
        string expectedOffice,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing PDF for filing {FilingId}", filingId);

        // Download PDF to memory
        using var pdfStream = await _httpClient.GetStreamAsync(pdfUrl, cancellationToken);

        // Analyze with Document Intelligence
        var operation = await _docIntelClient.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "prebuilt-layout",
            pdfStream,
            cancellationToken: cancellationToken);

        var result = operation.Value;

        // Extract filing information
        var filingInfo = ExtractFilingInformation(result);

        // Extract transactions from tables
        var transactions = ExtractTransactions(result);

        _logger.LogInformation("Extracted {Count} transactions from PDF", transactions.Count);

        return new TransactionDocument
        {
            FilingId = filingId,
            PdfUrl = pdfUrl,
            Filing_Information = filingInfo,
            Transactions = transactions
        };
    }

    private FilingInformation ExtractFilingInformation(AnalyzeResult result)
    {
        // Extract key-value pairs for filing info
        var kvPairs = result.KeyValuePairs;

        var name = kvPairs
            .FirstOrDefault(kv => kv.Key.Content.Contains("Name", StringComparison.OrdinalIgnoreCase))
            ?.Value.Content ?? "Unknown";

        var status = kvPairs
            .FirstOrDefault(kv => kv.Key.Content.Contains("Status", StringComparison.OrdinalIgnoreCase))
            ?.Value.Content ?? "Filed";

        var district = kvPairs
            .FirstOrDefault(kv => kv.Key.Content.Contains("District", StringComparison.OrdinalIgnoreCase))
            ?.Value.Content ?? "Unknown";

        return new FilingInformation
        {
            Name = name.Trim(),
            Status = status.Trim(),
            State_District = district.Trim()
        };
    }

    private List<Transaction> ExtractTransactions(AnalyzeResult result)
    {
        var transactions = new List<Transaction>();

        foreach (var table in result.Tables)
        {
            // Skip header row (index 0)
            var rowGroups = table.Cells
                .Where(c => c.RowIndex > 0)
                .GroupBy(c => c.RowIndex)
                .OrderBy(g => g.Key);

            foreach (var row in rowGroups)
            {
                var cells = row.OrderBy(c => c.ColumnIndex).ToList();

                if (cells.Count < 5)
                    continue;

                transactions.Add(new Transaction
                {
                    Asset = cells[0].Content.Trim(),
                    Transaction_Type = cells[1].Content.Trim(),
                    Date = cells[2].Content.Trim(),
                    Amount = cells[3].Content.Trim(),
                    ID_Owner = cells[4].Content.Trim()
                });
            }
        }

        return transactions;
    }
}
```

---

### 4.4 Checkpoint: Test Core Services

#### File: `src/CongressStockTrades.Tests/Services/FilingFetcherTests.cs`
```csharp
using CongressStockTrades.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;

namespace CongressStockTrades.Tests.Services;

public class FilingFetcherTests
{
    [Fact]
    public async Task GetLatestFilingAsync_ReturnsLatestFiling()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = new NullLogger<FilingFetcher>();
        var fetcher = new FilingFetcher(httpClient, logger);

        // Act
        var result = await fetcher.GetLatestFilingAsync(DateTime.Now.Year);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().NotBeNullOrEmpty();
        result.PdfUrl.Should().StartWith("https://disclosures-clerk.house.gov");
    }
}
```

**Run Test**:
```bash
dotnet test --filter "FullyQualifiedName~FilingFetcherTests"
```

---

## 5. Phase 3: Azure Functions Development

### 5.1 Configure Dependency Injection

#### File: `src/CongressStockTrades.Functions/Program.cs`
```csharp
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using CongressStockTrades.Core.Services;
using CongressStockTrades.Infrastructure.Repositories;
using CongressStockTrades.Infrastructure.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // HTTP Client
        services.AddHttpClient<IFilingFetcher, FilingFetcher>();
        services.AddHttpClient<IPdfProcessor, PdfProcessor>();

        // Document Intelligence
        services.AddSingleton(sp =>
        {
            var endpoint = configuration["DocumentIntelligence__Endpoint"]!;
            var key = configuration["DocumentIntelligence__Key"]!;
            return new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(key));
        });

        // Cosmos DB
        services.AddSingleton<ITransactionRepository, TransactionRepository>();

        // Services
        services.AddScoped<IFilingFetcher, FilingFetcher>();
        services.AddScoped<IPdfProcessor, PdfProcessor>();
        services.AddScoped<IDataValidator, DataValidator>();
    })
    .Build();

host.Run();
```

---

### 5.2 Implement Timer Function (Check New Filings)

#### File: `src/CongressStockTrades.Functions/Triggers/CheckNewFilingsTrigger.cs`
```csharp
using CongressStockTrades.Core.Models;
using CongressStockTrades.Core.Services;
using CongressStockTrades.Infrastructure.Repositories;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CongressStockTrades.Functions.Triggers;

public class CheckNewFilingsTrigger
{
    private readonly IFilingFetcher _filingFetcher;
    private readonly ITransactionRepository _repository;
    private readonly ILogger<CheckNewFilingsTrigger> _logger;

    public CheckNewFilingsTrigger(
        IFilingFetcher filingFetcher,
        ITransactionRepository repository,
        ILogger<CheckNewFilingsTrigger> logger)
    {
        _filingFetcher = filingFetcher;
        _repository = repository;
        _logger = logger;
    }

    [Function(nameof(CheckNewFilingsTrigger))]
    [QueueOutput("filings-to-process")]
    public async Task<string?> Run(
        [TimerTrigger("0 */5 * * * *")] TimerInfo timerInfo)
    {
        _logger.LogInformation("Checking for new filings at {Time}", DateTime.UtcNow);

        var currentYear = DateTime.UtcNow.Year;
        var latestFiling = await _filingFetcher.GetLatestFilingAsync(currentYear);

        if (latestFiling == null)
        {
            _logger.LogWarning("No filings found for year {Year}", currentYear);
            return null;
        }

        // Check if already processed
        var isProcessed = await _repository.IsFilingProcessedAsync(latestFiling.Id);

        if (isProcessed)
        {
            _logger.LogInformation("Filing {FilingId} already processed", latestFiling.Id);
            return null;
        }

        _logger.LogInformation("New filing detected: {FilingId}", latestFiling.Id);

        var message = new FilingMessage
        {
            FilingId = latestFiling.Id,
            PdfUrl = latestFiling.PdfUrl,
            Name = latestFiling.Name,
            Office = latestFiling.Office
        };

        return JsonSerializer.Serialize(message);
    }
}
```

---

### 5.3 Implement Queue Function (Process Filing)

#### File: `src/CongressStockTrades.Functions/Triggers/ProcessFilingTrigger.cs`
```csharp
using CongressStockTrades.Core.Models;
using CongressStockTrades.Core.Services;
using CongressStockTrades.Infrastructure.Repositories;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CongressStockTrades.Functions.Triggers;

public class ProcessFilingTrigger
{
    private readonly IPdfProcessor _pdfProcessor;
    private readonly IDataValidator _validator;
    private readonly ITransactionRepository _repository;
    private readonly ILogger<ProcessFilingTrigger> _logger;

    public ProcessFilingTrigger(
        IPdfProcessor pdfProcessor,
        IDataValidator validator,
        ITransactionRepository repository,
        ILogger<ProcessFilingTrigger> logger)
    {
        _pdfProcessor = pdfProcessor;
        _validator = validator;
        _repository = repository;
        _logger = logger;
    }

    [Function(nameof(ProcessFilingTrigger))]
    [SignalROutput(HubName = "filings")]
    public async Task<SignalRMessageAction> Run(
        [QueueTrigger("filings-to-process")] string messageBody)
    {
        _logger.LogInformation("Processing filing message");

        var message = JsonSerializer.Deserialize<FilingMessage>(messageBody)
            ?? throw new InvalidOperationException("Failed to deserialize message");

        try
        {
            // Process PDF
            var transactionDoc = await _pdfProcessor.ProcessPdfAsync(
                message.PdfUrl,
                message.FilingId,
                message.Name,
                message.Office);

            // Validate
            _validator.Validate(transactionDoc, message.Name, message.Office);

            // Store in Cosmos DB
            await _repository.StoreTransactionAsync(transactionDoc);

            // Mark as processed
            await _repository.MarkAsProcessedAsync(message.FilingId, message.PdfUrl, message.Name);

            _logger.LogInformation("Successfully processed filing {FilingId}", message.FilingId);

            // Return SignalR message
            return new SignalRMessageAction("newFiling")
            {
                Arguments = new[] { transactionDoc }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process filing {FilingId}", message.FilingId);
            throw; // Will retry or move to poison queue
        }
    }
}
```

---

### 5.4 Implement HTTP Function (REST API)

#### File: `src/CongressStockTrades.Functions/Triggers/GetLatestTransactionTrigger.cs`
```csharp
using CongressStockTrades.Infrastructure.Repositories;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace CongressStockTrades.Functions.Triggers;

public class GetLatestTransactionTrigger
{
    private readonly ITransactionRepository _repository;
    private readonly ILogger<GetLatestTransactionTrigger> _logger;

    public GetLatestTransactionTrigger(
        ITransactionRepository repository,
        ILogger<GetLatestTransactionTrigger> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [Function(nameof(GetLatestTransaction))]
    public async Task<HttpResponseData> GetLatestTransaction(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "latest")] HttpRequestData req)
    {
        _logger.LogInformation("GET /api/latest request received");

        try
        {
            var latestTransaction = await _repository.GetLatestTransactionAsync();

            if (latestTransaction == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteAsJsonAsync(new { error = "No transaction data found" });
                return notFoundResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(latestTransaction);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving latest transaction");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Failed to retrieve data" });
            return errorResponse;
        }
    }

    [Function(nameof(HealthCheck))]
    public async Task<HttpResponseData> HealthCheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { status = "healthy", timestamp = DateTime.UtcNow });
        return response;
    }
}
```

---

**Continue to next phase?** This is getting quite long. I can continue with:
- Phase 4: Infrastructure as Code (Bicep templates)
- Phase 5: CI/CD Pipeline (GitHub Actions)
- Phase 6-8: Testing, Deployment, Monitoring

Would you like me to continue in this same document or split into separate files?