using CongressStockTrades.Core.Models;

namespace CongressStockTrades.Core.Services;

/// <summary>
/// Service for enriching stock data with sector and industry information.
/// Uses Financial Modeling Prep API to fetch company profiles.
/// </summary>
public interface IStockDataService
{
    /// <summary>
    /// Fetches stock information (company name, sector, industry) for a given ticker.
    /// Results are cached for 30 days to minimize API calls.
    /// </summary>
    /// <param name="ticker">Stock ticker symbol (e.g., "AAPL", "BRK.B")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stock info or null if not found / API error</returns>
    Task<StockInfo?> GetStockInfoAsync(string ticker, CancellationToken cancellationToken = default);
}
