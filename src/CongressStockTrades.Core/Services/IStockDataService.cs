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

    /// <summary>
    /// Searches for a stock ticker by company name.
    /// Uses FMP search API to find matching ticker symbols.
    /// </summary>
    /// <param name="companyName">Company name to search for (e.g., "Apple Inc")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Best matching ticker symbol or null if not found</returns>
    Task<string?> SearchTickerByNameAsync(string companyName, CancellationToken cancellationToken = default);
}
