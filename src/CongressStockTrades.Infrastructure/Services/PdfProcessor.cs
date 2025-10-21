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
    private readonly ILogger<PdfProcessor> _logger;
    private readonly string _modelId;

    public PdfProcessor(
        HttpClient httpClient,
        DocumentAnalysisClient docIntelClient,
        IConfiguration configuration,
        ILogger<PdfProcessor> logger)
    {
        _httpClient = httpClient;
        _docIntelClient = docIntelClient;
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

        // Extract transactions from tables
        var transactions = ExtractTransactions(result);

        _logger.LogInformation("Extracted {Count} transactions from PDF", transactions.Count);

        return new TransactionDocument
        {
            Id = filingId, // Use FilingId as the Cosmos DB document id
            FilingId = filingId,
            PdfUrl = pdfUrl,
            Filing_Information = filingInfo,
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
                        Amount = fields.TryGetValue("Amount", out var amount) ? amount.Content?.Trim() ?? "" : "",
                        ID_Owner = fields.TryGetValue("Owner", out var owner) ? owner.Content?.Trim() ?? "" : ""
                    });
                }
            }
        }

        _logger.LogInformation("Extracted {Count} transactions from custom model", transactions.Count);
        return transactions;
    }
}
