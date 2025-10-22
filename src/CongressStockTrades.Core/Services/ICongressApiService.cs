using CongressStockTrades.Core.Models;

namespace CongressStockTrades.Core.Services;

/// <summary>
/// Service for interacting with the official Congress.gov API.
/// Provides access to member and committee information.
/// API documentation: https://api.congress.gov
/// </summary>
public interface ICongressApiService
{
    /// <summary>
    /// Searches for a congressional member by name and returns their bioguide ID.
    /// </summary>
    /// <param name="name">Member's full name (e.g., "Pelosi, Nancy")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Bioguide ID if found, null otherwise</returns>
    Task<string?> GetBioguideIdByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all committee memberships for a specific member using their bioguide ID.
    /// </summary>
    /// <param name="bioguideId">Member's bioguide ID (e.g., "P000197")</param>
    /// <param name="congress">Congress number (e.g., 118 for 118th Congress). Defaults to current.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of committee memberships</returns>
    Task<List<CommitteeMembership>> GetMemberCommitteesAsync(
        string bioguideId,
        int? congress = null,
        CancellationToken cancellationToken = default);
}
