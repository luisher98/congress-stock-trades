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

        // Register HttpClient for FilingFetcher
        services.AddHttpClient<IFilingFetcher, FilingFetcher>();

        // Register Document Intelligence client
        services.AddSingleton<DocumentAnalysisClient>(sp =>
        {
            // Use Environment variables directly (local.settings.json values are loaded as env vars)
            var endpoint = Environment.GetEnvironmentVariable("DocumentIntelligence__Endpoint")
                ?? throw new InvalidOperationException("DocumentIntelligence__Endpoint not configured");
            var key = Environment.GetEnvironmentVariable("DocumentIntelligence__Key")
                ?? throw new InvalidOperationException("DocumentIntelligence__Key not configured");

            Console.WriteLine($"[DI] Creating DocumentAnalysisClient with endpoint: {endpoint.Substring(0, 30)}...");
            return new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(key));
        });

        // Register HttpClient for PdfProcessor
        services.AddHttpClient();

        // Register memory cache for CongressApiService
        services.AddMemoryCache();

        // Register HttpClient for CongressApiService
        services.AddHttpClient<ICongressApiService, CongressApiService>();

        // SignalR Service
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
                Console.WriteLine($"[ERROR] Failed to create SignalR hub context: {ex.Message}");
                throw;
            }
        });

        // Register services
        services.AddSingleton<IPdfProcessor, PdfProcessor>();
        services.AddSingleton<IDataValidator, DataValidator>();
        services.AddSingleton<ITransactionRepository, TransactionRepository>();
        services.AddSingleton<INotificationService, SignalRNotificationService>();
        services.AddSingleton<TelegramNotificationService>();

        // Register logging
        services.AddLogging();
    })
    .Build();

host.Run();
