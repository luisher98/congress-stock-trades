namespace CongressStockTrades.Core.Models;

/// <summary>
/// Stock enrichment data including sector and industry information.
/// Only populated for publicly traded stocks with valid ticker symbols.
/// </summary>
public class StockInfo
{
    /// <summary>
    /// Stock ticker symbol.
    /// Example: "AAPL", "BRK.B"
    /// </summary>
    public required string Ticker { get; set; }

    /// <summary>
    /// Full company name from the stock exchange.
    /// Example: "Apple Inc."
    /// </summary>
    public required string CompanyName { get; set; }

    /// <summary>
    /// Business sector.
    /// Example: "Technology", "Financial Services"
    /// </summary>
    public required string Sector { get; set; }

    /// <summary>
    /// Specific industry within the sector.
    /// Example: "Consumer Electronics", "Insurance - Diversified"
    /// </summary>
    public required string Industry { get; set; }
}
