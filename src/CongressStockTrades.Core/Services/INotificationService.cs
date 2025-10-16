using CongressStockTrades.Core.Models;

namespace CongressStockTrades.Core.Services;

/// <summary>
/// Service for broadcasting real-time notifications to connected clients via SignalR.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Broadcasts a new transaction filing to all connected clients.
    /// </summary>
    /// <param name="transaction">The transaction document to broadcast</param>
    /// <returns>Task representing the async operation</returns>
    Task BroadcastNewTransactionAsync(TransactionDocument transaction);

    /// <summary>
    /// Notifies clients that a filing check is in progress.
    /// </summary>
    /// <param name="message">Status message</param>
    /// <returns>Task representing the async operation</returns>
    Task NotifyCheckingStatusAsync(string message);

    /// <summary>
    /// Notifies clients of an error during processing.
    /// </summary>
    /// <param name="filingId">Filing identifier that failed</param>
    /// <param name="error">Error message</param>
    /// <returns>Task representing the async operation</returns>
    Task NotifyErrorAsync(string filingId, string error);
}
