using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure;
using CongressStockTrades.Core.Services;
using CongressStockTrades.Infrastructure.Repositories;
using CongressStockTrades.Infrastructure.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        // Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        var configuration = context.Configuration;

        // Log configuration for debugging
        Console.WriteLine($"[STARTUP] DocumentIntelligence__ModelId: {configuration["DocumentIntelligence__ModelId"] ?? "NULL"}");
        Console.WriteLine($"[STARTUP] DocumentIntelligence__Endpoint: {(configuration["DocumentIntelligence__Endpoint"] != null ? "SET" : "NULL")}");
        Console.WriteLine($"[STARTUP] SignalR__ConnectionString: {(configuration["SignalR__ConnectionString"] != null ? "SET" : "NULL")}");
        Console.WriteLine($"[STARTUP] CosmosDb__Endpoint: {(configuration["CosmosDb__Endpoint"] != null ? "SET" : "NULL")}");

        // Register HttpClient for FilingFetcher
        services.AddHttpClient<IFilingFetcher, FilingFetcher>();

        // Register Document Intelligence client
        services.AddSingleton<DocumentAnalysisClient>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var endpoint = config["DocumentIntelligence__Endpoint"]
                ?? Environment.GetEnvironmentVariable("DocumentIntelligence__Endpoint")
                ?? throw new InvalidOperationException("DocumentIntelligence__Endpoint not configured");
            var key = config["DocumentIntelligence__Key"]
                ?? Environment.GetEnvironmentVariable("DocumentIntelligence__Key")
                ?? throw new InvalidOperationException("DocumentIntelligence__Key not configured");

            return new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(key));
        });

        // Register HttpClient for PdfProcessor
        services.AddHttpClient();

        // Register SignalR Service - use lazy initialization to avoid blocking startup
        services.AddSingleton<ServiceHubContext>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var connectionString = config["SignalR__ConnectionString"]
                ?? Environment.GetEnvironmentVariable("SignalR__ConnectionString")
                ?? throw new InvalidOperationException("SignalR__ConnectionString not configured");

            var serviceManager = new ServiceManagerBuilder()
                .WithOptions(option =>
                {
                    option.ConnectionString = connectionString;
                })
                .BuildServiceManager();

            try
            {
                // Use a timeout to prevent indefinite blocking
                var task = serviceManager.CreateHubContextAsync("transactions", default);
                if (task.Wait(TimeSpan.FromSeconds(30)))
                {
                    return task.Result;
                }
                else
                {
                    throw new TimeoutException("SignalR hub context creation timed out after 30 seconds");
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't crash the entire app
                Console.WriteLine($"[ERROR] Failed to create SignalR hub context: {ex.Message}");
                throw;
            }
        });

        // Register services
        services.AddSingleton<IPdfProcessor, PdfProcessor>();
        services.AddSingleton<IDataValidator, DataValidator>();
        services.AddSingleton<ITransactionRepository, TransactionRepository>();
        services.AddSingleton<INotificationService, SignalRNotificationService>();

        // Register logging
        services.AddLogging();
    })
    .Build();

host.Run();
