using CongressStockTrades.Core.Models;

namespace CongressStockTrades.Core.Services;

/// <summary>
/// Repository interface for managing committee roster documents in Cosmos DB.
/// Handles upserts for committees, subcommittees, members, and assignments.
/// </summary>
public interface ICommitteeRosterRepository
{
    /// <summary>
    /// Upserts a committee document.
    /// </summary>
    Task UpsertCommitteeAsync(CommitteeDocument committee, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a subcommittee document.
    /// </summary>
    Task UpsertSubcommitteeAsync(SubcommitteeDocument subcommittee, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a member document.
    /// </summary>
    Task UpsertMemberAsync(MemberDocument member, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts an assignment document.
    /// </summary>
    Task UpsertAssignmentAsync(AssignmentDocument assignment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts committees in batch.
    /// </summary>
    Task UpsertCommitteesAsync(IEnumerable<CommitteeDocument> committees, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts subcommittees in batch.
    /// </summary>
    Task UpsertSubcommitteesAsync(IEnumerable<SubcommitteeDocument> subcommittees, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts members in batch.
    /// </summary>
    Task UpsertMembersAsync(IEnumerable<MemberDocument> members, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts assignments in batch.
    /// </summary>
    Task UpsertAssignmentsAsync(IEnumerable<AssignmentDocument> assignments, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the last source document for a given URL.
    /// Used for change detection.
    /// </summary>
    Task<SourceDocument?> GetLastSourceAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a source document after a successful run.
    /// </summary>
    Task UpsertSourceAsync(SourceDocument source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a QA finding document.
    /// </summary>
    Task StoreQAFindingAsync(QAFindingDocument finding, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of assignments from the previous run for churn detection.
    /// </summary>
    Task<int> GetPreviousAssignmentCountAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all committee/subcommittee assignments for a specific member.
    /// </summary>
    Task<List<AssignmentDocument>> GetMemberAssignmentsAsync(string memberKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for a member by last name and first name.
    /// </summary>
    Task<MemberDocument?> FindMemberByNameAsync(string lastName, string firstName, CancellationToken cancellationToken = default);
}
