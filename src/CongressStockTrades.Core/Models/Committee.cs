namespace CongressStockTrades.Core.Models;

/// <summary>
/// Result of looking up a congressional member, including bioguide ID and party affiliation.
/// </summary>
public class MemberLookupResult
{
    /// <summary>
    /// Bioguide ID of the member (e.g., "P000197").
    /// </summary>
    public required string BioguideId { get; set; }

    /// <summary>
    /// Party affiliation (e.g., "Democratic", "Republican", "Independent").
    /// </summary>
    public string? PartyName { get; set; }
}

/// <summary>
/// Represents a congressional committee and member's role within it.
/// Data sourced from Congress.gov API.
/// </summary>
public class CommitteeMembership
{
    /// <summary>
    /// Committee system code (e.g., "HSBA" for House Financial Services).
    /// </summary>
    public required string CommitteeCode { get; set; }

    /// <summary>
    /// Full committee name (e.g., "Committee on Financial Services").
    /// </summary>
    public required string CommitteeName { get; set; }

    /// <summary>
    /// Chamber where the committee exists (House, Senate, Joint).
    /// </summary>
    public required string Chamber { get; set; }

    /// <summary>
    /// Member's role on the committee (e.g., "Member", "Chairman", "Ranking Member").
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Rank/position within the committee (optional).
    /// </summary>
    public int? Rank { get; set; }
}
