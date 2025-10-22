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

    public TransactionRepository(
        IConfiguration configuration,
        ILogger<TransactionRepository> logger)
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
        _transactionsContainer = database.GetContainer("transactions");
    }

    public async Task<bool> StoreTransactionAsync(TransactionDocument document, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Storing transaction for filing {FilingId}", document.FilingId);

        // Debug: Serialize with Newtonsoft.Json to see what Cosmos SDK sees
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(document, Newtonsoft.Json.Formatting.None);
        _logger.LogInformation("Newtonsoft serialized (first 500 chars): {Json}", json.Substring(0, Math.Min(500, json.Length)));

        try
        {
            await _transactionsContainer.CreateItemAsync(
                document,
                new PartitionKey(document.FilingId),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Successfully stored transaction for filing {FilingId}", document.FilingId);
            return true; // Successfully stored
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogWarning("Transaction for filing {FilingId} already exists", document.FilingId);
            return false; // Already existed
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
