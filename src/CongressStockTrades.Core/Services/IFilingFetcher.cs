using CongressStockTrades.Core.Models;

namespace CongressStockTrades.Core.Services;

/// <summary>
/// Service for fetching and parsing congressional stock trading filings from the House disclosure website.
/// Scrapes HTML content and extracts PTR (Periodic Transaction Report) filing metadata.
/// </summary>
public interface IFilingFetcher
{
    /// <summary>
    /// Fetches the most recent PTR filing for a given year.
    /// </summary>
    /// <param name="year">The year to fetch filings for (e.g., 2025)</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>The most recent filing, or null if no filings found</returns>
    /// <exception cref="System.Net.Http.HttpRequestException">Thrown when the HTTP request to House.gov fails</exception>
    Task<Filing?> GetLatestFilingAsync(int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches all PTR filings for a given year, sorted by filing ID descending (most recent first).
    /// Filters out non-PTR filings and parses HTML table rows into structured Filing objects.
    /// </summary>
    /// <param name="year">The year to fetch filings for (e.g., 2025)</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>List of filings sorted by ID descending, or empty list if none found</returns>
    /// <exception cref="System.Net.Http.HttpRequestException">Thrown when the HTTP request to House.gov fails</exception>
    Task<List<Filing>> GetFilingsAsync(int year, CancellationToken cancellationToken = default);
}
