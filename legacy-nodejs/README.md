# Legacy Node.js Implementation (Reference)

> **⚠️ This is the legacy Node.js/Express implementation for reference only.**
>
> **Active Development**: See root directory for C# Azure Functions implementation.

---

## Overview

This folder contains the original Node.js implementation of the Congress Stock Trading Tracker. It has been preserved for reference during the migration to C# Azure Functions.

### Original Tech Stack
- **Runtime**: Node.js
- **Framework**: Express.js
- **Database**: MongoDB
- **PDF Processing**: OpenAI Assistant API
- **Real-time**: Server-Sent Events (SSE)
- **Scheduling**: `setTimeout()`

---

## Directory Structure

```
legacy-nodejs/
├── package.json              # Node.js dependencies
├── package-lock.json         # Dependency lock file
├── transactions.json         # Sample transaction data
├── src/
│   ├── index.js             # Main entry point (Express server)
│   ├── components/
│   │   ├── dataValidation.js       # Data validation logic
│   │   └── pdfProcessing.js        # PDF download & processing
│   ├── db/
│   │   ├── db.js                   # MongoDB operations
│   │   └── dbConnection.js         # Database connection
│   ├── lib/
│   │   └── openai.js               # OpenAI API integration
│   ├── routes/
│   │   ├── transactionDataREST.js  # REST API endpoints
│   │   └── transactionDataSSE.js   # Server-Sent Events
│   ├── services/
│   │   └── checkLastTransaction.js # Filing monitoring service
│   └── utils/
│       ├── config.js               # Configuration management
│       ├── logger.js               # Logging utilities
│       ├── pdf.js                  # PDF utilities
│       └── transaction.js          # HTML parsing & fetching
└── README.md                # This file
```

---

## Key Components (Legacy)

### 1. Main Server ([src/index.js](src/index.js))
- Express.js application
- CORS enabled
- Routes: `/api/sse` and `/api/latest`
- Periodic filing monitoring via `setTimeout()`

### 2. Filing Monitor ([src/services/checkLastTransaction.js](src/services/checkLastTransaction.js))
- Uses **in-memory state** (`oldPDFUrl`) to track last processed filing
- ⚠️ **Problem**: State lost on restart
- Calls `getLatestTransaction()` to fetch from website
- Processes PDF if new filing detected

### 3. Transaction Fetcher ([src/utils/transaction.js](src/utils/transaction.js))
- HTTP POST to House.gov website
- Parses HTML using `jsdom`
- Extracts PTR filings from table
- Returns sorted list by filing ID

### 4. PDF Processing ([src/components/pdfProcessing.js](src/components/pdfProcessing.js))
- Downloads PDF using Puppeteer (originally) → now simple HTTP
- Sends to OpenAI Assistant API
- Validates extracted data
- Includes retry logic with exponential backoff

### 5. OpenAI Integration ([src/lib/openai.js](src/lib/openai.js))
- Uploads PDF to OpenAI
- Uses Assistant API to extract structured data
- Polls for completion
- Returns JSON with filing info + transactions

### 6. Data Validation ([src/components/dataValidation.js](src/components/dataValidation.js))
- Validates name matches between PDF and website
- Checks office/district consistency
- Ensures required fields present

### 7. MongoDB Storage ([src/db/db.js](src/db/db.js))
- Stores complete transaction documents
- Retrieves latest transaction for REST API
- Connection string from environment variables

### 8. Server-Sent Events ([src/routes/transactionDataSSE.js](src/routes/transactionDataSSE.js))
- Maintains set of connected clients
- Broadcasts new filings to all clients
- Handles client disconnections

### 9. REST API ([src/routes/transactionDataREST.js](src/routes/transactionDataREST.js))
- `GET /api/latest` - Returns most recent transaction
- Error handling with appropriate status codes

---

## Configuration (Legacy)

### Environment Variables (`.env` - not included)
```env
OPENAI_API_KEY=sk-...
OPENAI_ASSISTANT_ID=asst_...
PORT=5000
SERVER_NAME=http://localhost
SCRAPPER_FREQUENCY_MINUTES=60

MONGODB_USER=your_user
MONGODB_PASSWORD=your_password
MONGODB_URI=your_cluster.mongodb.net
MONGODB_DB=polititian-transactions
MONGODB_COLLECTION=transactions-test
```

---

## Running the Legacy Application (For Reference)

### Prerequisites
```bash
# Node.js 18+ required
node --version

# Install dependencies
npm install
```

### Start Server
```bash
# Development
npm run dev

# Start database operations
npm run db

# Process transactions
npm run transactions
```

### Access Endpoints
- **SSE**: http://localhost:5000/api/sse
- **REST API**: http://localhost:5000/api/latest

---

## Migration Notes

### What Changed in C# Version

| Aspect | Legacy (Node.js) | New (C# Azure Functions) |
|--------|------------------|--------------------------|
| **Architecture** | Monolithic Express server | Serverless functions (Timer, Queue, HTTP) |
| **State Management** | In-memory (`oldPDFUrl`) | Cosmos DB (`processed-filings` collection) |
| **PDF Processing** | OpenAI Assistant API | Azure AI Document Intelligence |
| **Scheduling** | `setTimeout()` | Azure Functions Timer Trigger (CRON) |
| **Message Queue** | None (direct processing) | Azure Storage Queue |
| **Real-time** | Express SSE | Azure SignalR Service |
| **Database** | MongoDB Atlas | Azure Cosmos DB |
| **Deployment** | Manual / Heroku | GitHub Actions → Azure Container Apps |
| **Monitoring** | Console logs | Application Insights + Azure Monitor |
| **Cost** | $25-50/month | $30-40/month |
| **Scalability** | Single instance | Auto-scaling serverless |
| **Reliability** | No retry mechanism | 5 retries + dead-letter queue |

---

## Known Issues (Legacy Implementation)

### 1. State Loss on Restart
**Problem**: `oldPDFUrl` stored in memory
- Server restart → reprocesses same filing
- No persistence between executions

**Solution in C#**: Cosmos DB `processed-filings` container

### 2. No Retry Queue
**Problem**: PDF processing failure loses filing
- No retry mechanism
- Manual intervention required

**Solution in C#**: Azure Storage Queue with automatic retries

### 3. Single Point of Failure
**Problem**: If Express server crashes, entire system down
- No auto-restart
- No redundancy

**Solution in C#**: Azure Functions auto-restart, multi-instance

### 4. Limited Observability
**Problem**: Console logs only
- Hard to debug production issues
- No metrics or alerts

**Solution in C#**: Application Insights with traces, metrics, alerts

### 5. OpenAI Cost & Reliability
**Problem**: $30/month for 1000 filings
- LLM can hallucinate data
- Rate limits possible

**Solution in C#**: Document Intelligence ($1.50/1000), purpose-built for forms

---

## Preserved Features

The following features from the legacy system are maintained in the C# version:

✅ **HTML Parsing**: Same logic (now in `FilingFetcher.cs`)
✅ **Data Validation**: Name/office comparison preserved
✅ **REST API**: Same endpoint structure (`/api/latest`)
✅ **Real-time Notifications**: SSE replaced with SignalR (better)
✅ **PDF URL Storage**: Still store reference, not file
✅ **Periodic Checking**: 5-minute interval maintained

---

## Reference Documentation

For the new C# implementation, see:
- [Requirements Document](../docs/REQUIREMENTS.md)
- [System Architecture](../docs/ARCHITECTURE.md)
- [Implementation Plan](../docs/IMPLEMENTATION_PLAN.md)
- [Deployment Guide](../docs/DEPLOYMENT.md)

---

## License

ISC License - Same as original project

## Original Author

Luis Hernández Martín (luisheratm@gmail.com)

---

**Status**: Archived for reference only (2025-10-14)
