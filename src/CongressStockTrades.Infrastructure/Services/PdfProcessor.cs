using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using CongressStockTrades.Core.Models;
using CongressStockTrades.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CongressStockTrades.Infrastructure.Services;

public class PdfProcessor : IPdfProcessor
{
    private readonly HttpClient _httpClient;
    private readonly DocumentAnalysisClient _docIntelClient;
    private readonly ICongressApiService _congressApi;
    private readonly IAssetParser _assetParser;
    private readonly IStockDataService _stockDataService;
    private readonly ILogger<PdfProcessor> _logger;
    private readonly string _modelId;

    public PdfProcessor(
        HttpClient httpClient,
        DocumentAnalysisClient docIntelClient,
        ICongressApiService congressApi,
        IAssetParser assetParser,
        IStockDataService stockDataService,
        IConfiguration configuration,
        ILogger<PdfProcessor> logger)
    {
        _httpClient = httpClient;
        _docIntelClient = docIntelClient;
        _congressApi = congressApi;
        _assetParser = assetParser;
        _stockDataService = stockDataService;
        _logger = logger;

        _modelId = Environment.GetEnvironmentVariable("DocumentIntelligence__ModelId")
            ?? configuration["DocumentIntelligence__ModelId"]
            ?? "ptr-extractor-v1"; // Default fallback

        _logger.LogInformation("PdfProcessor initialized with ModelId: {ModelId}", _modelId);
    }

    public async Task<TransactionDocument> ProcessPdfAsync(
        string pdfUrl,
        string filingId,
        string expectedName,
        string expectedOffice,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing PDF for filing {FilingId}", filingId);

        // Download PDF to memory as a seekable stream (required by Document Intelligence)
        using var response = await _httpClient.GetAsync(pdfUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var memoryStream = new MemoryStream();
        await response.Content.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0; // Reset to beginning for Document Intelligence

        // Analyze with Document Intelligence using custom trained model
        var operation = await _docIntelClient.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            _modelId,
            memoryStream,
            cancellationToken: cancellationToken);

        var result = operation.Value;

        // Extract filing information
        var filingInfo = ExtractFilingInformation(result);

        // Lookup committee memberships from Congress.gov API
        await EnrichWithCommitteeInfoAsync(filingInfo, cancellationToken);

        // Extract transactions from tables
        var transactions = ExtractTransactions(result);

        _logger.LogInformation("Extracted {Count} transactions from PDF", transactions.Count);

        // Enrich transactions with asset type and stock data
        await EnrichTransactionsWithStockDataAsync(transactions, cancellationToken);

        // Validate that we got meaningful data from the model
        if (transactions.Count == 0)
        {
            _logger.LogWarning("PDF {FilingId} produced zero transactions - may be incompatible format", filingId);
            throw new InvalidOperationException($"PDF format incompatible with model - no transactions extracted from filing {filingId}");
        }

        if (filingInfo.Name == "Unknown" || filingInfo.State_District == "Unknown")
        {
            _logger.LogWarning("PDF {FilingId} missing critical filer information - may be incompatible format", filingId);
        }

        // Extract additional fields from document
        var document = result.Documents.FirstOrDefault();

        // Extract filing date
        string? filingDate = null;
        if (document?.Fields.TryGetValue("filing_date", out var filingDateField) == true)
        {
            filingDate = filingDateField.Content?.Trim();
        }

        // Extract IPO status (true if ipo_true is selected, false otherwise)
        bool isIPO = false;
        if (document?.Fields.TryGetValue("ipo_true", out var ipoTrueField) == true &&
            ipoTrueField.FieldType == DocumentFieldType.SelectionMark)
        {
            isIPO = ipoTrueField.Value.AsSelectionMarkState() == DocumentSelectionMarkState.Selected;
        }

        // Extract investment vehicles
        List<string>? investmentVehicles = null;
        if (document?.Fields.TryGetValue("investment_vehicle", out var investmentVehicleField) == true &&
            investmentVehicleField.FieldType == DocumentFieldType.List)
        {
            investmentVehicles = new List<string>();
            var vehicleList = investmentVehicleField.Value.AsList();
            foreach (var vehicle in vehicleList)
            {
                if (vehicle.FieldType == DocumentFieldType.Dictionary)
                {
                    var vehicleDict = vehicle.Value.AsDictionary();
                    if (vehicleDict.TryGetValue("type", out var typeField))
                    {
                        investmentVehicles.Add(typeField.Content?.Trim() ?? "");
                    }
                }
            }
        }

        return new TransactionDocument
        {
            Id = filingId, // Use FilingId as the Cosmos DB document id
            FilingId = filingId,
            PdfUrl = pdfUrl,
            Filing_Date = filingDate,
            IsIPO = isIPO,
            Filing_Information = filingInfo,
            Investment_Vehicles = investmentVehicles,
            Transactions = transactions
        };
    }

    private FilingInformation ExtractFilingInformation(AnalyzeResult result)
    {
        // Extract from custom model fields
        var document = result.Documents.FirstOrDefault();

        if (document == null)
        {
            _logger.LogWarning("No document found in analysis result");
            return new FilingInformation
            {
                Name = "Unknown",
                Status = "Filed",
                State_District = "Unknown"
            };
        }

        // Extract filer information from nested dictionary structure
        // The custom model returns filer_information with nested COLUMN1 fields
        var name = "Unknown";
        var status = "Filed";
        var district = "Unknown";

        if (document.Fields.TryGetValue("filer_information", out var filerField) &&
            filerField.FieldType == DocumentFieldType.Dictionary)
        {
            var filerFields = filerField.Value.AsDictionary();

            // Extract Name from nested COLUMN1
            if (filerFields.TryGetValue("Name", out var nameField) &&
                nameField.FieldType == DocumentFieldType.Dictionary)
            {
                var nameFields = nameField.Value.AsDictionary();
                if (nameFields.TryGetValue("COLUMN1", out var column1Field))
                    name = column1Field.Content ?? "Unknown";
            }

            // Extract Status from nested COLUMN1
            if (filerFields.TryGetValue("Status", out var statusField) &&
                statusField.FieldType == DocumentFieldType.Dictionary)
            {
                var statusFields = statusField.Value.AsDictionary();
                if (statusFields.TryGetValue("COLUMN1", out var column1Field))
                    status = column1Field.Content ?? "Filed";
            }

            // Extract State-district from nested COLUMN1
            // Note: Field name uses hyphen "State-district", not underscore
            if (filerFields.TryGetValue("State-district", out var districtField) &&
                districtField.FieldType == DocumentFieldType.Dictionary)
            {
                var districtFields = districtField.Value.AsDictionary();
                if (districtFields.TryGetValue("COLUMN1", out var column1Field))
                    district = column1Field.Content ?? "Unknown";
            }
        }

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
        var document = result.Documents.FirstOrDefault();

        // Use lowercase "transactions" - that's what the trained model returns
        if (document == null || !document.Fields.TryGetValue("transactions", out var transactionsField))
        {
            _logger.LogWarning("No transactions field found in document");
            return transactions;
        }

        // If transactions is a list/array field
        if (transactionsField.FieldType == DocumentFieldType.List)
        {
            var transactionsList = transactionsField.Value.AsList();

            foreach (var item in transactionsList)
            {
                if (item.FieldType == DocumentFieldType.Dictionary)
                {
                    var fields = item.Value.AsDictionary();

                    transactions.Add(new Transaction
                    {
                        Asset = fields.TryGetValue("Asset", out var asset) ? asset.Content?.Trim() ?? "" : "",
                        Transaction_Type = fields.TryGetValue("Transaction Type", out var type) ? type.Content?.Trim() ?? "" : "",
                        Date = fields.TryGetValue("Date", out var date) ? date.Content?.Trim() ?? "" : "",
                        Amount = fields.TryGetValue("Amount", out var amount) ? amount.Content?.Trim() ?? "" : ""
                    });
                }
            }
        }

        _logger.LogInformation("Extracted {Count} transactions from custom model", transactions.Count);
        return transactions;
    }

    private async Task EnrichWithCommitteeInfoAsync(FilingInformation filingInfo, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filingInfo.Name) || filingInfo.Name == "Unknown")
            {
                _logger.LogDebug("Skipping committee lookup - no valid member name");
                return;
            }

            _logger.LogInformation("Looking up committees for {Name}", filingInfo.Name);

            // Get bioguide ID from name
            var bioguideId = await _congressApi.GetBioguideIdByNameAsync(filingInfo.Name, cancellationToken);

            if (string.IsNullOrEmpty(bioguideId))
            {
                _logger.LogWarning("Could not find bioguide ID for {Name}", filingInfo.Name);
                return;
            }

            // Get committee memberships
            var committees = await _congressApi.GetMemberCommitteesAsync(bioguideId, null, cancellationToken);

            if (committees.Any())
            {
                filingInfo.Committees = committees;
                _logger.LogInformation("Found {Count} committee memberships for {Name}",
                    committees.Count, filingInfo.Name);
            }
            else
            {
                _logger.LogInformation("No committees found for {Name}", filingInfo.Name);
            }
        }
        catch (Exception ex)
        {
            // Don't fail the whole processing if committee lookup fails
            _logger.LogError(ex, "Error enriching with committee info for {Name}", filingInfo.Name);
        }
    }

    private async Task EnrichTransactionsWithStockDataAsync(List<Transaction> transactions, CancellationToken cancellationToken)
    {
        var enrichedCount = 0;
        var stockCount = 0;

        try
        {
            foreach (var transaction in transactions)
            {
                // Extract asset type from bracket tag
                transaction.AssetType = _assetParser.ExtractAssetType(transaction.Asset);

                // Only enrich stocks
                if (transaction.AssetType == "Stock")
                {
                    stockCount++;

                    // Extract ticker symbol
                    var ticker = _assetParser.ExtractTicker(transaction.Asset);

                    if (!string.IsNullOrEmpty(ticker))
                    {
                        // Fetch stock info from FMP API (with caching)
                        transaction.StockInfo = await _stockDataService.GetStockInfoAsync(ticker, cancellationToken);

                        if (transaction.StockInfo != null)
                        {
                            enrichedCount++;
                        }
                    }
                    else
                    {
                        _logger.LogDebug("No ticker found for stock asset: {Asset}", transaction.Asset);
                    }
                }
            }

            _logger.LogInformation(
                "Stock enrichment: {EnrichedCount}/{StockCount} stocks enriched ({TotalCount} total transactions, {AssetTypes})",
                enrichedCount, stockCount, transactions.Count,
                string.Join(", ", transactions.GroupBy(t => t.AssetType).Select(g => $"{g.Key}: {g.Count()}")));
        }
        catch (Exception ex)
        {
            // Don't fail the whole processing if stock enrichment fails
            _logger.LogError(ex, "Error enriching transactions with stock data");
        }
    }
}
