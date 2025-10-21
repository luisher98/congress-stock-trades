using CongressStockTrades.Core.Models;
using CongressStockTrades.Core.Services;
using CongressStockTrades.Infrastructure.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CongressStockTrades.Functions.Functions;

/// <summary>
/// Queue-triggered function that processes filing PDFs and stores transaction data.
/// Downloads PDFs, extracts data using Document Intelligence, validates, and stores in Cosmos DB.
/// </summary>
public class ProcessFilingFunction
{
    private readonly IPdfProcessor _pdfProcessor;
    private readonly IDataValidator _validator;
    private readonly ITransactionRepository _repository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ProcessFilingFunction> _logger;

    /// <summary>
    /// Initializes a new instance of the ProcessFilingFunction class.
    /// </summary>
    public ProcessFilingFunction(
        IPdfProcessor pdfProcessor,
        IDataValidator validator,
        ITransactionRepository repository,
        INotificationService notificationService,
        ILogger<ProcessFilingFunction> logger)
    {
        _logger = logger;
        _pdfProcessor = pdfProcessor;
        _validator = validator;
        _repository = repository;
        _notificationService = notificationService;
    }

    /// <summary>
    /// Queue trigger that processes filing messages from the filings-to-process queue.
    /// Automatically retries up to 5 times on failure before moving to poison queue.
    /// </summary>
    [Function("ProcessFiling")]
    public async Task Run(
        [QueueTrigger("filings-to-process", Connection = "AzureWebJobsStorage")] string queueMessage)
    {
        _logger.LogInformation("Processing queue message: {Message}", queueMessage);

        FilingMessage? message = null;
        try
        {
            // Deserialize queue message
            message = JsonSerializer.Deserialize<FilingMessage>(queueMessage);
            if (message == null)
            {
                _logger.LogError("Failed to deserialize queue message");
                throw new InvalidOperationException("Invalid queue message format");
            }

            _logger.LogInformation(
                "Processing filing {FilingId} for {Name} ({Office})",
                message.FilingId,
                message.Name,
                message.Office);

            // Check if already processed (deduplication)
            var isProcessed = await _repository.IsFilingProcessedAsync(message.FilingId);
            if (isProcessed)
            {
                _logger.LogWarning("Filing {FilingId} already processed, skipping", message.FilingId);
                return;
            }

            // Process PDF
            var transactionDocument = await _pdfProcessor.ProcessPdfAsync(
                message.PdfUrl,
                message.FilingId,
                message.Name,
                message.Office);

            // Validate extracted data
            _validator.Validate(transactionDocument, message.Name, message.Office);

            // Store in Cosmos DB
            await _repository.StoreTransactionAsync(transactionDocument);
            await _repository.MarkAsProcessedAsync(
                message.FilingId,
                message.PdfUrl,
                message.Name);

            _logger.LogInformation(
                "Successfully processed filing {FilingId} with {Count} transactions",
                message.FilingId,
                transactionDocument.Transactions.Count);

            // Broadcast to connected clients via SignalR
            await _notificationService.BroadcastNewTransactionAsync(transactionDocument);
        }
        catch (ValidationException ex)
        {
            _logger.LogError(ex, "Validation failed for filing {FilingId}", message?.FilingId);

            // Notify clients of error
            if (message != null)
            {
                await _notificationService.NotifyErrorAsync(message.FilingId, ex.Message);
            }

            throw; // Will retry and eventually move to poison queue
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing filing {FilingId}", message?.FilingId);

            // Notify clients of error
            if (message != null)
            {
                await _notificationService.NotifyErrorAsync(message.FilingId, ex.Message);
            }

            throw; // Will retry and eventually move to poison queue
        }
    }
}
