using System.Text.Json.Serialization;

namespace CongressStockTrades.Core.Models;

/// <summary>
/// Lightweight document to track processed filings
/// </summary>
public class ProcessedFiling
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    public required string PdfUrl { get; set; }
    public required string Politician { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public required string Status { get; set; } // "completed" | "failed"
    public string? ErrorMessage { get; set; }
}
