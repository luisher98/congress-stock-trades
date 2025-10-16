using System.Text.Json.Serialization;

namespace CongressStockTrades.Core.Models;

/// <summary>
/// Lightweight tracking document stored in Cosmos DB 'processed-filings' container.
/// Used to prevent reprocessing the same filing multiple times (deduplication).
/// The filing ID serves as both the document ID and partition key.
/// </summary>
public class ProcessedFiling
{
    /// <summary>
    /// Filing identifier (serves as both document ID and partition key).
    /// Example: "20250123456"
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// URL reference to the original PDF document.
    /// </summary>
    public required string PdfUrl { get; set; }

    /// <summary>
    /// Politician name for quick reference without querying transactions container.
    /// </summary>
    public required string Politician { get; set; }

    /// <summary>
    /// Timestamp when processing completed (success or failure).
    /// </summary>
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Processing status: "completed" or "failed".
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// Error message if Status is "failed", otherwise null.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
