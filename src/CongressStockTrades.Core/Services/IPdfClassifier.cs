using CongressStockTrades.Infrastructure.Services;

namespace CongressStockTrades.Core.Services;

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
