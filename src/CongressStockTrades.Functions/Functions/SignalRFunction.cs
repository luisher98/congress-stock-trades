using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;

namespace CongressStockTrades.Functions.Functions;

/// <summary>
/// SignalR functions for real-time client communication.
/// Provides negotiation endpoint and connection management.
/// </summary>
public class SignalRFunction
{
    private readonly ILogger<SignalRFunction> _logger;
    private readonly ServiceHubContext _hubContext;
    private const string HubName = "transactions";

    /// <summary>
    /// Initializes a new instance of the SignalRFunction class.
    /// </summary>
    public SignalRFunction(
        ServiceHubContext hubContext,
        ILogger<SignalRFunction> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/negotiate - SignalR negotiation endpoint for clients to obtain connection info.
    /// </summary>
    [Function("negotiate")]
    public async Task<HttpResponseData> Negotiate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "negotiate")] HttpRequestData req)
    {
        _logger.LogInformation("SignalR negotiate request");

        try
        {
            var negotiateResponse = await _hubContext.NegotiateAsync(new());

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(negotiateResponse);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SignalR negotiation");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Negotiation failed" });
            return errorResponse;
        }
    }
}
