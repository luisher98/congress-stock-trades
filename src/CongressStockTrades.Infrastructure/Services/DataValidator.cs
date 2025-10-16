using CongressStockTrades.Core.Models;
using CongressStockTrades.Core.Services;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace CongressStockTrades.Infrastructure.Services;

public class DataValidator : IDataValidator
{
    private readonly ILogger<DataValidator> _logger;

    public DataValidator(ILogger<DataValidator> logger)
    {
        _logger = logger;
    }

    public void Validate(TransactionDocument document, string expectedName, string expectedOffice)
    {
        var errors = new List<string>();

        // Early validation if website data is missing
        if (string.IsNullOrEmpty(expectedName) || string.IsNullOrEmpty(expectedOffice))
        {
            _logger.LogWarning("Missing website data - limited validation possible");
            if (document.Transactions == null || document.Transactions.Count == 0)
            {
                throw new ValidationException("No transactions found in processed data");
            }
            return;
        }

        // Validate filing information exists
        if (document.Filing_Information == null)
        {
            errors.Add("Filing information is missing");
        }
        else
        {
            // Validate name with normalization (matching legacy logic)
            var normalizedWebsiteName = NormalizeName(expectedName);
            var normalizedTransactionName = NormalizeName(document.Filing_Information.Name);
            var nameMatch = normalizedWebsiteName == normalizedTransactionName;

            // Validate office
            var officeMatch = document.Filing_Information.State_District.Contains(expectedOffice, StringComparison.OrdinalIgnoreCase);

            _logger.LogInformation("Name match: {NameMatch}", nameMatch);
            _logger.LogInformation("Office match: {OfficeMatch}", officeMatch);

            if (!nameMatch || !officeMatch)
            {
                _logger.LogInformation("Data mismatch detected");
                _logger.LogInformation("  Website Name: {WebsiteName}", expectedName);
                _logger.LogInformation("  Normalized: {NormalizedWebsiteName}", normalizedWebsiteName);
                _logger.LogInformation("  Filing Name: {FilingName}", document.Filing_Information.Name);
                _logger.LogInformation("  Normalized: {NormalizedFilingName}", normalizedTransactionName);
                _logger.LogInformation("  Office: {WebsiteOffice} vs {FilingOffice}", expectedOffice, document.Filing_Information.State_District);

                // If only name doesn't match but office does, and names are similar, proceed with warning
                if (officeMatch && (normalizedWebsiteName.Contains(normalizedTransactionName) ||
                    normalizedTransactionName.Contains(normalizedWebsiteName)))
                {
                    _logger.LogInformation("Names are similar enough to proceed with caution");
                }
                else
                {
                    throw new ValidationException("Data mismatch between website and filing");
                }
            }
        }

        // Validate transactions exist
        if (document.Transactions == null || document.Transactions.Count == 0)
        {
            errors.Add("No transactions found in document");
        }
        else
        {
            // Validate each transaction has required fields
            for (int i = 0; i < document.Transactions.Count; i++)
            {
                var transaction = document.Transactions[i];

                if (string.IsNullOrWhiteSpace(transaction.Asset))
                    errors.Add($"Transaction {i + 1}: Asset is missing");

                if (string.IsNullOrWhiteSpace(transaction.Transaction_Type))
                    errors.Add($"Transaction {i + 1}: Transaction type is missing");

                if (string.IsNullOrWhiteSpace(transaction.Date))
                    errors.Add($"Transaction {i + 1}: Date is missing");

                if (string.IsNullOrWhiteSpace(transaction.Amount))
                    errors.Add($"Transaction {i + 1}: Amount is missing");
            }
        }

        // Validate PDF URL
        if (string.IsNullOrWhiteSpace(document.PdfUrl))
        {
            errors.Add("PDF URL is missing");
        }

        // Validate filing ID
        if (string.IsNullOrWhiteSpace(document.FilingId))
        {
            errors.Add("Filing ID is missing");
        }

        if (errors.Count > 0)
        {
            var errorMessage = string.Join("; ", errors);
            _logger.LogError("Validation failed for filing {FilingId}: {Errors}", document.FilingId, errorMessage);
            throw new ValidationException($"Validation failed: {errorMessage}");
        }

        _logger.LogInformation("Found {Count} transaction(s)", document.Transactions?.Count ?? 0);
        _logger.LogInformation("Validation passed for filing {FilingId}", document.FilingId);
    }

    /// <summary>
    /// Normalizes a name for comparison by:
    /// - Removing titles (Hon., Dr., etc.)
    /// - Removing punctuation
    /// - Converting to lowercase
    /// - Sorting name parts to handle different orderings
    /// </summary>
    private static string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        var normalized = name.ToLowerInvariant();

        // Remove common titles
        normalized = Regex.Replace(normalized, @"^hon\.?\s+", "", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"^dr\.?\s+", "", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s+mrs\.?\s+", " ", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s+mr\.?\s+", " ", RegexOptions.IgnoreCase);

        // Remove all punctuation including dots and commas
        normalized = Regex.Replace(normalized, @"[.,]", "");

        // Split into parts, remove empty/whitespace, and filter suffixes
        var parts = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !Regex.IsMatch(part, @"^(jr|sr|[ivx]+|[1-9](?:st|nd|rd|th))$"))
            .OrderBy(part => part)
            .ToList();

        return string.Join(" ", parts);
    }
}

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message)
    {
    }
}
