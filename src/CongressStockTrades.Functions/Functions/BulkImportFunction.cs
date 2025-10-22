using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using CongressStockTrades.Core.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace CongressStockTrades.Functions.Functions;

/// <summary>
/// HTTP-triggered function for bulk importing local PDFs.
/// Allows processing of historical PDFs that have already been downloaded.
/// </summary>
public class BulkImportFunction
{
    private readonly QueueClient _queueClient;
    private readonly ILogger<BulkImportFunction> _logger;

    public BulkImportFunction(
        IConfiguration configuration,
        ILogger<BulkImportFunction> logger)
    {
        var connectionString = configuration["AzureWebJobsStorage"]
            ?? throw new InvalidOperationException("AzureWebJobsStorage not configured");

        _queueClient = new QueueClient(connectionString, "filings-to-process",
            new QueueClientOptions
            {
                MessageEncoding = QueueMessageEncoding.Base64
            });
        _logger = logger;
    }

    /// <summary>
    /// POST /api/bulk-import
    /// Body: { "filings": [{ "filingId": "20033318", "pdfUrl": "https://...", "name": "...", "office": "..." }] }
    /// </summary>
    [Function("BulkImport")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "bulk-import")] HttpRequestData req)
    {
        _logger.LogInformation("Bulk import request received");

        try
        {
            // Parse request body
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var bulkRequest = JsonSerializer.Deserialize<BulkImportRequest>(requestBody);

            if (bulkRequest?.Filings == null || !bulkRequest.Filings.Any())
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("No filings provided");
                return badRequest;
            }

            var queuedCount = 0;
            var errors = new List<string>();

            // Queue each filing for processing
            foreach (var filing in bulkRequest.Filings)
            {
                try
                {
                    var message = new FilingMessage
                    {
                        FilingId = filing.FilingId,
                        PdfUrl = filing.PdfUrl,
                        Name = filing.Name ?? "Bulk Import",
                        Office = filing.Office ?? "Unknown",
                        QueuedAt = DateTime.UtcNow
                    };

                    var messageJson = JsonSerializer.Serialize(message);
                    await _queueClient.SendMessageAsync(messageJson);
                    queuedCount++;

                    _logger.LogInformation("Queued filing {FilingId} for processing", filing.FilingId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to queue filing {FilingId}", filing.FilingId);
                    errors.Add($"{filing.FilingId}: {ex.Message}");
                }
            }

            // Return results
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                queued = queuedCount,
                total = bulkRequest.Filings.Count,
                errors = errors
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk import failed");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Bulk import failed: {ex.Message}");
            return errorResponse;
        }
    }
}

public class BulkImportRequest
{
    public List<BulkFiling> Filings { get; set; } = new();
}

public class BulkFiling
{
    public required string FilingId { get; set; }
    public required string PdfUrl { get; set; }
    public string? Name { get; set; }
    public string? Office { get; set; }
}
