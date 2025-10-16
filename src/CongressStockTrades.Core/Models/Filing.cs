namespace CongressStockTrades.Core.Models;

/// <summary>
/// Represents a filing metadata from the House website
/// </summary>
public class Filing
{
    /// <summary>
    /// Unique filing identifier extracted from PDF URL
    /// Example: "20250123456"
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Politician's name from website listing
    /// Example: "Doe, John"
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Office/District information
    /// Example: "CA12"
    /// </summary>
    public required string Office { get; set; }

    /// <summary>
    /// Year of filing
    /// </summary>
    public required string FilingYear { get; set; }

    /// <summary>
    /// Full URL to PDF document
    /// </summary>
    public required string PdfUrl { get; set; }
}
