using CongressStockTrades.Core.Models;

namespace CongressStockTrades.Core.Services;

/// <summary>
/// Optional QA service for validating committee roster data against the OAL PDF.
/// Non-blocking: discrepancies are logged but do not fail the run.
/// </summary>
public interface ICommitteeRosterQAService
{
    /// <summary>
    /// Validates parsed roster data against the Official Alphabetical List (OAL).
    /// Downloads OAL, extracts member→committee mappings, and compares samples.
    /// Logs discrepancies to QA findings without throwing exceptions.
    /// </summary>
    /// <param name="oalUrl">URL to OAL PDF</param>
    /// <param name="parseResult">Parsed roster data from SCSOAL</param>
    /// <param name="sourceDate">Source date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of QA findings (empty if no discrepancies)</returns>
    Task<List<QAFindingDocument>> ValidateAgainstOALAsync(
        string oalUrl,
        CommitteeRosterParseResult parseResult,
        string sourceDate,
        CancellationToken cancellationToken = default);
}
