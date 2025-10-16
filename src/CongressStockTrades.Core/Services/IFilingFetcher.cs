using CongressStockTrades.Core.Models;

namespace CongressStockTrades.Core.Services;

public interface IFilingFetcher
{
    /// <summary>
    /// Fetches the latest filing for a given year
    /// </summary>
    Task<Filing?> GetLatestFilingAsync(int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches all filings for a given year
    /// </summary>
    Task<List<Filing>> GetFilingsAsync(int year, CancellationToken cancellationToken = default);
}
