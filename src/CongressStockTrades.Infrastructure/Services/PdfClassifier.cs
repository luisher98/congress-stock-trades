using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using CongressStockTrades.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CongressStockTrades.Infrastructure.Services;

/// <summary>
/// Classifies PDFs to determine if they are valid PTR forms before expensive extraction.
/// Uses Azure Document Intelligence prebuilt-read or a custom classification model.
/// </summary>
public class PdfClassifier : IPdfClassifier
{
    private readonly DocumentAnalysisClient _client;
    private readonly ILogger<PdfClassifier> _logger;
    private readonly string? _classificationModelId;

    public PdfClassifier(
        IConfiguration configuration,
        ILogger<PdfClassifier> logger)
    {
        var endpoint = configuration["DocumentIntelligence__Endpoint"]
            ?? Environment.GetEnvironmentVariable("DocumentIntelligence__Endpoint")
            ?? throw new InvalidOperationException("DocumentIntelligence__Endpoint not configured");

        var key = configuration["DocumentIntelligence__Key"]
            ?? Environment.GetEnvironmentVariable("DocumentIntelligence__Key")
            ?? throw new InvalidOperationException("DocumentIntelligence__Key not configured");

        _logger = logger;
        _logger.LogInformation("[Classifier] Creating DocumentAnalysisClient with endpoint: {Endpoint}...",
            endpoint.Substring(0, Math.Min(40, endpoint.Length)));

        _client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(key));

        // Optional: Use a custom classification model if configured
        _classificationModelId = configuration["DocumentIntelligence__ClassificationModelId"]
            ?? Environment.GetEnvironmentVariable("DocumentIntelligence__ClassificationModelId");

        if (!string.IsNullOrEmpty(_classificationModelId))
        {
            _logger.LogInformation("[Classifier] Using custom classification model: {ModelId}", _classificationModelId);
        }
    }

    /// <summary>
    /// Classifies a PDF to determine if it's a valid PTR form.
    /// Uses simple heuristics with prebuilt-read model (cheap) or custom classifier.
    /// </summary>
    public async Task<PdfClassificationResult> ClassifyPdfAsync(
        string pdfUrl,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Classifier] Classifying PDF: {Url}", pdfUrl);

        try
        {
            // If custom classification model exists, use it
            if (!string.IsNullOrEmpty(_classificationModelId))
            {
                return await ClassifyWithCustomModelAsync(pdfUrl, cancellationToken);
            }

            // Otherwise, use prebuilt-read with heuristics (cheaper fallback)
            return await ClassifyWithHeuristicsAsync(pdfUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Classifier] Classification failed for {Url}", pdfUrl);

            // On error, assume it's valid to avoid false negatives
            return new PdfClassificationResult
            {
                IsValidPtrForm = true,
                Confidence = 0.0,
                Reason = $"Classification error: {ex.Message} - defaulting to valid"
            };
        }
    }

    private async Task<PdfClassificationResult> ClassifyWithCustomModelAsync(
        string pdfUrl,
        CancellationToken cancellationToken)
    {
        // Download PDF to memory stream
        using var httpClient = new HttpClient();
        using var response = await httpClient.GetAsync(pdfUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = new MemoryStream();
        await response.Content.CopyToAsync(stream, cancellationToken);
        stream.Position = 0;

        // Analyze with custom classification model
        var operation = await _client.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            _classificationModelId!,
            stream,
            cancellationToken: cancellationToken);

        var result = operation.Value;

        // Check if classified as PTR form
        var document = result.Documents.FirstOrDefault();
        if (document == null)
        {
            return new PdfClassificationResult
            {
                IsValidPtrForm = false,
                Confidence = 0.0,
                Reason = "No document detected"
            };
        }

        var isPtrForm = document.DocType?.ToLower().Contains("ptr") == true;
        var confidence = document.Confidence;

        _logger.LogInformation("[Classifier] Classification result: {DocType} (confidence: {Confidence})",
            document.DocType, confidence);

        return new PdfClassificationResult
        {
            IsValidPtrForm = isPtrForm,
            Confidence = confidence,
            Reason = $"Classified as: {document.DocType}"
        };
    }

    private async Task<PdfClassificationResult> ClassifyWithHeuristicsAsync(
        string pdfUrl,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Classifier] Using prebuilt-read with heuristics");

        // Download PDF to memory stream
        using var httpClient = new HttpClient();
        using var response = await httpClient.GetAsync(pdfUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = new MemoryStream();
        await response.Content.CopyToAsync(stream, cancellationToken);
        stream.Position = 0;

        // Use prebuilt-read (cheapest option - ~$0.0015 per page)
        var operation = await _client.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "prebuilt-read",
            stream,
            cancellationToken: cancellationToken);

        var result = operation.Value;

        // Extract text from first page
        var firstPageText = string.Join(" ",
            result.Pages.FirstOrDefault()?.Lines.Select(l => l.Content) ?? Enumerable.Empty<string>());

        _logger.LogInformation("[Classifier] Extracted {Length} chars from first page", firstPageText.Length);

        // Heuristic checks for PTR form
        var requiredKeywords = new[]
        {
            "PERIODIC TRANSACTION REPORT",
            "FILING STATUS",
            "TRANSACTION",
            "ASSET"
        };

        var foundKeywords = requiredKeywords.Count(keyword =>
            firstPageText.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        var confidence = foundKeywords / (double)requiredKeywords.Length;
        var isValid = confidence >= 0.75; // Need 75% of keywords

        _logger.LogInformation("[Classifier] Found {Found}/{Total} keywords (confidence: {Confidence})",
            foundKeywords, requiredKeywords.Length, confidence);

        return new PdfClassificationResult
        {
            IsValidPtrForm = isValid,
            Confidence = confidence,
            Reason = $"Heuristic check: {foundKeywords}/{requiredKeywords.Length} keywords found"
        };
    }
}

/// <summary>
/// Result of PDF classification.
/// </summary>
public class PdfClassificationResult
{
    /// <summary>
    /// Whether the PDF is a valid PTR form.
    /// </summary>
    public bool IsValidPtrForm { get; set; }

    /// <summary>
    /// Confidence score (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Reason for classification decision.
    /// </summary>
    public required string Reason { get; set; }
}
