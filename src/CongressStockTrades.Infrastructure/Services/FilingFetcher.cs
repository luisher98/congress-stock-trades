using CongressStockTrades.Core.Models;
using CongressStockTrades.Core.Services;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace CongressStockTrades.Infrastructure.Services;

/// <summary>
/// Implementation of IFilingFetcher that scrapes the House disclosure website for PTR filing metadata.
/// Uses HTTP POST requests and HtmlAgilityPack to parse the HTML response tables.
/// </summary>
public class FilingFetcher : IFilingFetcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FilingFetcher> _logger;
    private const string BaseUrl = "https://disclosures-clerk.house.gov";

    /// <summary>
    /// Initializes a new instance of the FilingFetcher class.
    /// </summary>
    /// <param name="httpClient">HTTP client for making requests to House.gov</param>
    /// <param name="logger">Logger for diagnostic output</param>
    public FilingFetcher(HttpClient httpClient, ILogger<FilingFetcher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Filing?> GetLatestFilingAsync(int year, CancellationToken cancellationToken = default)
    {
        var filings = await GetFilingsAsync(year, cancellationToken);
        return filings.FirstOrDefault();
    }

    public async Task<List<Filing>> GetFilingsAsync(int year, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching filings for year {Year}", year);

        var url = $"{BaseUrl}/FinancialDisclosure/ViewMemberSearchResult?filingYear={year}";
        var response = await _httpClient.PostAsync(url, null, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to fetch filings: {StatusCode}", response.StatusCode);
            return new List<Filing>();
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseHtml(html, year);
    }

    /// <summary>
    /// Parses HTML response to extract PTR filing metadata from table rows.
    /// Filters for PTR-type filings only and extracts filing ID from PDF URLs using regex.
    /// </summary>
    /// <param name="html">Raw HTML response from House.gov</param>
    /// <param name="year">Year for context (not used in parsing but kept for potential filtering)</param>
    /// <returns>List of parsed Filing objects sorted by ID descending</returns>
    private List<Filing> ParseHtml(string html, int year)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var rows = doc.DocumentNode.SelectNodes("//tbody//tr");
        if (rows == null)
        {
            _logger.LogWarning("No table rows found in HTML");
            return new List<Filing>();
        }

        var filings = new List<Filing>();

        foreach (var row in rows)
        {
            try
            {
                var nameElement = row.SelectSingleNode(".//td[@data-label='Name']//a");
                var officeElement = row.SelectSingleNode(".//td[@data-label='Office']");
                var filingYearElement = row.SelectSingleNode(".//td[@data-label='Filing Year']");
                var filingTypeElement = row.SelectSingleNode(".//td[@data-label='Filing']");

                // Skip if not PTR filing
                if (filingTypeElement == null || !filingTypeElement.InnerText.Contains("PTR"))
                    continue;

                if (nameElement == null || officeElement == null || filingYearElement == null)
                    continue;

                var href = nameElement.GetAttributeValue("href", string.Empty);
                if (string.IsNullOrEmpty(href) || !href.Contains("ptr-pdfs"))
                    continue;

                // Extract filing ID from URL
                var match = Regex.Match(href, @"/(\d+)\.pdf$");
                if (!match.Success)
                {
                    _logger.LogWarning("Could not extract filing ID from href: {Href}", href);
                    continue;
                }

                var filingId = match.Groups[1].Value;
                var name = nameElement.InnerText.Trim();
                var office = officeElement.InnerText.Trim();
                var filingYear = filingYearElement.InnerText.Trim();

                var cleanHref = href.StartsWith("/") ? href : "/" + href;
                var pdfUrl = new Uri(new Uri(BaseUrl), cleanHref).ToString();

                filings.Add(new Filing
                {
                    Id = filingId,
                    Name = name,
                    Office = office,
                    FilingYear = filingYear,
                    PdfUrl = pdfUrl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing filing row");
            }
        }

        // Sort by ID descending (most recent first)
        return filings.OrderByDescending(f => long.Parse(f.Id)).ToList();
    }
}
