using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace CongressStockTrades.Core.Models;

/// <summary>
/// Represents a congressional committee with full metadata and provenance.
/// Stored in Cosmos DB 'committees' container.
/// </summary>
public class CommitteeDocument
{
    /// <summary>
    /// Cosmos DB document ID (same as CommitteeKey).
    /// </summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Partition key: normalized committee name.
    /// Example: "agriculture"
    /// </summary>
    [JsonPropertyName("committeeKey")]
    [JsonProperty("committeeKey")]
    public required string CommitteeKey { get; set; }

    /// <summary>
    /// Display name as printed in the PDF.
    /// Example: "Committee on Agriculture"
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Chamber where the committee exists.
    /// Fixed to "House" for this implementation.
    /// </summary>
    public string Chamber { get; set; } = "House";

    /// <summary>
    /// Committee type (Standing, Select, Joint).
    /// Example: "Standing"
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Optional majority/minority ratio if present.
    /// Example: "26-22"
    /// </summary>
    public string? Ratio { get; set; }

    /// <summary>
    /// Source provenance for this committee.
    /// </summary>
    public required CommitteeProvenance Provenance { get; set; }
}

/// <summary>
/// Represents a subcommittee under a parent committee.
/// Stored in Cosmos DB 'subcommittees' container.
/// </summary>
public class SubcommitteeDocument
{
    /// <summary>
    /// Cosmos DB document ID (same as SubcommitteeKey).
    /// </summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Partition key: parent committee key.
    /// Example: "agriculture"
    /// </summary>
    [JsonPropertyName("committeeKey")]
    [JsonProperty("committeeKey")]
    public required string CommitteeKey { get; set; }

    /// <summary>
    /// Compound key: committeeKey::normalizedSubcommitteeName
    /// Example: "agriculture::livestock"
    /// </summary>
    public required string SubcommitteeKey { get; set; }

    /// <summary>
    /// Parent committee name (normalized).
    /// </summary>
    public required string ParentCommitteeName { get; set; }

    /// <summary>
    /// Subcommittee name/title as printed.
    /// Example: "Subcommittee on Livestock, Dairy, and Poultry"
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Optional notes (ex officio rules, etc.).
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Source provenance for this subcommittee.
    /// </summary>
    public required CommitteeProvenance Provenance { get; set; }
}

/// <summary>
/// Represents a congressional member from the roster.
/// Stored in Cosmos DB 'members' container.
/// </summary>
public class MemberDocument
{
    /// <summary>
    /// Cosmos DB document ID (same as MemberKey).
    /// </summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Partition key: normalized member name.
    /// Example: "smith-jason-ca02"
    /// </summary>
    [JsonPropertyName("memberKey")]
    [JsonProperty("memberKey")]
    public required string MemberKey { get; set; }

    /// <summary>
    /// Display name as printed in the PDF.
    /// Example: "Smith, Jason"
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// State abbreviation.
    /// Example: "CA"
    /// </summary>
    public required string State { get; set; }

    /// <summary>
    /// District number if shown.
    /// Example: "02"
    /// </summary>
    public string? District { get; set; }

    /// <summary>
    /// Party affiliation (optional, leave null unless mapped later).
    /// Example: "Republican"
    /// </summary>
    public string? Party { get; set; }

    /// <summary>
    /// Source provenance for this member.
    /// </summary>
    public required CommitteeProvenance Provenance { get; set; }
}

/// <summary>
/// Represents a member's assignment to a committee or subcommittee.
/// Stored in Cosmos DB 'assignments' container.
/// Preserves historical snapshots via sourceDate in the key.
/// </summary>
public class AssignmentDocument
{
    /// <summary>
    /// Cosmos DB document ID (same as AssignmentKey).
    /// </summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Partition key: committee key or member key (depends on query pattern).
    /// Using committeeKey for efficient committee roster queries.
    /// </summary>
    [JsonPropertyName("committeeKey")]
    [JsonProperty("committeeKey")]
    public required string CommitteeKey { get; set; }

    /// <summary>
    /// Compound key: memberKey::committeeOrSubcommitteeKey::sourceDate
    /// Example: "smith-jason-ca02::agriculture::2025-09-16"
    /// </summary>
    public required string AssignmentKey { get; set; }

    /// <summary>
    /// Member key reference.
    /// </summary>
    public required string MemberKey { get; set; }

    /// <summary>
    /// Member display name.
    /// </summary>
    public required string MemberDisplayName { get; set; }

    /// <summary>
    /// Committee key (for full committee assignments).
    /// </summary>
    public string? CommitteeAssignmentKey { get; set; }

    /// <summary>
    /// Subcommittee key (for subcommittee assignments).
    /// </summary>
    public string? SubcommitteeAssignmentKey { get; set; }

    /// <summary>
    /// Role on the committee/subcommittee.
    /// Examples: "Chair", "Ranking Member", "Vice Chair", "Ex Officio", "Member"
    /// </summary>
    public required string Role { get; set; }

    /// <summary>
    /// Majority or minority grouping.
    /// Values: "Majority", "Minority"
    /// </summary>
    public required string Group { get; set; }

    /// <summary>
    /// Numeric order within the list (from numbered prefix).
    /// </summary>
    public int PositionOrder { get; set; }

    /// <summary>
    /// Source provenance for this assignment.
    /// </summary>
    public required AssignmentProvenance Provenance { get; set; }
}

/// <summary>
/// Source document tracking PDF runs and change detection.
/// Stored in Cosmos DB 'sources' container.
/// </summary>
public class SourceDocument
{
    /// <summary>
    /// Cosmos DB document ID (sourceDate as YYYY-MM-DD).
    /// </summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Partition key: source URL.
    /// </summary>
    [JsonPropertyName("url")]
    [JsonProperty("url")]
    public required string Url { get; set; }

    /// <summary>
    /// Source date extracted from PDF cover page.
    /// Example: "2025-09-16"
    /// </summary>
    public required string SourceDate { get; set; }

    /// <summary>
    /// SHA256 hash of PDF bytes for change detection.
    /// </summary>
    public required string PdfHash { get; set; }

    /// <summary>
    /// Blob storage URI where PDF is stored.
    /// Example: "https://storage.blob.core.windows.net/committee-rosters/2025-09-16/scsoal.pdf"
    /// </summary>
    public required string BlobUri { get; set; }

    /// <summary>
    /// PDF byte length.
    /// </summary>
    public long ByteLength { get; set; }

    /// <summary>
    /// Parser version for reproducibility.
    /// Example: "1.0.0"
    /// </summary>
    public required string ParserVersion { get; set; }

    /// <summary>
    /// Timestamp when processing completed.
    /// </summary>
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Entity counts from this run.
    /// </summary>
    public required SourceResultCounts ResultCounts { get; set; }

    /// <summary>
    /// Run status (Success, Degraded, Failed).
    /// </summary>
    public string Status { get; set; } = "Success";
}

/// <summary>
/// QA findings from optional OAL validation.
/// Stored in Cosmos DB 'qa-findings' container.
/// </summary>
public class QAFindingDocument
{
    /// <summary>
    /// Cosmos DB document ID (generated GUID).
    /// </summary>
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Partition key: source date.
    /// </summary>
    [JsonPropertyName("sourceDate")]
    [JsonProperty("sourceDate")]
    public required string SourceDate { get; set; }

    /// <summary>
    /// Type of discrepancy.
    /// Example: "MissingInSCSOAL", "MissingInOAL", "RoleMismatch"
    /// </summary>
    public required string DiscrepancyType { get; set; }

    /// <summary>
    /// Member name from sample.
    /// </summary>
    public required string MemberName { get; set; }

    /// <summary>
    /// Committee name involved.
    /// </summary>
    public required string CommitteeName { get; set; }

    /// <summary>
    /// Details/evidence of the discrepancy.
    /// </summary>
    public required string Details { get; set; }

    /// <summary>
    /// Timestamp when finding was recorded.
    /// </summary>
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Provenance metadata for committees, subcommittees, and members.
/// </summary>
public class CommitteeProvenance
{
    /// <summary>
    /// Source date from PDF cover.
    /// Example: "2025-09-16"
    /// </summary>
    public required string SourceDate { get; set; }

    /// <summary>
    /// Page number where entity was found.
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Blob URI of the source PDF.
    /// </summary>
    public required string BlobUri { get; set; }

    /// <summary>
    /// PDF hash for integrity.
    /// </summary>
    public required string PdfHash { get; set; }
}

/// <summary>
/// Provenance metadata for assignments (includes raw line).
/// </summary>
public class AssignmentProvenance
{
    /// <summary>
    /// Source date from PDF cover.
    /// Example: "2025-09-16"
    /// </summary>
    public required string SourceDate { get; set; }

    /// <summary>
    /// Page number where assignment was found.
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Blob URI of the source PDF.
    /// </summary>
    public required string BlobUri { get; set; }

    /// <summary>
    /// PDF hash for integrity.
    /// </summary>
    public required string PdfHash { get; set; }

    /// <summary>
    /// Raw line text from PDF.
    /// Example: "1. Smith, Jason, of Missouri, Chair"
    /// </summary>
    public required string RawLine { get; set; }
}

/// <summary>
/// Result counts from a parser run.
/// </summary>
public class SourceResultCounts
{
    public int CommitteesCount { get; set; }
    public int SubcommitteesCount { get; set; }
    public int MembersCount { get; set; }
    public int AssignmentsCount { get; set; }
}
