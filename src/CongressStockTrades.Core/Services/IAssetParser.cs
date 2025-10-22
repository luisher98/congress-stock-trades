namespace CongressStockTrades.Core.Services;

/// <summary>
/// Service for parsing asset strings from congressional stock trading PDFs.
/// Extracts ticker symbols and classifies asset types based on bracket tags.
/// </summary>
public interface IAssetParser
{
    /// <summary>
    /// Extracts the ticker symbol from an asset string.
    /// Looks for symbols within parentheses, e.g., "Apple Inc (AAPL)" → "AAPL"
    /// </summary>
    /// <param name="asset">Full asset string from PDF</param>
    /// <returns>Ticker symbol or null if not found</returns>
    string? ExtractTicker(string asset);

    /// <summary>
    /// Determines the asset type based on bracket tags in the asset string.
    /// Bracket tag mappings:
    ///   [ST] → "Stock"
    ///   [GS] → "Bond" (Government Security)
    ///   [CB] → "Bond" (Corporate Bond)
    ///   [CR] → "Crypto"
    ///   [MF] → "Fund" (Mutual Fund)
    ///   [OP] → "Option"
    ///   (none) → "Unknown"
    /// </summary>
    /// <param name="asset">Full asset string from PDF</param>
    /// <returns>Asset type classification</returns>
    string ExtractAssetType(string asset);
}
