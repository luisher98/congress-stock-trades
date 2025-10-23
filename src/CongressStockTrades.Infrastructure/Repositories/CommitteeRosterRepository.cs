using CongressStockTrades.Core.Models;
using CongressStockTrades.Core.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CongressStockTrades.Infrastructure.Repositories;

public class CommitteeRosterRepository : ICommitteeRosterRepository
{
    private readonly CosmosClient _cosmosClient;
    private readonly ILogger<CommitteeRosterRepository> _logger;
    private readonly Container _committeesContainer;
    private readonly Container _subcommitteesContainer;
    private readonly Container _membersContainer;
    private readonly Container _assignmentsContainer;
    private readonly Container _sourcesContainer;
    private readonly Container _qaFindingsContainer;

    public CommitteeRosterRepository(
        IConfiguration configuration,
        ILogger<CommitteeRosterRepository> logger)
    {
        var endpoint = configuration["CosmosDb__Endpoint"]
            ?? Environment.GetEnvironmentVariable("CosmosDb__Endpoint")
            ?? throw new InvalidOperationException("CosmosDb__Endpoint not configured");
        var key = configuration["CosmosDb__Key"]
            ?? Environment.GetEnvironmentVariable("CosmosDb__Key")
            ?? throw new InvalidOperationException("CosmosDb__Key not configured");
        var databaseName = configuration["CosmosDb__DatabaseName"]
            ?? Environment.GetEnvironmentVariable("CosmosDb__DatabaseName")
            ?? "CongressTrades";

        _cosmosClient = new CosmosClient(endpoint, key);
        _logger = logger;

        var database = _cosmosClient.GetDatabase(databaseName);
        _committeesContainer = database.GetContainer("committees");
        _subcommitteesContainer = database.GetContainer("subcommittees");
        _membersContainer = database.GetContainer("members");
        _assignmentsContainer = database.GetContainer("assignments");
        _sourcesContainer = database.GetContainer("sources");
        _qaFindingsContainer = database.GetContainer("qa-findings");
    }

    public async Task UpsertCommitteeAsync(CommitteeDocument committee, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Upserting committee {CommitteeKey}", committee.CommitteeKey);
        await _committeesContainer.UpsertItemAsync(
            committee,
            new PartitionKey(committee.CommitteeKey),
            cancellationToken: cancellationToken);
    }

    public async Task UpsertSubcommitteeAsync(SubcommitteeDocument subcommittee, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Upserting subcommittee {SubcommitteeKey}", subcommittee.SubcommitteeKey);
        await _subcommitteesContainer.UpsertItemAsync(
            subcommittee,
            new PartitionKey(subcommittee.CommitteeKey),
            cancellationToken: cancellationToken);
    }

    public async Task UpsertMemberAsync(MemberDocument member, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Upserting member {MemberKey}", member.MemberKey);
        await _membersContainer.UpsertItemAsync(
            member,
            new PartitionKey(member.MemberKey),
            cancellationToken: cancellationToken);
    }

    public async Task UpsertAssignmentAsync(AssignmentDocument assignment, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Upserting assignment {AssignmentKey}", assignment.AssignmentKey);
        await _assignmentsContainer.UpsertItemAsync(
            assignment,
            new PartitionKey(assignment.CommitteeKey),
            cancellationToken: cancellationToken);
    }

    public async Task UpsertCommitteesAsync(IEnumerable<CommitteeDocument> committees, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Upserting {Count} committees in batch", committees.Count());
        foreach (var committee in committees)
        {
            await UpsertCommitteeAsync(committee, cancellationToken);
        }
    }

    public async Task UpsertSubcommitteesAsync(IEnumerable<SubcommitteeDocument> subcommittees, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Upserting {Count} subcommittees in batch", subcommittees.Count());
        foreach (var subcommittee in subcommittees)
        {
            await UpsertSubcommitteeAsync(subcommittee, cancellationToken);
        }
    }

    public async Task UpsertMembersAsync(IEnumerable<MemberDocument> members, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Upserting {Count} members in batch", members.Count());
        foreach (var member in members)
        {
            await UpsertMemberAsync(member, cancellationToken);
        }
    }

    public async Task UpsertAssignmentsAsync(IEnumerable<AssignmentDocument> assignments, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Upserting {Count} assignments in batch", assignments.Count());
        foreach (var assignment in assignments)
        {
            await UpsertAssignmentAsync(assignment, cancellationToken);
        }
    }

    public async Task<SourceDocument?> GetLastSourceAsync(string url, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving last source for URL {Url}", url);

        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.url = @url ORDER BY c.sourceDate DESC")
            .WithParameter("@url", url);

        using var iterator = _sourcesContainer.GetItemQueryIterator<SourceDocument>(query);

        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            return response.FirstOrDefault();
        }

        return null;
    }

    public async Task UpsertSourceAsync(SourceDocument source, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Upserting source document for date {SourceDate}", source.SourceDate);
        await _sourcesContainer.UpsertItemAsync(
            source,
            new PartitionKey(source.Url),
            cancellationToken: cancellationToken);
    }

    public async Task StoreQAFindingAsync(QAFindingDocument finding, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Storing QA finding for {MemberName} - {CommitteeName}", finding.MemberName, finding.CommitteeName);
        await _qaFindingsContainer.CreateItemAsync(
            finding,
            new PartitionKey(finding.SourceDate),
            cancellationToken: cancellationToken);
    }

    public async Task<int> GetPreviousAssignmentCountAsync(string url, CancellationToken cancellationToken = default)
    {
        var lastSource = await GetLastSourceAsync(url, cancellationToken);
        return lastSource?.ResultCounts.AssignmentsCount ?? 0;
    }
}
