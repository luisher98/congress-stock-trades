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
            ?.Value?.Content ?? "Unknown";

        var status = kvPairs
            .FirstOrDefault(kv => kv.Key.Content.Contains("Status", StringComparison.OrdinalIgnoreCase))
            ?.Value?.Content ?? "Filed";

        var district = kvPairs
            .FirstOrDefault(kv => kv.Key.Content.Contains("District", StringComparison.OrdinalIgnoreCase))
            ?.Value?.Content ?? "Unknown";

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
