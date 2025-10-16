using CongressStockTrades.Core.Models;

namespace CongressStockTrades.Core.Services;

public interface IPdfProcessor
{
    /// <summary>
    /// Downloads and processes a PDF to extract transaction data
    /// </summary>
    Task<TransactionDocument> ProcessPdfAsync(
        string pdfUrl,
        string filingId,
        string expectedName,
        string expectedOffice,
        CancellationToken cancellationToken = default);
}
