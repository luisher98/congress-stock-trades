using CongressStockTrades.Core.Models;

namespace CongressStockTrades.Core.Services;

/// <summary>
/// Service for processing PDF filing documents using Azure AI Document Intelligence.
/// Downloads PDFs from the House website, extracts structured data using OCR and layout analysis,
/// and transforms the results into structured transaction documents.
/// </summary>
public interface IPdfProcessor
{
    /// <summary>
    /// Downloads a PDF from the given URL and extracts structured transaction data using Azure Document Intelligence.
    /// Uses the prebuilt-layout model to extract tables and key-value pairs, then transforms them into
    /// a structured TransactionDocument with filing information and transactions list.
    /// </summary>
    /// <param name="pdfUrl">Full URL to the PDF file on house.gov</param>
    /// <param name="filingId">Unique filing identifier for the document</param>
    /// <param name="expectedName">Politician name from website for validation</param>
    /// <param name="expectedOffice">Office/district from website for validation</param>
    /// <param name="cancellationToken">Token to cancel the async operation</param>
    /// <returns>A fully populated TransactionDocument with filing info and transactions</returns>
    /// <exception cref="System.Net.Http.HttpRequestException">Thrown when PDF download fails</exception>
    /// <exception cref="System.Exception">Thrown when Document Intelligence API call fails</exception>
    Task<TransactionDocument> ProcessPdfAsync(
        string pdfUrl,
        string filingId,
        string expectedName,
        string expectedOffice,
        CancellationToken cancellationToken = default);
}
