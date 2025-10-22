using CongressStockTrades.Core.Models;
using CongressStockTrades.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace CongressStockTrades.Infrastructure.Services;

/// <summary>
/// Implementation of ICongressApiService for accessing Congress.gov API.
/// Includes in-memory caching to respect rate limits (5,000 requests/hour).
/// </summary>
public class CongressApiService : ICongressApiService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CongressApiService> _logger;
    private readonly string _apiKey;
    private readonly int _currentCongress;
    private const string BaseUrl = "https://api.congress.gov/v3";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    public CongressApiService(
        HttpClient httpClient,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<CongressApiService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;

        _apiKey = Environment.GetEnvironmentVariable("CongressApi__ApiKey")
            ?? configuration["CongressApi:ApiKey"]
            ?? throw new InvalidOperationException("CongressApi:ApiKey is required");

        // Calculate current Congress (118th = 2023-2024, 119th = 2025-2026, etc.)
        var currentYear = DateTime.UtcNow.Year;
        _currentCongress = ((currentYear - 1789) / 2) + 1;

        _logger.LogInformation("CongressApiService initialized for {Congress}th Congress", _currentCongress);
    }

    public async Task<string?> GetBioguideIdByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var cacheKey = $"bioguide_{name.ToLowerInvariant()}";

        if (_cache.TryGetValue<string>(cacheKey, out var cachedId))
        {
            _logger.LogDebug("Cache hit for bioguide ID: {Name}", name);
            return cachedId;
        }

        try
        {
            _logger.LogInformation("Searching for member: {Name}", name);

            // Get current members and search by name
            var url = $"{BaseUrl}/member?currentMember=true&limit=250&api_key={_apiKey}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch members: {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<MemberListResponse>(json);

            if (result?.Members == null)
            {
                _logger.LogWarning("No members found in API response");
                return null;
            }

            // Search for matching name (Congress uses "Last, First" format)
            // Try exact match first
            var member = result.Members.FirstOrDefault(m =>
                m.Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true);

            // If no exact match, try fuzzy matching (handles "Hon. Thomas Suozzi" vs "Suozzi, Thomas R.")
            if (member == null)
            {
                var normalizedSearchName = NormalizeName(name);
                member = result.Members.FirstOrDefault(m =>
                {
                    if (m.Name == null) return false;
                    var normalizedMemberName = NormalizeName(m.Name);
                    return normalizedMemberName.Contains(normalizedSearchName, StringComparison.OrdinalIgnoreCase) ||
                           normalizedSearchName.Contains(normalizedMemberName, StringComparison.OrdinalIgnoreCase);
                });

                if (member != null)
                {
                    _logger.LogInformation("Found member via fuzzy match: '{SearchName}' matched to '{MemberName}'",
                        name, member.Name);
                }
            }

            if (member?.BioguideId != null)
            {
                _cache.Set(cacheKey, member.BioguideId, CacheDuration);
                _logger.LogInformation("Found bioguide ID {BioguideId} for {Name}", member.BioguideId, name);
                return member.BioguideId;
            }

            _logger.LogWarning("No bioguide ID found for member: {Name}", name);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching bioguide ID for {Name}", name);
            return null;
        }
    }

    public async Task<List<CommitteeMembership>> GetMemberCommitteesAsync(
        string bioguideId,
        int? congress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bioguideId))
            return new List<CommitteeMembership>();

        var cacheKey = $"committees_{bioguideId}";

        if (_cache.TryGetValue<List<CommitteeMembership>>(cacheKey, out var cached))
        {
            _logger.LogDebug("Cache hit for committees: {BioguideId}", bioguideId);
            return cached;
        }

        try
        {
            _logger.LogInformation("Fetching committees for member {BioguideId} from unitedstates/congress-legislators", bioguideId);

            // Use the unitedstates/congress-legislators data source (more reliable than Congress.gov API)
            var url = "https://theunitedstates.io/congress-legislators/committee-membership-current.yaml";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch committee data: {StatusCode}", response.StatusCode);
                return new List<CommitteeMembership>();
            }

            var yaml = await response.Content.ReadAsStringAsync(cancellationToken);
            var committees = ParseCommitteeMemberships(yaml, bioguideId);

            _cache.Set(cacheKey, committees, CacheDuration);
            _logger.LogInformation("Found {Count} committees for {BioguideId}", committees.Count, bioguideId);
            return committees;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching committees for {BioguideId}", bioguideId);
            return new List<CommitteeMembership>();
        }
    }

    /// <summary>
    /// Normalizes a member name for fuzzy matching.
    /// Removes titles, punctuation, and extra whitespace.
    /// </summary>
    private string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        // Remove common titles
        var normalized = name
            .Replace("Hon.", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Rep.", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Sen.", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Dr.", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Mr.", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Mrs.", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Ms.", "", StringComparison.OrdinalIgnoreCase);

        // Remove punctuation and extra spaces
        normalized = new string(normalized.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());
        normalized = string.Join(" ", normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

        return normalized.Trim();
    }

    /// <summary>
    /// Parses YAML committee membership data and extracts committees for a specific bioguide ID.
    /// </summary>
    private List<CommitteeMembership> ParseCommitteeMemberships(string yaml, string bioguideId)
    {
        var committees = new List<CommitteeMembership>();

        try
        {
            var deserializer = new DeserializerBuilder().Build();
            var yamlObject = deserializer.Deserialize<Dictionary<string, CommitteeYaml>>(yaml);

            if (yamlObject == null)
                return committees;

            foreach (var kvp in yamlObject)
            {
                var committeeCode = kvp.Key;
                var committee = kvp.Value;

                if (committee.Members == null)
                    continue;

                var member = committee.Members.FirstOrDefault(m =>
                    m.Bioguide?.Equals(bioguideId, StringComparison.OrdinalIgnoreCase) == true);

                if (member != null)
                {
                    // Determine chamber from committee code
                    string chamber;
                    if (committeeCode.StartsWith("HS"))
                        chamber = "house";
                    else if (committeeCode.StartsWith("SS"))
                        chamber = "senate";
                    else if (committeeCode.StartsWith("JS"))
                        chamber = "joint";
                    else
                        chamber = "unknown";

                    committees.Add(new CommitteeMembership
                    {
                        CommitteeCode = committeeCode,
                        CommitteeName = committee.Name ?? committeeCode,
                        Chamber = chamber,
                        Role = member.Party, // In the YAML, this is "majority" or "minority"
                        Rank = member.Rank
                    });
                }
            }

            return committees;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing YAML committee data for bioguide {BioguideId}", bioguideId);
            return committees;
        }
    }

    #region API Response Models

    private class MemberListResponse
    {
        [JsonPropertyName("members")]
        public List<MemberSummary>? Members { get; set; }
    }

    private class MemberSummary
    {
        [JsonPropertyName("bioguideId")]
        public string? BioguideId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private class CommitteeListResponse
    {
        [JsonPropertyName("committees")]
        public List<CommitteeSummary>? Committees { get; set; }
    }

    private class CommitteeSummary
    {
        [JsonPropertyName("systemCode")]
        public string? SystemCode { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private class CommitteeDetailResponse
    {
        [JsonPropertyName("committee")]
        public CommitteeDetail? Committee { get; set; }
    }

    private class CommitteeDetail
    {
        [JsonPropertyName("members")]
        public List<CommitteeMember>? Members { get; set; }
    }

    private class CommitteeMember
    {
        [JsonPropertyName("bioguideId")]
        public string? BioguideId { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("rank")]
        public int? Rank { get; set; }
    }

    // YAML models for unitedstates/congress-legislators data
    private class CommitteeYaml
    {
        public string? Name { get; set; }
        public List<MemberYaml>? Members { get; set; }
    }

    private class MemberYaml
    {
        public string? Name { get; set; }
        public string? Party { get; set; }
        public int? Rank { get; set; }
        public string? Bioguide { get; set; }
    }

    #endregion
}
