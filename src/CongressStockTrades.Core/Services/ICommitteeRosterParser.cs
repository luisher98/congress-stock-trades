using CongressStockTrades.Core.Models;

namespace CongressStockTrades.Core.Services;

/// <summary>
/// Parser service for extracting committee roster data from House PDF documents.
/// </summary>
public interface ICommitteeRosterParser
{
    /// <summary>
    /// Parses the Standing and Select Committees PDF (scsoal.pdf).
    /// Extracts committees, subcommittees, members, and assignments with full provenance.
    /// </summary>
    /// <param name="pdfStream">PDF file stream</param>
    /// <param name="sourceDate">Source date extracted from PDF cover (YYYY-MM-DD)</param>
    /// <param name="blobUri">Blob storage URI where PDF is stored</param>
    /// <param name="pdfHash">SHA256 hash of PDF bytes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed roster data</returns>
    Task<CommitteeRosterParseResult> ParseSCSOALAsync(
        Stream pdfStream,
        string sourceDate,
        string blobUri,
        string pdfHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts the cover date from the PDF (page 1).
    /// Searches for patterns like "SEPTEMBER 16, 2025" and returns as "2025-09-16".
    /// </summary>
    /// <param name="pdfStream">PDF file stream</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Source date in YYYY-MM-DD format</returns>
    Task<string?> ExtractCoverDateAsync(Stream pdfStream, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of parsing a committee roster PDF.
/// Contains all extracted entities ready for upsert.
/// </summary>
public class CommitteeRosterParseResult
{
    public required List<CommitteeDocument> Committees { get; set; }
    public required List<SubcommitteeDocument> Subcommittees { get; set; }
    public required List<MemberDocument> Members { get; set; }
    public required List<AssignmentDocument> Assignments { get; set; }

    /// <summary>
    /// Run status (Success, Degraded).
    /// Degraded if structural anomalies detected.
    /// </summary>
    public string Status { get; set; } = "Success";

    /// <summary>
    /// Optional warning messages.
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}
