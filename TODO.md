# Congress Stock Trades - TODO List

**Last Updated**: 2025-10-22
**Current Status**: Core functionality complete, pending production deployment

---

## ✅ Completed (This Session)

### Core Fixes & Enhancements
- [x] Fixed queue message encoding (Base64 vs None) - poison queue issue resolved
- [x] Fixed Document Intelligence field extraction for custom model `ptr-extractor-v1`
  - [x] Lowercase field names (`transactions` not `Transactions`)
  - [x] Nested `filer_information` dictionary structure
  - [x] Field name with hyphen (`State-district` not `State_District`)
  - [x] Transaction field with space (`Transaction Type` not `Transaction_Type`)
- [x] Fixed Cosmos DB document storage issues
  - [x] Added `required` keyword to `Id` and `FilingId` properties
  - [x] Added Newtonsoft.Json serialization attributes (SDK v3 compatibility)
  - [x] Fixed partition key camelCase (`filingId` not `FilingId`)
- [x] Re-enabled SignalR notifications after core fixes
- [x] Changed timer interval from 30s → 5 minutes
- [x] Improved DataValidator name regex to handle titles anywhere in name
- [x] Added deduplication checks to prevent reprocessing
- [x] Fixed test files (added `Id` property, removed `ID_Owner`)

### New Features Added
- [x] Added missing Document Intelligence fields to data model:
  - [x] `Filing_Date` - Date from PDF
  - [x] `IsIPO` - Boolean for IPO status
  - [x] `Investment_Vehicles` - Array of investment vehicles
  - [x] Removed unnecessary `ID_Owner` field from Transaction
- [x] Added error handling for incompatible PDF formats
- [x] Created PdfClassifier service for cost optimization (not yet integrated)
- [x] Created BulkImportFunction for processing historical PDFs
- [x] Created Python bulk_import.py script for batch processing

### Documentation
- [x] Created test-signalr.html for SignalR testing
- [x] Created BulkImportFunction.cs for historical PDF processing
- [x] Created PdfClassifier.cs for PDF format validation (cost savings)

---

## 🚧 In Progress

### Testing & Validation
- [ ] **Test new fields extraction** (Filing_Date, IsIPO, Investment_Vehicles)
  - Need to restart function and process a new filing
  - Verify all fields appear in Cosmos DB
  - Check SignalR broadcasts include new fields

- [ ] **Remove debug logging**
  - `TransactionRepository.cs:42-44` - Newtonsoft serialization logging
  - Clean up excessive logs from development

---

## 📋 TODO - Immediate (Pre-Deployment)

### Core Functionality
- [ ] **Integrate PdfClassifier** (optional but recommended for cost savings)
  - [ ] Wire into ProcessFilingFunction before expensive extraction
  - [ ] Add configuration flag to enable/disable classification
  - [ ] Test with valid and invalid PDFs
  - [ ] Update cost estimates in README

- [ ] **Update API Response Format**
  - [ ] Update README.md API documentation to remove `ID_Owner`
  - [ ] Update API documentation to include new fields (Filing_Date, IsIPO, Investment_Vehicles)
  - [ ] Test `/api/latest` endpoint response format

- [ ] **Test Bulk Import**
  - [ ] Test BulkImportFunction with sample historical PDFs
  - [ ] Verify deduplication works correctly
  - [ ] Test rate limiting for cost control
  - [ ] Document bulk import process in README

### Configuration
- [ ] **Add missing Azure configuration**
  - [ ] Add `DocumentIntelligence__ModelId` to Azure Function App settings
  - [ ] Verify all environment variables are documented in README
  - [ ] Add `DocumentIntelligence__ClassificationModelId` (optional)

### Testing
- [ ] **End-to-End Testing**
  - [ ] Test complete flow: detection → queue → extraction → validation → storage → notification
  - [ ] Test error scenarios (validation failure, Cosmos DB errors, incompatible PDFs)
  - [ ] Test SignalR notifications reach clients
  - [ ] Verify deduplication prevents reprocessing

- [ ] **Update Unit Tests**
  - [ ] Add tests for new fields (Filing_Date, IsIPO, Investment_Vehicles)
  - [ ] Add tests for PdfClassifier
  - [ ] Add tests for BulkImportFunction
  - [ ] Add tests for incompatible PDF handling
  - [ ] Verify >80% code coverage

### Documentation
- [ ] **Update README.md**
  - [ ] Update API response examples (remove ID_Owner, add new fields)
  - [ ] Add bulk import documentation
  - [ ] Add PdfClassifier documentation
  - [ ] Update cost estimates if classifier is integrated
  - [ ] Update project status to "Production Ready"

- [ ] **Update Architecture Documentation**
  - [ ] Document new fields in data model
  - [ ] Document classification flow (if integrated)
  - [ ] Document bulk import architecture
  - [ ] Update sequence diagrams

---

## 📋 TODO - Production Deployment

### Pre-Deployment Checklist
- [ ] **Security Review**
  - [ ] Verify no secrets in code or config files
  - [ ] Review Key Vault integration
  - [ ] Review authentication levels (Function, Anonymous)
  - [ ] Enable HTTPS-only for all endpoints

- [ ] **Performance Testing**
  - [ ] Load test with multiple concurrent filings
  - [ ] Test queue scaling behavior
  - [ ] Verify Cosmos DB RU consumption
  - [ ] Test SignalR connection limits

- [ ] **Monitoring Setup**
  - [ ] Configure Application Insights alerts
  - [ ] Set up KQL queries for key metrics
  - [ ] Configure log retention policies
  - [ ] Set up availability tests

### Deployment
- [ ] **Deploy Infrastructure**
  - [ ] Review Bicep templates
  - [ ] Deploy to Azure via GitHub Actions or `azd up`
  - [ ] Verify all resources created correctly
  - [ ] Configure custom domain (if applicable)

- [ ] **Deploy Application**
  - [ ] Deploy to staging slot
  - [ ] Run smoke tests
  - [ ] Swap to production
  - [ ] Monitor for errors

- [ ] **Post-Deployment Verification**
  - [ ] Verify timer function runs every 5 minutes
  - [ ] Test manual queue message
  - [ ] Test API endpoints
  - [ ] Test SignalR connections
  - [ ] Monitor costs for first 24 hours

---

## 🎯 TODO - Future Enhancements

### Features
- [ ] **Web Frontend**
  - [ ] Create React/Next.js dashboard
  - [ ] Real-time transaction feed with SignalR
  - [ ] Search and filter transactions
  - [ ] Politician portfolio views
  - [ ] Email/SMS notifications

- [ ] **Advanced Analytics**
  - [ ] Track portfolio changes over time
  - [ ] Aggregate trading patterns
  - [ ] Correlation with market movements
  - [ ] Insider trading alerts

- [ ] **Data Quality**
  - [ ] Train better Document Intelligence model with more samples
  - [ ] Add OCR confidence thresholds
  - [ ] Implement manual review queue for low-confidence extractions
  - [ ] Add data quality metrics dashboard

### Performance Optimization
- [ ] **Implement Classification Model**
  - [ ] Train custom classification model with valid/invalid samples
  - [ ] Integrate with bulk import
  - [ ] Measure cost savings

- [ ] **Caching Layer**
  - [ ] Add Redis cache for frequently accessed data
  - [ ] Cache API responses (5-minute TTL)
  - [ ] Reduce Cosmos DB reads

- [ ] **Database Optimization**
  - [ ] Review Cosmos DB indexing strategy
  - [ ] Implement composite indexes for common queries
  - [ ] Archive old filings to cheaper storage

### Scalability
- [ ] **Multi-Region Deployment**
  - [ ] Deploy to multiple Azure regions
  - [ ] Use Cosmos DB global distribution
  - [ ] Implement Traffic Manager

- [ ] **Event Grid Integration**
  - [ ] Publish filing events to Event Grid
  - [ ] Allow subscribers to react to new filings
  - [ ] Enable webhooks for external integrations

### Developer Experience
- [ ] **Local Development Improvements**
  - [ ] Docker Compose setup for all dependencies
  - [ ] Mock Document Intelligence responses
  - [ ] Seed data for testing

- [ ] **CI/CD Enhancements**
  - [ ] Add automated security scanning
  - [ ] Add performance regression tests
  - [ ] Implement blue-green deployment validation

---

## 🐛 Known Issues

### Minor Issues
- [ ] Multiple background Bash processes running (need cleanup)
  - See system reminders - 27+ function instances running
  - Kill old processes: `pkill -f "func start"`

### Technical Debt
- [ ] SignalR package version warning (NU1603)
  - CongressStockTrades.Functions depends on v1.9.0 but v1.10.0 resolved
  - Consider updating to v1.10.0 explicitly

---

## 📊 Cost Optimization Opportunities

### Current Costs
- Document Intelligence: ~$0.05 per PDF page (most expensive)
- Cosmos DB: ~$25/month (scales with data)
- Functions: ~$10/month (scales with executions)

### Optimization Ideas
1. **Classification Model** (High Impact - 90%+ savings on invalid PDFs)
   - Cost: $0.002 per PDF to classify vs $0.05 to extract
   - Status: Code created but not integrated

2. **Batch Processing** (Medium Impact)
   - Process multiple filings in single Document Intelligence call
   - Requires API support for multi-page documents

3. **Cosmos DB Reserved Capacity** (Low Impact - only if high volume)
   - Switch from serverless to provisioned throughput
   - Only beneficial at >10K RU/s

4. **Function Plan Optimization** (Low Impact)
   - Stay on Consumption Plan unless >1M executions/month
   - Consider Premium Plan only if cold starts are an issue

---

## 📚 Documentation Updates Needed

- [x] Create TODO.md (this file)
- [ ] Update README.md with latest changes
- [ ] Update API documentation
- [ ] Add bulk import guide
- [ ] Add troubleshooting section
- [ ] Add cost optimization guide
- [ ] Update architecture diagrams

---

## 🎓 Learning & Improvement

### Areas for Learning
- [ ] Azure Durable Functions for complex workflows
- [ ] Azure Cognitive Search for advanced querying
- [ ] Power BI for analytics dashboards
- [ ] GitHub Copilot for AI-assisted development

### Code Quality
- [ ] Review TODO comments in code
- [ ] Refactor long methods (>50 lines)
- [ ] Add XML documentation to all public APIs
- [ ] Improve error messages for better debugging

---

## 📞 Questions to Resolve

1. **Should we integrate the PdfClassifier now or after production deployment?**
   - Pro: Significant cost savings (90%+ on invalid PDFs)
   - Con: Adds complexity before first deployment
   - **Recommendation**: Add after production deployment is stable

2. **What should be the production timer interval?**
   - Currently: 5 minutes
   - Consider: 1 minute for faster detection (costs more)
   - **Recommendation**: Start with 5 minutes, monitor latency

3. **Should we implement rate limiting for bulk imports?**
   - Pro: Prevents cost spikes
   - Con: Slower processing
   - **Recommendation**: Yes - add configurable rate limit

4. **How long should we retain historical data in Cosmos DB?**
   - Current: Indefinite
   - Consider: Archive to Blob Storage after 1 year
   - **Recommendation**: Keep all data in Cosmos DB initially, monitor costs

---

## 🚀 Next Steps (Priority Order)

1. **Test new fields** - Restart function, process filing, verify new fields in Cosmos DB
2. **Remove debug logging** - Clean up Newtonsoft serialization logs
3. **Update README** - Document new fields and bulk import
4. **Deploy to production** - Use GitHub Actions or `azd up`
5. **Monitor costs** - Track first week of production usage
6. **Consider classifier integration** - If invalid PDFs are common

---

**Notes:**
- This TODO list is a living document - update as work progresses
- Mark items complete with `[x]` and add completion date
- Add new items as they arise
- Review weekly and reprioritize as needed
