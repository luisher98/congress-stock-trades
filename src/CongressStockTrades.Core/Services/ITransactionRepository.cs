using CongressStockTrades.Core.Models;

namespace CongressStockTrades.Core.Services;

public interface ITransactionRepository
{
    /// <summary>
    /// Stores a transaction document in Cosmos DB
    /// </summary>
    Task StoreTransactionAsync(TransactionDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a filing has already been processed
    /// </summary>
    Task<bool> IsFilingProcessedAsync(string filingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a filing as processed
    /// </summary>
    Task MarkAsProcessedAsync(string filingId, string pdfUrl, string politician, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most recent transaction document
    /// </summary>
    Task<TransactionDocument?> GetLatestTransactionAsync(CancellationToken cancellationToken = default);
}
