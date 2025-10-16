using CongressStockTrades.Core.Models;
using CongressStockTrades.Core.Services;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CongressStockTrades.Infrastructure.Services;

/// <summary>
/// Implementation of INotificationService using Azure SignalR Service.
/// Broadcasts real-time updates to connected web clients.
/// </summary>
public class SignalRNotificationService : INotificationService
{
    private readonly ServiceHubContext _hubContext;
    private readonly ILogger<SignalRNotificationService> _logger;
    private const string HubName = "transactions";

    /// <summary>
    /// Initializes a new instance of the SignalRNotificationService class.
    /// </summary>
    public SignalRNotificationService(
        ServiceHubContext hubContext,
        ILogger<SignalRNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Broadcasts a new transaction filing to all connected clients.
    /// Sends both alert status and full transaction data.
    /// </summary>
    public async Task BroadcastNewTransactionAsync(TransactionDocument transaction)
    {
        _logger.LogInformation(
            "Broadcasting new transaction {FilingId} to all clients",
            transaction.FilingId);

        try
        {
            // Send alert notification
            var payload = new
            {
                status = "alert",
                message = "New filing data found!",
                time = DateTime.UtcNow.ToString("o"),
                pdfUrl = transaction.PdfUrl,
                transaction = transaction
            };

            await _hubContext.Clients.All.SendCoreAsync("transactionUpdate", new object[] { payload });

            _logger.LogInformation("Successfully broadcast transaction {FilingId}", transaction.FilingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast transaction {FilingId}", transaction.FilingId);
            throw;
        }
    }

    /// <summary>
    /// Notifies clients that a filing check is in progress.
    /// </summary>
    public async Task NotifyCheckingStatusAsync(string message)
    {
        _logger.LogInformation("Notifying clients: {Message}", message);

        try
        {
            var payload = new
            {
                status = "finished checking",
                time = DateTime.UtcNow.ToString("o"),
                message = message
            };

            await _hubContext.Clients.All.SendCoreAsync("transactionUpdate", new object[] { payload });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send checking status notification");
            throw;
        }
    }

    /// <summary>
    /// Notifies clients of an error during processing.
    /// </summary>
    public async Task NotifyErrorAsync(string filingId, string error)
    {
        _logger.LogWarning("Notifying clients of error for filing {FilingId}: {Error}", filingId, error);

        try
        {
            var payload = new
            {
                status = "error",
                message = $"Error processing filing {filingId}: {error}",
                time = DateTime.UtcNow.ToString("o"),
                filingId = filingId
            };

            await _hubContext.Clients.All.SendCoreAsync("transactionUpdate", new object[] { payload });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send error notification");
            throw;
        }
    }
}
