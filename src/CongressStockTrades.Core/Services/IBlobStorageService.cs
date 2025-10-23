namespace CongressStockTrades.Core.Services;

/// <summary>
/// Service for managing blob storage operations for committee roster PDFs.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Uploads a PDF to blob storage.
    /// Path format: committee-rosters/{YYYY-MM-DD}/{filename}
    /// </summary>
    /// <param name="pdfStream">PDF file stream</param>
    /// <param name="sourceDate">Source date (YYYY-MM-DD)</param>
    /// <param name="filename">Filename (e.g., "scsoal.pdf")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Blob URI</returns>
    Task<string> UploadPdfAsync(
        Stream pdfStream,
        string sourceDate,
        string filename,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a PDF from a URL.
    /// </summary>
    /// <param name="url">PDF URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PDF stream and byte length</returns>
    Task<(Stream Stream, long ByteLength)> DownloadPdfAsync(
        string url,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes SHA256 hash of a stream.
    /// </summary>
    /// <param name="stream">Input stream (will be reset to position 0)</param>
    /// <returns>Hex-encoded SHA256 hash</returns>
    string ComputeHash(Stream stream);
}
