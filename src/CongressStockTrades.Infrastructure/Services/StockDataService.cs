using CongressStockTrades.Core.Models;
using CongressStockTrades.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CongressStockTrades.Infrastructure.Services;

/// <summary>
/// Service for enriching stock data using Alpha Vantage API (primary) with Yahoo Finance fallback.
/// Provides sector and industry information with 30-day caching.
/// </summary>
public class StockDataService : IStockDataService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<StockDataService> _logger;
    private readonly string? _alphaVantageApiKey;

    public StockDataService(
        HttpClient httpClient,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<StockDataService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;

        // Alpha Vantage API key is optional - will fall back to Yahoo Finance if not provided
        _alphaVantageApiKey = Environment.GetEnvironmentVariable("AlphaVantage__ApiKey")
            ?? configuration["AlphaVantage__ApiKey"];

        if (string.IsNullOrWhiteSpace(_alphaVantageApiKey))
        {
            _logger.LogWarning("AlphaVantage API key not configured - will use Yahoo Finance (unofficial API)");
        }
    }

    /// <summary>
    /// Fetches stock information from Alpha Vantage (primary) or Yahoo Finance (fallback) with 30-day caching.
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

        StockInfo? stockInfo = null;

        // Try Alpha Vantage first if API key is available
        if (!string.IsNullOrWhiteSpace(_alphaVantageApiKey))
        {
            stockInfo = await GetStockInfoFromAlphaVantageAsync(ticker, cancellationToken);
        }

        // Fallback to Yahoo Finance if Alpha Vantage failed or not configured
        if (stockInfo == null)
        {
            stockInfo = await GetStockInfoFromYahooFinanceAsync(ticker, cancellationToken);
        }

        // Cache successful results for 30 days
        if (stockInfo != null)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
            };
            _cache.Set(cacheKey, stockInfo, cacheOptions);

            _logger.LogInformation("Successfully enriched ticker {Ticker}: {Company} - {Sector} / {Industry}",
                ticker, stockInfo.CompanyName, stockInfo.Sector, stockInfo.Industry);
        }

        return stockInfo;
    }

    private async Task<StockInfo?> GetStockInfoFromAlphaVantageAsync(string ticker, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Fetching stock info for ticker {Ticker} from Alpha Vantage", ticker);

            var url = $"https://www.alphavantage.co/query?function=OVERVIEW&symbol={ticker}&apikey={_alphaVantageApiKey}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Alpha Vantage API returned {StatusCode} for ticker {Ticker}", response.StatusCode, ticker);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var overview = JsonSerializer.Deserialize<AlphaVantageOverview>(json);

            if (overview == null || string.IsNullOrWhiteSpace(overview.Symbol))
            {
                _logger.LogInformation("No overview data found for ticker {Ticker} from Alpha Vantage", ticker);
                return null;
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(overview.Name) ||
                string.IsNullOrWhiteSpace(overview.Sector) ||
                string.IsNullOrWhiteSpace(overview.Industry))
            {
                _logger.LogWarning("Incomplete overview data for ticker {Ticker} from Alpha Vantage", ticker);
                return null;
            }

            return new StockInfo
            {
                Ticker = ticker.ToUpperInvariant(),
                CompanyName = overview.Name,
                Sector = overview.Sector,
                Industry = overview.Industry
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching stock info from Alpha Vantage for ticker {Ticker}", ticker);
            return null;
        }
    }

    private async Task<StockInfo?> GetStockInfoFromYahooFinanceAsync(string ticker, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Fetching stock info for ticker {Ticker} from Yahoo Finance", ticker);

            var url = $"https://query2.finance.yahoo.com/v10/finance/quoteSummary/{ticker}?modules=assetProfile";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Yahoo Finance API returned {StatusCode} for ticker {Ticker}", response.StatusCode, ticker);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var yahooResponse = JsonSerializer.Deserialize<YahooFinanceResponse>(json);

            var assetProfile = yahooResponse?.QuoteSummary?.Result?.FirstOrDefault()?.AssetProfile;
            if (assetProfile == null)
            {
                _logger.LogInformation("No asset profile found for ticker {Ticker} from Yahoo Finance", ticker);
                return null;
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(assetProfile.LongName) ||
                string.IsNullOrWhiteSpace(assetProfile.Sector) ||
                string.IsNullOrWhiteSpace(assetProfile.Industry))
            {
                _logger.LogWarning("Incomplete asset profile for ticker {Ticker} from Yahoo Finance", ticker);
                return null;
            }

            return new StockInfo
            {
                Ticker = ticker.ToUpperInvariant(),
                CompanyName = assetProfile.LongName,
                Sector = assetProfile.Sector,
                Industry = assetProfile.Industry
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching stock info from Yahoo Finance for ticker {Ticker}", ticker);
            return null;
        }
    }

    /// <summary>
    /// Searches for a ticker symbol by company name using Alpha Vantage (primary) or Yahoo Finance (fallback).
    /// </summary>
    public async Task<string?> SearchTickerByNameAsync(string companyName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            return null;

        string? ticker = null;

        // Try Alpha Vantage first if API key is available
        if (!string.IsNullOrWhiteSpace(_alphaVantageApiKey))
        {
            ticker = await SearchTickerFromAlphaVantageAsync(companyName, cancellationToken);
        }

        // Fallback to Yahoo Finance if Alpha Vantage failed or not configured
        if (ticker == null)
        {
            ticker = await SearchTickerFromYahooFinanceAsync(companyName, cancellationToken);
        }

        return ticker;
    }

    private async Task<string?> SearchTickerFromAlphaVantageAsync(string companyName, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Searching for ticker with company name: {CompanyName} via Alpha Vantage", companyName);

            var url = $"https://www.alphavantage.co/query?function=SYMBOL_SEARCH&keywords={Uri.EscapeDataString(companyName)}&apikey={_alphaVantageApiKey}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Alpha Vantage search API returned {StatusCode} for query {CompanyName}", response.StatusCode, companyName);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var searchResponse = JsonSerializer.Deserialize<AlphaVantageSearchResponse>(json);

            if (searchResponse?.BestMatches == null || searchResponse.BestMatches.Count == 0)
            {
                _logger.LogInformation("No search results found for company name: {CompanyName} from Alpha Vantage", companyName);
                return null;
            }

            // Take the first result (best match)
            var bestMatch = searchResponse.BestMatches[0];

            if (string.IsNullOrWhiteSpace(bestMatch.Symbol))
            {
                _logger.LogWarning("Search result has no symbol for company name: {CompanyName}", companyName);
                return null;
            }

            _logger.LogInformation("Found ticker {Ticker} for company name {CompanyName} via Alpha Vantage",
                bestMatch.Symbol, companyName);

            return bestMatch.Symbol;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for ticker via Alpha Vantage for company name: {CompanyName}", companyName);
            return null;
        }
    }

    private async Task<string?> SearchTickerFromYahooFinanceAsync(string companyName, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Searching for ticker with company name: {CompanyName} via Yahoo Finance", companyName);

            var url = $"https://query2.finance.yahoo.com/v1/finance/search?q={Uri.EscapeDataString(companyName)}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Yahoo Finance search API returned {StatusCode} for query {CompanyName}", response.StatusCode, companyName);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var searchResponse = JsonSerializer.Deserialize<YahooSearchResponse>(json);

            var quotes = searchResponse?.Quotes?.Where(q => q.QuoteType == "EQUITY").ToList();
            if (quotes == null || quotes.Count == 0)
            {
                _logger.LogInformation("No equity search results found for company name: {CompanyName} from Yahoo Finance", companyName);
                return null;
            }

            // Take the first equity result (best match)
            var bestMatch = quotes[0];

            if (string.IsNullOrWhiteSpace(bestMatch.Symbol))
            {
                _logger.LogWarning("Search result has no symbol for company name: {CompanyName}", companyName);
                return null;
            }

            _logger.LogInformation("Found ticker {Ticker} for company name {CompanyName} via Yahoo Finance",
                bestMatch.Symbol, companyName);

            return bestMatch.Symbol;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for ticker via Yahoo Finance for company name: {CompanyName}", companyName);
            return null;
        }
    }

    #region API Response Models

    // Alpha Vantage Models
    private class AlphaVantageOverview
    {
        [JsonPropertyName("Symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("Name")]
        public string? Name { get; set; }

        [JsonPropertyName("Sector")]
        public string? Sector { get; set; }

        [JsonPropertyName("Industry")]
        public string? Industry { get; set; }
    }

    private class AlphaVantageSearchResponse
    {
        [JsonPropertyName("bestMatches")]
        public List<AlphaVantageSearchMatch>? BestMatches { get; set; }
    }

    private class AlphaVantageSearchMatch
    {
        [JsonPropertyName("1. symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("2. name")]
        public string? Name { get; set; }
    }

    // Yahoo Finance Models
    private class YahooFinanceResponse
    {
        [JsonPropertyName("quoteSummary")]
        public YahooQuoteSummary? QuoteSummary { get; set; }
    }

    private class YahooQuoteSummary
    {
        [JsonPropertyName("result")]
        public List<YahooResult>? Result { get; set; }
    }

    private class YahooResult
    {
        [JsonPropertyName("assetProfile")]
        public YahooAssetProfile? AssetProfile { get; set; }
    }

    private class YahooAssetProfile
    {
        [JsonPropertyName("longName")]
        public string? LongName { get; set; }

        [JsonPropertyName("sector")]
        public string? Sector { get; set; }

        [JsonPropertyName("industry")]
        public string? Industry { get; set; }
    }

    private class YahooSearchResponse
    {
        [JsonPropertyName("quotes")]
        public List<YahooSearchQuote>? Quotes { get; set; }
    }

    private class YahooSearchQuote
    {
        [JsonPropertyName("symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("shortname")]
        public string? ShortName { get; set; }

        [JsonPropertyName("quoteType")]
        public string? QuoteType { get; set; }

        [JsonPropertyName("exchange")]
        public string? Exchange { get; set; }
    }

    #endregion
}
