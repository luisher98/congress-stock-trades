using CongressStockTrades.Core.Services;
using System.Text.RegularExpressions;

namespace CongressStockTrades.Infrastructure.Services;

/// <summary>
/// Parses asset strings from congressional stock trading PDFs.
/// Extracts ticker symbols and classifies asset types based on bracket tags.
/// </summary>
public class AssetParser : IAssetParser
{
    // Regex to extract ticker symbols from parentheses
    // Examples: "(AAPL)", "(BRK.B)", "(MSFT)"
    private static readonly Regex TickerRegex = new(@"\(([A-Z][A-Z0-9\.]{0,10})\)", RegexOptions.Compiled);

    // Regex to extract bracket tags
    // Examples: "[ST]", "[GS]", "[CR]"
    private static readonly Regex BracketTagRegex = new(@"\[([A-Z]+)\]", RegexOptions.Compiled);

    // Bracket tag to asset type mapping
    private static readonly Dictionary<string, string> BracketTagMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "ST", "Stock" },
        { "GS", "Bond" },      // Government Security
        { "CB", "Bond" },      // Corporate Bond
        { "CR", "Crypto" },
        { "MF", "Fund" },      // Mutual Fund
        { "OP", "Option" }
    };

    /// <summary>
    /// Extracts the ticker symbol from an asset string.
    /// </summary>
    public string? ExtractTicker(string asset)
    {
        if (string.IsNullOrWhiteSpace(asset))
            return null;

        var match = TickerRegex.Match(asset);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Determines the asset type based on bracket tags.
    /// </summary>
    public string ExtractAssetType(string asset)
    {
        if (string.IsNullOrWhiteSpace(asset))
            return "Unknown";

        var match = BracketTagRegex.Match(asset);
        if (!match.Success)
            return "Unknown";

        var tag = match.Groups[1].Value;
        return BracketTagMap.TryGetValue(tag, out var assetType) ? assetType : tag;
    }
}
