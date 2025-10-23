using Azure.Storage.Blobs;
using CongressStockTrades.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace CongressStockTrades.Infrastructure.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<BlobStorageService> _logger;
    private readonly HttpClient _httpClient;
    private const string ContainerName = "committee-rosters";

    public BlobStorageService(
        IConfiguration configuration,
        ILogger<BlobStorageService> logger,
        HttpClient httpClient)
    {
        var connectionString = configuration["AzureWebJobsStorage"]
            ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage")
            ?? throw new InvalidOperationException("AzureWebJobsStorage not configured");

        _blobServiceClient = new BlobServiceClient(connectionString);
        _logger = logger;
        _httpClient = httpClient;

        // Ensure container exists
        _blobServiceClient.GetBlobContainerClient(ContainerName).CreateIfNotExists();
    }

    public async Task<string> UploadPdfAsync(
        Stream pdfStream,
        string sourceDate,
        string filename,
        CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        var blobPath = $"{sourceDate}/{filename}";
        var blobClient = containerClient.GetBlobClient(blobPath);

        _logger.LogInformation("Uploading PDF to blob storage: {BlobPath}", blobPath);

        // Reset stream position before upload
        if (pdfStream.CanSeek)
        {
            pdfStream.Position = 0;
        }

        await blobClient.UploadAsync(pdfStream, overwrite: true, cancellationToken);

        _logger.LogInformation("Successfully uploaded PDF to {BlobUri}", blobClient.Uri);
        return blobClient.Uri.ToString();
    }

    public async Task<(Stream Stream, long ByteLength)> DownloadPdfAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Downloading PDF from {Url}", url);

        var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength ?? 0;
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        // Copy to MemoryStream to allow seeking (needed for hash computation and re-reading)
        var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        _logger.LogInformation("Downloaded PDF: {ByteLength} bytes", contentLength);
        return (memoryStream, contentLength);
    }

    public string ComputeHash(Stream stream)
    {
        if (!stream.CanSeek)
        {
            throw new ArgumentException("Stream must be seekable for hash computation", nameof(stream));
        }

        stream.Position = 0;
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        stream.Position = 0; // Reset for re-reading

        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
