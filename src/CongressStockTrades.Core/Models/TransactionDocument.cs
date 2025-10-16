using System.Text.Json.Serialization;

namespace CongressStockTrades.Core.Models;

/// <summary>
/// Complete filing document with transaction details stored in Cosmos DB 'transactions' container.
/// Contains both the filing metadata and the list of stock transactions extracted from the PDF.
/// </summary>
public class TransactionDocument
{
    /// <summary>
    /// Cosmos DB document ID (auto-generated GUID).
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Filing identifier used as the partition key.
    /// Example: "20250123456"
    /// </summary>
    public required string FilingId { get; set; }

    /// <summary>
    /// URL reference to the original PDF document.
    /// The PDF itself is not stored, only the URL.
    /// </summary>
    public required string PdfUrl { get; set; }

    /// <summary>
    /// Timestamp when the PDF processing completed.
    /// Set to UTC now when the document is created.
    /// </summary>
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Filing metadata extracted from the PDF header.
    /// Contains politician name, filing status, and district information.
    /// </summary>
    public required FilingInformation Filing_Information { get; set; }

    /// <summary>
    /// List of stock transactions extracted from the PDF tables.
    /// Each transaction includes asset, type, date, amount, and owner information.
    /// </summary>
    public required List<Transaction> Transactions { get; set; }
}

/// <summary>
/// Filing metadata extracted from the PDF header section.
/// </summary>
public class FilingInformation
{
    /// <summary>
    /// Politician's full name.
    /// Example: "Doe, John"
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Filing status from the PDF.
    /// Typically "Filed" or "Amended".
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// State and district information.
    /// Example: "CA12"
    /// </summary>
    public required string State_District { get; set; }
}

/// <summary>
/// Individual stock transaction extracted from PDF table rows.
/// </summary>
public class Transaction
{
    /// <summary>
    /// Owner of the asset (Self, Spouse, Joint, Dependent Child).
    /// </summary>
    public required string ID_Owner { get; set; }

    /// <summary>
    /// Description of the asset being traded.
    /// Example: "Apple Inc. - Common Stock"
    /// </summary>
    public required string Asset { get; set; }

    /// <summary>
    /// Type of transaction (Purchase, Sale, Exchange).
    /// </summary>
    public required string Transaction_Type { get; set; }

    /// <summary>
    /// Date of the transaction.
    /// Example: "2025-01-15"
    /// </summary>
    public required string Date { get; set; }

    /// <summary>
    /// Transaction amount range.
    /// Example: "$1,001 - $15,000"
    /// </summary>
    public required string Amount { get; set; }
}
