namespace CongressStockTrades.Core.Models;

/// <summary>
/// Message payload for Storage Queue
/// </summary>
public class FilingMessage
{
    public required string FilingId { get; set; }
    public required string PdfUrl { get; set; }
    public required string Name { get; set; }
    public required string Office { get; set; }
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
}
