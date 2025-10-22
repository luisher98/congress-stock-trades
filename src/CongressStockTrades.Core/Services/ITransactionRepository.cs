using CongressStockTrades.Core.Models;

namespace CongressStockTrades.Core.Services;

/// <summary>
/// Repository interface for managing transaction documents in Cosmos DB.
/// Handles operations on the 'transactions' container.
/// </summary>
public interface ITransactionRepository
{
    /// <summary>
    /// Stores a complete transaction document in the Cosmos DB 'transactions' container.
    /// Uses filing ID as the partition key for efficient querying and deduplication.
    /// </summary>
    /// <param name="document">The transaction document to store</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>True if the document was stored, false if it already existed (409 Conflict)</returns>
    /// <exception cref="System.Exception">Thrown when Cosmos DB operation fails</exception>
    Task<bool> StoreTransactionAsync(TransactionDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most recently processed transaction document, ordered by processedAt timestamp descending.
    /// Used by the REST API to serve the latest filing data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>The most recent transaction document, or null if no documents exist</returns>
    /// <exception cref="System.Exception">Thrown when Cosmos DB operation fails</exception>
    Task<TransactionDocument?> GetLatestTransactionAsync(CancellationToken cancellationToken = default);
}
