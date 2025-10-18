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

        _logger.LogInformation("PdfProcessor initializing...");

        _modelId = configuration["DocumentIntelligence__ModelId"]
            ?? Environment.GetEnvironmentVariable("DocumentIntelligence__ModelId")
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

        // Download PDF to memory
        using var pdfStream = await _httpClient.GetStreamAsync(pdfUrl, cancellationToken);

        // Analyze with Document Intelligence using custom trained model
        var operation = await _docIntelClient.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            _modelId,
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

        var name = document.Fields.TryGetValue("Name", out var nameField)
            ? nameField.Content ?? "Unknown"
            : "Unknown";

        var status = document.Fields.TryGetValue("Status", out var statusField)
            ? statusField.Content ?? "Filed"
            : "Filed";

        var district = document.Fields.TryGetValue("State_District", out var districtField)
            ? districtField.Content ?? "Unknown"
            : "Unknown";

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

        if (document == null || !document.Fields.TryGetValue("Transactions", out var transactionsField))
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
                        Transaction_Type = fields.TryGetValue("Transaction_Type", out var type) ? type.Content?.Trim() ?? "" : "",
                        Date = fields.TryGetValue("Date", out var date) ? date.Content?.Trim() ?? "" : "",
                        Amount = fields.TryGetValue("Amount", out var amount) ? amount.Content?.Trim() ?? "" : "",
                        ID_Owner = fields.TryGetValue("ID_Owner", out var owner) ? owner.Content?.Trim() ?? "" : ""
                    });
                }
            }
        }

        _logger.LogInformation("Extracted {Count} transactions from custom model", transactions.Count);
        return transactions;
    }
}
