using Azure.Storage.Queues;
using CongressStockTrades.Core.Models;
using CongressStockTrades.Core.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CongressStockTrades.Functions.Functions;

/// <summary>
/// Timer-triggered function that periodically checks for new congressional stock trading filings.
/// Runs every 5 minutes and queues any new filings for processing.
/// </summary>
public class CheckNewFilingsFunction
{
    private readonly IFilingFetcher _filingFetcher;
    private readonly ITransactionRepository _repository;
    private readonly INotificationService _notificationService;
    private readonly QueueClient _queueClient;
    private readonly ILogger<CheckNewFilingsFunction> _logger;

    /// <summary>
    /// Initializes a new instance of the CheckNewFilingsFunction class.
    /// </summary>
    public CheckNewFilingsFunction(
        IFilingFetcher filingFetcher,
        ITransactionRepository repository,
        INotificationService notificationService,
        IConfiguration configuration,
        ILogger<CheckNewFilingsFunction> logger)
    {
        _filingFetcher = filingFetcher;
        _repository = repository;
        _notificationService = notificationService;
        _logger = logger;

        var connectionString = configuration["AzureWebJobsStorage"]
            ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage")
            ?? throw new InvalidOperationException("AzureWebJobsStorage not configured");
        _queueClient = new QueueClient(connectionString, "filings-to-process");
    }

    /// <summary>
    /// Timer trigger that runs every 5 minutes to check for new filings.
    /// </summary>
    [Function("CheckNewFilings")]
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timerInfo)
    {
        _logger.LogInformation("Timer trigger fired at: {Time}", DateTime.UtcNow);

        try
        {
            var currentYear = DateTime.UtcNow.Year;
            var latestFiling = await _filingFetcher.GetLatestFilingAsync(currentYear);

            if (latestFiling == null)
            {
                _logger.LogInformation("No filings found for year {Year}", currentYear);
                return;
            }

            _logger.LogInformation("Latest filing found: {FilingId} - {Name}", latestFiling.Id, latestFiling.Name);

            // Check if already processed
            var isProcessed = await _repository.IsFilingProcessedAsync(latestFiling.Id);
            if (isProcessed)
            {
                _logger.LogInformation("Filing {FilingId} already processed, skipping", latestFiling.Id);
                await _notificationService.NotifyCheckingStatusAsync("No new filings found.");
                return;
            }

            // Queue for processing
            _logger.LogInformation("New filing detected: {FilingId} - {Name}", latestFiling.Id, latestFiling.Name);

            var message = new FilingMessage
            {
                FilingId = latestFiling.Id,
                PdfUrl = latestFiling.PdfUrl,
                Name = latestFiling.Name,
                Office = latestFiling.Office
            };

            var messageJson = JsonSerializer.Serialize(message);
            await _queueClient.SendMessageAsync(messageJson);

            _logger.LogInformation("Queued filing {FilingId} for processing", latestFiling.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for new filings");
            throw;
        }
    }
}
