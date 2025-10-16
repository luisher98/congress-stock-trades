using CongressStockTrades.Core.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace CongressStockTrades.Functions.Functions;

/// <summary>
/// HTTP-triggered functions for the REST API.
/// Provides endpoints for querying transaction data and health checks.
/// </summary>
public class TransactionApiFunction
{
    private readonly ITransactionRepository _repository;
    private readonly ILogger<TransactionApiFunction> _logger;

    /// <summary>
    /// Initializes a new instance of the TransactionApiFunction class.
    /// </summary>
    public TransactionApiFunction(
        ITransactionRepository repository,
        ILogger<TransactionApiFunction> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/latest - Returns the most recently processed transaction filing.
    /// </summary>
    [Function("GetLatestTransaction")]
    public async Task<HttpResponseData> GetLatest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "latest")] HttpRequestData req)
    {
        _logger.LogInformation("GetLatest endpoint called");

        try
        {
            var transaction = await _repository.GetLatestTransactionAsync();

            if (transaction == null)
            {
                _logger.LogWarning("No transactions found");
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteAsJsonAsync(new
                {
                    error = "No transaction data available"
                });
                return notFoundResponse;
            }

            _logger.LogInformation("Returning transaction {FilingId}", transaction.FilingId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            var json = JsonSerializer.Serialize(transaction, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await response.WriteStringAsync(json);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving latest transaction");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                error = "Internal server error",
                message = ex.Message
            });
            return errorResponse;
        }
    }

    /// <summary>
    /// GET /api/health - Health check endpoint for monitoring.
    /// </summary>
    [Function("HealthCheck")]
    public async Task<HttpResponseData> Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        _logger.LogInformation("Health check endpoint called");

        try
        {
            // Test Cosmos DB connectivity
            var transaction = await _repository.GetLatestTransactionAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                dependencies = new
                {
                    cosmosDb = "healthy",
                    hasData = transaction != null
                }
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            var response = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            await response.WriteAsJsonAsync(new
            {
                status = "unhealthy",
                timestamp = DateTime.UtcNow,
                error = ex.Message
            });
            return response;
        }
    }
}
