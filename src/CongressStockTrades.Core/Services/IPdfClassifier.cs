namespace CongressStockTrades.Core.Services;

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

/// <summary>
/// Service for classifying PDFs before expensive extraction.
/// </summary>
public interface IPdfClassifier
{
    /// <summary>
    /// Classifies a PDF to determine if it's a valid PTR form.
    /// </summary>
    Task<PdfClassificationResult> ClassifyPdfAsync(
        string pdfUrl,
        CancellationToken cancellationToken = default);
}
