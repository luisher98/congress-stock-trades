using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace CongressStockTrades.Core.Models;

/// <summary>
/// Complete filing document with transaction details stored in Cosmos DB 'transactions' container.
/// Contains both the filing metadata and the list of stock transactions extracted from the PDF.
/// </summary>
public class TransactionDocument
{
    /// <summary>
    /// Cosmos DB document ID.
    /// </summary>
    [JsonPropertyName("id")]  // For System.Text.Json
    [JsonProperty("id")]      // For Newtonsoft.Json (used by Cosmos SDK v3)
    public required string Id { get; set; }

    /// <summary>
    /// Filing identifier used as the partition key.
    /// Example: "20250123456"
    /// </summary>
    [JsonPropertyName("filingId")]  // For System.Text.Json - camelCase to match Cosmos DB partition key path
    [JsonProperty("filingId")]      // For Newtonsoft.Json (used by Cosmos SDK v3) - camelCase to match Cosmos DB partition key path
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
    /// Filing date from the PDF.
    /// Example: "2025-01-06"
    /// </summary>
    public string? Filing_Date { get; set; }

    /// <summary>
    /// Whether this filing is for an Initial Public Offering.
    /// </summary>
    public bool IsIPO { get; set; }

    /// <summary>
    /// Filing metadata extracted from the PDF header.
    /// Contains politician name, filing status, and district information.
    /// </summary>
    public required FilingInformation Filing_Information { get; set; }

    /// <summary>
    /// List of investment vehicles (optional).
    /// Example: "Real estate investments (Owner: JT)"
    /// </summary>
    public List<string>? Investment_Vehicles { get; set; }

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

    /// <summary>
    /// List of committee memberships for this member (optional).
    /// Populated from Congress.gov API if available.
    /// </summary>
    public List<CommitteeMembership>? Committees { get; set; }
}

/// <summary>
/// Individual stock transaction extracted from PDF table rows.
/// </summary>
public class Transaction
{
    /// <summary>
    /// Description of the asset being traded.
    /// Example: "Apple Inc. - Common Stock (AAPL) [ST]"
    /// Note: This is the full asset string as extracted from the PDF, including tickers and tags.
    /// </summary>
    public required string Asset { get; set; }

    /// <summary>
    /// Type of asset based on bracket tag from PDF.
    /// Examples: "Stock", "Bond", "Crypto", "Fund", "Option", "Unknown"
    /// Derived from tags like [ST], [GS], [CR], [MF], [OP]
    /// </summary>
    public string? AssetType { get; set; }

    /// <summary>
    /// Enriched stock information (sector, industry, etc.) from Financial Modeling Prep API.
    /// Only populated for stocks ([ST]) with valid ticker symbols.
    /// Null for bonds, crypto, funds, or when enrichment fails.
    /// </summary>
    public StockInfo? StockInfo { get; set; }

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
