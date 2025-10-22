using CongressStockTrades.Core.Models;

namespace CongressStockTrades.Core.Services;

/// <summary>
/// Repository interface for managing transaction documents and processed filing tracking in Cosmos DB.
/// Handles operations on both the 'transactions' and 'processed-filings' containers.
/// </summary>
public interface ITransactionRepository
{
    /// <summary>
    /// Stores a complete transaction document in the Cosmos DB 'transactions' container.
    /// Uses filing ID as the partition key for efficient querying.
    /// </summary>
    /// <param name="document">The transaction document to store</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>True if the document was stored, false if it already existed (409 Conflict)</returns>
    /// <exception cref="System.Exception">Thrown when Cosmos DB operation fails</exception>
    Task<bool> StoreTransactionAsync(TransactionDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a filing has already been processed by querying the 'processed-filings' container.
    /// Used for deduplication to avoid reprocessing the same filing multiple times.
    /// </summary>
    /// <param name="filingId">The filing identifier to check</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>True if the filing has been processed, false otherwise</returns>
    /// <exception cref="System.Exception">Thrown when Cosmos DB operation fails</exception>
    Task<bool> IsFilingProcessedAsync(string filingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a filing as processed by upserting a record in the 'processed-filings' container.
    /// This prevents reprocessing and tracks which filings have been handled.
    /// </summary>
    /// <param name="filingId">The filing identifier to mark as processed</param>
    /// <param name="pdfUrl">URL reference to the PDF</param>
    /// <param name="politician">Politician name for quick reference</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <exception cref="System.Exception">Thrown when Cosmos DB operation fails</exception>
    Task MarkAsProcessedAsync(string filingId, string pdfUrl, string politician, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most recently processed transaction document, ordered by processedAt timestamp descending.
    /// Used by the REST API to serve the latest filing data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>The most recent transaction document, or null if no documents exist</returns>
    /// <exception cref="System.Exception">Thrown when Cosmos DB operation fails</exception>
    Task<TransactionDocument?> GetLatestTransactionAsync(CancellationToken cancellationToken = default);
}
