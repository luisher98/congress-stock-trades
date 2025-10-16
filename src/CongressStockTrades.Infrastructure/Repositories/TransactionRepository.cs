using CongressStockTrades.Core.Models;
using CongressStockTrades.Core.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CongressStockTrades.Infrastructure.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly CosmosClient _cosmosClient;
    private readonly ILogger<TransactionRepository> _logger;
    private readonly Container _transactionsContainer;
    private readonly Container _processedFilingsContainer;

    public TransactionRepository(
        IConfiguration configuration,
        ILogger<TransactionRepository> logger)
    {
        var endpoint = configuration["CosmosDb__Endpoint"]
            ?? throw new InvalidOperationException("CosmosDb__Endpoint not configured");
        var key = configuration["CosmosDb__Key"]
            ?? throw new InvalidOperationException("CosmosDb__Key not configured");
        var databaseName = configuration["CosmosDb__DatabaseName"] ?? "CongressTrades";

        _cosmosClient = new CosmosClient(endpoint, key);
        _logger = logger;

        var database = _cosmosClient.GetDatabase(databaseName);
        _transactionsContainer = database.GetContainer("transactions");
        _processedFilingsContainer = database.GetContainer("processed-filings");
    }

    public async Task StoreTransactionAsync(TransactionDocument document, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Storing transaction for filing {FilingId}", document.FilingId);

        try
        {
            await _transactionsContainer.CreateItemAsync(
                document,
                new PartitionKey(document.FilingId),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Successfully stored transaction for filing {FilingId}", document.FilingId);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogWarning("Transaction for filing {FilingId} already exists", document.FilingId);
        }
    }

    public async Task<bool> IsFilingProcessedAsync(string filingId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _processedFilingsContainer.ReadItemAsync<ProcessedFiling>(
                filingId,
                new PartitionKey(filingId),
                cancellationToken: cancellationToken);

            return response.Resource != null;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task MarkAsProcessedAsync(
        string filingId,
        string pdfUrl,
        string politician,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Marking filing {FilingId} as processed", filingId);

        var processedFiling = new ProcessedFiling
        {
            Id = filingId,
            PdfUrl = pdfUrl,
            Politician = politician,
            Status = "completed"
        };

        try
        {
            await _processedFilingsContainer.UpsertItemAsync(
                processedFiling,
                new PartitionKey(filingId),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Successfully marked filing {FilingId} as processed", filingId);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Failed to mark filing {FilingId} as processed", filingId);
            throw;
        }
    }

    public async Task<TransactionDocument?> GetLatestTransactionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving latest transaction");

        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c ORDER BY c.processedAt DESC");

        using var iterator = _transactionsContainer.GetItemQueryIterator<TransactionDocument>(query);

        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            return response.FirstOrDefault();
        }

        return null;
    }
}
