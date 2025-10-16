namespace CongressStockTrades.Core.Models;

/// <summary>
/// Message payload queued to Azure Storage Queue when a new filing is detected.
/// Used to pass filing metadata from the timer function to the queue processing function.
/// </summary>
public class FilingMessage
{
    /// <summary>
    /// Unique filing identifier extracted from PDF URL.
    /// Example: "20250123456"
    /// </summary>
    public required string FilingId { get; set; }

    /// <summary>
    /// Full URL to the PDF document on the House disclosure website.
    /// Example: "https://disclosures-clerk.house.gov/public_disc/ptr-pdfs/2025/20250123456.pdf"
    /// </summary>
    public required string PdfUrl { get; set; }

    /// <summary>
    /// Politician's name from the website listing.
    /// Used for validation against PDF extracted data.
    /// Example: "Doe, John"
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Office/District information from the website listing.
    /// Used for validation against PDF extracted data.
    /// Example: "CA12"
    /// </summary>
    public required string Office { get; set; }

    /// <summary>
    /// Timestamp when the message was queued.
    /// Defaults to UTC now when the object is created.
    /// </summary>
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
}
