# Congress Stock Trading Tracker - Requirements Document

## Table of Contents
1. [Executive Summary](#executive-summary)
2. [Functional Requirements](#functional-requirements)
3. [Non-Functional Requirements](#non-functional-requirements)
4. [Azure Services Required](#azure-services-required)
5. [Data Models](#data-models)

---

## 1. Executive Summary

### 1.1 Project Overview
A serverless cloud application that monitors, processes, and serves U.S. Congress politician stock trading disclosure data in real-time. The system automatically detects new filings, extracts structured data from PDF documents using AI, and provides real-time notifications to connected clients.

### 1.2 Current State (Node.js)
- Express.js web server with REST and SSE endpoints
- MongoDB for data storage
- OpenAI Assistant API for PDF processing
- Simple HTTP fetch for website data (no longer uses Puppeteer)
- Periodic checking via setTimeout
- In-memory state management

### 1.3 Target State (C# + Azure)
- Serverless Azure Functions architecture
- Azure Cosmos DB for data persistence
- Azure AI Document Intelligence for PDF extraction
- Azure SignalR Service for real-time notifications
- Azure Storage Queues for message passing
- Infrastructure as Code (Bicep)
- Automated CI/CD with GitHub Actions

---

## 2. Functional Requirements

### 2.1 Core Features

#### FR-1: Automated Filing Monitoring
- **Description**: System shall periodically check the House disclosure website for new PTR (Periodic Transaction Report) filings
- **Frequency**: Every 5 minutes
- **Source**: `https://disclosures-clerk.house.gov/FinancialDisclosure/ViewMemberSearchResult?filingYear={year}`
- **Detection**: Compare latest filing ID against last processed filing ID stored in Cosmos DB
- **Output**: If new filing detected, queue message for processing

#### FR-2: PDF Processing
- **Description**: System shall download PDF filings and extract structured transaction data
- **Input**: PDF URL from government website
- **Processing**:
  - Download PDF to memory stream (no persistent storage)
  - Send to Azure AI Document Intelligence
  - Parse layout model response (tables, key-value pairs)
  - Transform into structured JSON
- **Output**:
  ```json
  {
    "Filing_Information": {
      "Name": "Last, First",
      "Status": "Filed",
      "State_District": "XX00"
    },
    "Transactions": [
      {
        "ID_Owner": "Self",
        "Asset": "Company Stock",
        "Transaction_Type": "Purchase",
        "Date": "2023-01-01",
        "Amount": "$1,001 - $15,000"
      }
    ]
  }
  ```

#### FR-3: Data Validation
- **Description**: System shall validate extracted data against website metadata
- **Validations**:
  - Name from PDF matches name from website listing
  - Office/District from PDF matches website listing
  - Filing contains at least one transaction
  - Required fields are present (Date, Asset, Type, Amount)
- **On Failure**: Log error, mark filing as failed, send to dead-letter queue

#### FR-4: Data Persistence
- **Description**: System shall store processed filing data with deduplication
- **Collections**:
  - `transactions`: Complete filing data with transaction array and PDF URL reference
  - `processed-filings`: Tracking collection to prevent reprocessing
- **Deduplication**: Use filing ID as unique identifier
- **Retention**: Indefinite (Cosmos DB serverless)
- **PDF Storage**: Store only URL reference, not the file itself

#### FR-5: Real-Time Notifications
- **Description**: System shall notify connected clients when new filings are processed
- **Protocol**: Server-Sent Events (SSE) via Azure SignalR Service
- **Payload**: Complete filing information + transaction data
- **Connection Management**: Auto-reconnect on disconnect

#### FR-6: REST API
- **Description**: System shall provide HTTP endpoints for querying data
- **Endpoints**:
  - `GET /api/latest` - Retrieve most recent processed filing
  - `GET /api/health` - Health check endpoint
  - `GET /api/negotiate` - SignalR connection negotiation (automatic)

---

## 3. Non-Functional Requirements

### 3.1 Performance
- **NFR-1.1**: PDF processing shall complete in < 10 seconds per filing
- **NFR-1.2**: API response time shall be < 500ms for latest transaction query
- **NFR-1.3**: Queue message processing shall begin within 30 seconds of queuing

### 3.2 Reliability
- **NFR-2.1**: System uptime: 99.5% availability (Azure Functions SLA)
- **NFR-2.2**: Retry Policy:
  - Automatic retry on transient failures (5 attempts)
  - Exponential backoff between retries (5 minute visibility timeout)
  - Dead-letter queue for permanent failures
- **NFR-2.3**: Error Handling: All exceptions logged to Application Insights with correlation IDs

### 3.3 Scalability
- **NFR-3.1**: Support 1-1000 filings per month volume
- **NFR-3.2**: Handle burst of 10 concurrent PDF processing requests
- **NFR-3.3**: Support 100 concurrent SignalR connections (Free tier limit)

### 3.4 Security
- **NFR-4.1**: Authentication: Function keys for admin endpoints
- **NFR-4.2**: Secrets Management: Azure Key Vault for API keys
- **NFR-4.3**: Managed Identity: Functions access Azure services without connection strings
- **NFR-4.4**: CORS: Configurable allowed origins
- **NFR-4.5**: HTTPS: All endpoints TLS 1.2+
- **NFR-4.6**: No credential storage in source code or configuration files

### 3.5 Maintainability
- **NFR-5.1**: Code Quality: Unit tests with >80% coverage
- **NFR-5.2**: Documentation: XML comments on all public APIs
- **NFR-5.3**: Logging: Structured logging to Application Insights
- **NFR-5.4**: Monitoring: Custom metrics and alerts for critical operations

### 3.6 Cost Optimization
- **NFR-6.1**: Target monthly cost: <$50 for normal operations
- **NFR-6.2**: Cosmos DB: Serverless mode (pay per request)
- **NFR-6.3**: Functions: Consumption plan (pay per execution)
- **NFR-6.4**: SignalR: Free tier (upgrade only if >20 concurrent connections)
- **NFR-6.5**: Document Intelligence: $1.50 per 1000 pages
- **NFR-6.6**: No blob storage costs (PDFs not persisted)

---

## 4. Azure Services Required

### 4.1 Core Services

#### Service 1: Azure Functions
- **SKU**: Consumption Plan (Y1)
- **Runtime**: .NET 8 Isolated
- **OS**: Linux
- **Functions**:
  - Timer Trigger: Check for new filings (CRON: `0 */5 * * * *`)
  - Queue Trigger: Process filing PDFs
  - HTTP Trigger: REST API endpoints
  - SignalR Output Binding: Send notifications
- **Configuration**:
  ```json
  {
    "version": "2.0",
    "extensions": {
      "queues": {
        "maxDequeueCount": 5,
        "visibilityTimeout": "00:05:00",
        "batchSize": 1
      }
    },
    "functionTimeout": "00:05:00"
  }
  ```

#### Service 2: Azure Storage Account
- **SKU**: Standard LRS (Locally Redundant Storage)
- **Purpose**:
  - Queue storage (message passing)
  - Function state management
- **Queues**:
  - `filings-to-process`: Main processing queue
  - `filings-to-process-poison`: Dead-letter for failed messages
- **Lifecycle**: Queue messages auto-delete after 7 days
- **Cost**: ~$1/month

#### Service 3: Azure AI Document Intelligence
- **SKU**: S0 (Standard)
- **Region**: Same as Function App (for latency optimization)
- **Model**: Prebuilt Layout Model
- **Features Used**:
  - Table extraction
  - Key-value pair detection
  - Text recognition
- **Pricing**: $1.50 per 1000 pages
- **Expected Cost**: ~$1.50/month (1000 filings/month)

#### Service 4: Azure Cosmos DB
- **API**: NoSQL (native)
- **Mode**: Serverless
- **Region**: Single region (primary)
- **Consistency**: Session (default)
- **Database**: `CongressTrades`
  - **Container**: `transactions`
    - Partition key: `/filingId`
    - Indexing: Automatic
  - **Container**: `processed-filings`
    - Partition key: `/id`
    - Indexing: Automatic
- **Expected Cost**: ~$25/month (1000 filings, minimal queries)

#### Service 5: Azure SignalR Service
- **SKU**: Free F1
- **Limits**: 20 concurrent connections, 20K messages/day
- **Mode**: Serverless
- **Features**:
  - Automatic scaling
  - Integrated with Functions via output binding
  - WebSocket + Server-Sent Events support
- **Cost**: Free (upgrade to Standard if >20 concurrent users)

---

### 4.2 Supporting Services

#### Service 6: Application Insights
- **SKU**: Pay-as-you-go
- **Free Tier**: 5GB/month (sufficient for this workload)
- **Purpose**:
  - Distributed tracing across functions
  - Custom metrics and KPIs
  - Exception tracking with stack traces
  - Query logs via Kusto (KQL)
- **Retention**: 90 days
- **Cost**: Free tier covers expected usage

#### Service 7: Log Analytics Workspace
- **SKU**: Pay-as-you-go
- **Purpose**: Backend for Application Insights
- **Retention**: 30 days (configurable)
- **Cost**: Included in Application Insights free tier

#### Service 8: Azure Key Vault (Optional but Recommended)
- **SKU**: Standard
- **Purpose**: Store secrets securely
- **Secrets**:
  - Document Intelligence API key
  - Cosmos DB connection string (if not using Managed Identity)
  - SignalR connection string
- **Access**: Via Managed Identity from Functions
- **Cost**: $0.03 per 10K operations (~$0.10/month)

#### Service 9: Azure Monitor
- **Purpose**: Alerting and dashboards
- **Alert Rules**:
  - Dead-letter queue has messages → Email/SMS notification
  - Function failure rate >10% → Alert DevOps team
  - Document Intelligence errors → Investigate API issues
  - High response times (>2s) → Performance degradation
- **Cost**: First 10 alert rules free

---

### 4.3 Service Dependency Matrix

| Service | Depends On | Purpose |
|---------|-----------|---------|
| **Timer Function** | Storage Queue, Cosmos DB | Check website, queue new filings |
| **Queue Function** | Document Intelligence, Cosmos DB, SignalR | Process PDFs, store data, notify |
| **HTTP Function** | Cosmos DB | Serve REST API queries |
| **Storage Queue** | - | Message passing between functions |
| **Document Intelligence** | - | Extract data from PDFs |
| **Cosmos DB** | - | Persist transaction data, track processed filings |
| **SignalR Service** | - | Real-time client notifications |
| **Application Insights** | Log Analytics | Centralized logging and monitoring |
| **Key Vault** | - | Secure secret storage |

---

## 5. Data Models

### 5.1 Filing Message (Queue Payload)
```csharp
/// <summary>
/// Message queued when new filing is detected
/// </summary>
public class FilingMessage
{
    /// <summary>
    /// Unique filing identifier from PDF URL
    /// Example: "20250123456"
    /// </summary>
    public string FilingId { get; set; }

    /// <summary>
    /// Full URL to PDF on government website
    /// Example: "https://disclosures-clerk.house.gov/public_disc/ptr-pdfs/2025/20250123456.pdf"
    /// </summary>
    public string PdfUrl { get; set; }

    /// <summary>
    /// Politician name from website listing
    /// Example: "Doe, John"
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Office/District from website listing
    /// Example: "CA12"
    /// </summary>
    public string Office { get; set; }

    /// <summary>
    /// Timestamp when message was queued
    /// </summary>
    public DateTime QueuedAt { get; set; }
}
```

### 5.2 Transaction Document (Cosmos DB)
```csharp
/// <summary>
/// Complete filing data stored in Cosmos DB 'transactions' container
/// </summary>
public class TransactionDocument
{
    /// <summary>
    /// Cosmos DB document ID (auto-generated)
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; }

    /// <summary>
    /// Filing ID (partition key for efficient queries)
    /// </summary>
    public string FilingId { get; set; }

    /// <summary>
    /// URL to original PDF (reference only, file not stored)
    /// </summary>
    public string PdfUrl { get; set; }

    /// <summary>
    /// Timestamp when processing completed
    /// </summary>
    public DateTime ProcessedAt { get; set; }

    /// <summary>
    /// Filing metadata extracted from PDF
    /// </summary>
    public FilingInformation Filing_Information { get; set; }

    /// <summary>
    /// List of stock transactions from PDF
    /// </summary>
    public List<Transaction> Transactions { get; set; }
}

/// <summary>
/// Filing metadata
/// </summary>
public class FilingInformation
{
    public string Name { get; set; }
    public string Status { get; set; }
    public string State_District { get; set; }
}

/// <summary>
/// Individual stock transaction
/// </summary>
public class Transaction
{
    public string ID_Owner { get; set; }
    public string Asset { get; set; }
    public string Transaction_Type { get; set; }
    public string Date { get; set; }
    public string Amount { get; set; }
}
```

### 5.3 Processed Filing Tracker (Cosmos DB)
```csharp
/// <summary>
/// Lightweight document to track which filings have been processed
/// Stored in 'processed-filings' container for deduplication
/// </summary>
public class ProcessedFiling
{
    /// <summary>
    /// Filing ID (serves as both document ID and partition key)
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; }

    /// <summary>
    /// URL reference to original PDF
    /// </summary>
    public string PdfUrl { get; set; }

    /// <summary>
    /// Politician name for quick reference
    /// </summary>
    public string Politician { get; set; }

    /// <summary>
    /// When processing completed
    /// </summary>
    public DateTime ProcessedAt { get; set; }

    /// <summary>
    /// Processing status: "completed" or "failed"
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Error message if status is "failed"
    /// </summary>
    public string ErrorMessage { get; set; }
}
```

---

## 6. Cost Estimate

### 6.1 Monthly Cost Breakdown (Normal Operations)

| Service | SKU/Tier | Estimated Usage | Cost |
|---------|----------|-----------------|------|
| **Azure Functions** | Consumption (Y1) | ~15K executions, 100GB-s | $10 |
| **Storage Account** | Standard LRS | Queue operations + state | $1 |
| **Document Intelligence** | S0 | 1000 pages/month | $1.50 |
| **Cosmos DB** | Serverless | 1M RU/month, 1GB storage | $25 |
| **SignalR Service** | Free F1 | <20 concurrent, <20K msg/day | $0 |
| **Application Insights** | Pay-as-you-go | <5GB/month | $0 (free tier) |
| **Key Vault** | Standard | ~1000 operations/month | $0.10 |
| **Azure Monitor** | - | 5 alert rules | $0 (free tier) |
| **Total** | | | **~$37.60/month** |

### 6.2 Cost Scaling

| Scenario | Filings/Month | Estimated Cost |
|----------|---------------|----------------|
| Low volume | 100 | $28 |
| Normal | 1,000 | $38 |
| High volume | 5,000 | $75 |
| Very high | 10,000 | $125 |

**Note**: Primary cost driver is Cosmos DB. Consider reserved capacity if sustained high volume.

---

## 7. Success Criteria

### 7.1 Acceptance Criteria

- ✅ System detects new filings within 5 minutes of publication
- ✅ PDF processing success rate >95%
- ✅ API response time <500ms (p95)
- ✅ Zero data loss (all new filings processed)
- ✅ Real-time notifications delivered within 10 seconds
- ✅ Monthly cost <$50
- ✅ 99.5% uptime
- ✅ Automated CI/CD with zero-downtime deployments
- ✅ Unit test coverage >80%
- ✅ All secrets managed via Key Vault (no hardcoded credentials)

### 7.2 Key Performance Indicators (KPIs)

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Filing Detection Latency** | <5 min | Time from publication to detection |
| **Processing Time** | <10s | Timer trigger to storage completion |
| **API Response Time** | <500ms | p95 latency for `/api/latest` |
| **Error Rate** | <5% | Failed processings / total filings |
| **Availability** | 99.5% | Uptime percentage |
| **Cost Efficiency** | <$0.05/filing | Total monthly cost / filings processed |

---

## 8. Constraints and Assumptions

### 8.1 Constraints
- Must use Azure cloud platform (no multi-cloud)
- Must comply with government data retention policies
- Budget limit: $50/month
- Free tier limits for SignalR (20 concurrent connections)

### 8.2 Assumptions
- House.gov website structure remains stable
- PDF format is consistent (standard PTR form layout)
- Filings arrive at steady rate (not thousands simultaneously)
- Internet connectivity is reliable
- Azure services maintain published SLAs

### 8.3 Out of Scope
- Historical data migration (start fresh)
- User authentication/authorization (public data)
- Custom PDF form templates beyond standard PTR
- Mobile app development
- Data analytics/visualization dashboard
- Committee membership verification (noted as TODO)

---

## 9. Risks and Mitigations

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| **Website structure change** | High | Medium | Monitor for parsing errors, send alerts |
| **PDF format change** | High | Low | Train custom Document Intelligence model |
| **Azure service outage** | High | Low | Automatic retries, dead-letter queue |
| **Cost overrun** | Medium | Low | Budget alerts, serverless auto-scaling limits |
| **Rate limiting (Document Intelligence)** | Medium | Low | Queue-based throttling (1 at a time) |
| **Data quality issues** | Medium | Medium | Validation layer with logging |

---

## 10. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-10-14 | Initial | Complete requirements specification |
