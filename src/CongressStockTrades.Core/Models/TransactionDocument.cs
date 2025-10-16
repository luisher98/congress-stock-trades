using System.Text.Json.Serialization;

namespace CongressStockTrades.Core.Models;

/// <summary>
/// Complete transaction document stored in Cosmos DB
/// </summary>
public class TransactionDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public required string FilingId { get; set; }
    public required string PdfUrl { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public required FilingInformation Filing_Information { get; set; }
    public required List<Transaction> Transactions { get; set; }
}

public class FilingInformation
{
    public required string Name { get; set; }
    public required string Status { get; set; }
    public required string State_District { get; set; }
}

public class Transaction
{
    public required string ID_Owner { get; set; }
    public required string Asset { get; set; }
    public required string Transaction_Type { get; set; }
    public required string Date { get; set; }
    public required string Amount { get; set; }
}
