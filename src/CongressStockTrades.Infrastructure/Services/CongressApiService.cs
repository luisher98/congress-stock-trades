using CongressStockTrades.Core.Models;
using CongressStockTrades.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

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

        var congressNum = congress ?? _currentCongress;
        var cacheKey = $"committees_{bioguideId}_{congressNum}";

        if (_cache.TryGetValue<List<CommitteeMembership>>(cacheKey, out var cached))
        {
            _logger.LogDebug("Cache hit for committees: {BioguideId}", bioguideId);
            return cached;
        }

        try
        {
            _logger.LogInformation("Fetching committees for member {BioguideId} in {Congress}th Congress",
                bioguideId, congressNum);

            var committees = new List<CommitteeMembership>();

            // Fetch committees for both House and Senate (member might have served in both)
            foreach (var chamber in new[] { "house", "senate" })
            {
                var url = $"{BaseUrl}/committee/{congressNum}/{chamber}?limit=250&api_key={_apiKey}";
                var response = await _httpClient.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch {Chamber} committees: {StatusCode}",
                        chamber, response.StatusCode);
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<CommitteeListResponse>(json);

                if (result?.Committees == null)
                    continue;

                // For each committee, check if the member is on it
                foreach (var committee in result.Committees)
                {
                    if (string.IsNullOrEmpty(committee.SystemCode))
                        continue;

                    var membership = await GetMembershipInCommitteeAsync(
                        congressNum, chamber, committee.SystemCode, bioguideId, cancellationToken);

                    if (membership != null)
                    {
                        membership.CommitteeName = committee.Name ?? "Unknown Committee";
                        membership.Chamber = char.ToUpper(chamber[0]) + chamber.Substring(1);
                        committees.Add(membership);
                    }
                }
            }

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

    private async Task<CommitteeMembership?> GetMembershipInCommitteeAsync(
        int congress,
        string chamber,
        string committeeCode,
        string bioguideId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract the base committee code without chamber prefix
            var cleanCode = committeeCode.Replace("HS", "").Replace("SS", "").Replace("JS", "");

            var url = $"{BaseUrl}/committee/{congress}/{chamber}/{cleanCode}?api_key={_apiKey}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<CommitteeDetailResponse>(json);

            // Check if member is in the committee
            var member = result?.Committee?.Members?.FirstOrDefault(m =>
                m.BioguideId?.Equals(bioguideId, StringComparison.OrdinalIgnoreCase) == true);

            if (member == null)
                return null;

            return new CommitteeMembership
            {
                CommitteeCode = committeeCode,
                CommitteeName = "Unknown", // Will be set by caller
                Chamber = chamber,
                Role = member.Title,
                Rank = member.Rank
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking membership in {Committee}", committeeCode);
            return null;
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

    #endregion
}
