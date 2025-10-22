using CongressStockTrades.Core.Models;
using CongressStockTrades.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CongressStockTrades.Infrastructure.Services;

/// <summary>
/// Service for enriching stock data using Financial Modeling Prep API.
/// Provides sector and industry information with 30-day caching.
/// </summary>
public class StockDataService : IStockDataService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<StockDataService> _logger;
    private readonly string _apiKey;
    private const string BaseUrl = "https://financialmodelingprep.com/api/v3";

    public StockDataService(
        HttpClient httpClient,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<StockDataService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;

        _apiKey = Environment.GetEnvironmentVariable("FinancialModelingPrep__ApiKey")
            ?? configuration["FinancialModelingPrep__ApiKey"]
            ?? throw new InvalidOperationException("FinancialModelingPrep__ApiKey configuration is missing");
    }

    /// <summary>
    /// Fetches stock information from FMP API with 30-day caching.
    /// </summary>
    public async Task<StockInfo?> GetStockInfoAsync(string ticker, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
            return null;

        // Check cache first (30-day TTL)
        var cacheKey = $"stock_info_{ticker.ToUpperInvariant()}";
        if (_cache.TryGetValue<StockInfo>(cacheKey, out var cachedInfo))
        {
            _logger.LogDebug("Cache hit for ticker {Ticker}", ticker);
            return cachedInfo;
        }

        try
        {
            _logger.LogInformation("Fetching stock info for ticker {Ticker} from FMP API", ticker);

            var url = $"{BaseUrl}/profile/{ticker}?apikey={_apiKey}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("FMP API returned {StatusCode} for ticker {Ticker}", response.StatusCode, ticker);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var profiles = JsonSerializer.Deserialize<List<FmpCompanyProfile>>(json);

            if (profiles == null || profiles.Count == 0)
            {
                _logger.LogInformation("No profile data found for ticker {Ticker}", ticker);
                return null;
            }

            var profile = profiles[0];

            // Validate required fields
            if (string.IsNullOrWhiteSpace(profile.CompanyName) ||
                string.IsNullOrWhiteSpace(profile.Sector) ||
                string.IsNullOrWhiteSpace(profile.Industry))
            {
                _logger.LogWarning("Incomplete profile data for ticker {Ticker}", ticker);
                return null;
            }

            var stockInfo = new StockInfo
            {
                Ticker = ticker.ToUpperInvariant(),
                CompanyName = profile.CompanyName,
                Sector = profile.Sector,
                Industry = profile.Industry
            };

            // Cache for 30 days
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
            };
            _cache.Set(cacheKey, stockInfo, cacheOptions);

            _logger.LogInformation("Successfully enriched ticker {Ticker}: {Company} - {Sector} / {Industry}",
                ticker, profile.CompanyName, profile.Sector, profile.Industry);

            return stockInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching stock info for ticker {Ticker}", ticker);
            return null;
        }
    }

    #region FMP API Response Models

    private class FmpCompanyProfile
    {
        [JsonPropertyName("symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("companyName")]
        public string? CompanyName { get; set; }

        [JsonPropertyName("sector")]
        public string? Sector { get; set; }

        [JsonPropertyName("industry")]
        public string? Industry { get; set; }
    }

    #endregion
}
