using CongressStockTrades.Core.Models;
using CongressStockTrades.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace CongressStockTrades.Infrastructure.Services;

/// <summary>
/// Sends transaction notifications to Telegram via Bot API.
/// </summary>
public class TelegramNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramNotificationService> _logger;
    private readonly ICommitteeRosterRepository? _committeeRosterRepository;
    private readonly string _botToken;
    private readonly string _chatId;
    private readonly bool _enabled;

    public TelegramNotificationService(
        IConfiguration configuration,
        ILogger<TelegramNotificationService> logger,
        ICommitteeRosterRepository committeeRosterRepository)
    {
        _httpClient = new HttpClient();
        _logger = logger;
        _committeeRosterRepository = committeeRosterRepository;

        _botToken = configuration["Telegram__BotToken"]
            ?? Environment.GetEnvironmentVariable("Telegram__BotToken")
            ?? "";

        _chatId = configuration["Telegram__ChatId"]
            ?? Environment.GetEnvironmentVariable("Telegram__ChatId")
            ?? "";

        _enabled = !string.IsNullOrEmpty(_botToken) && !string.IsNullOrEmpty(_chatId);

        if (!_enabled)
        {
            _logger.LogWarning("Telegram notifications disabled - BotToken or ChatId not configured");
        }
        else
        {
            _logger.LogInformation("Telegram notifications enabled for chat: {ChatId}", _chatId);
        }
    }

    /// <summary>
    /// Sends a new transaction notification to Telegram.
    /// </summary>
    public async Task SendTransactionNotificationAsync(TransactionDocument transaction)
    {
        if (!_enabled)
        {
            _logger.LogDebug("Telegram disabled - skipping notification");
            return;
        }

        try
        {
            // Try to get committee assignments from roster database
            List<AssignmentDocument>? assignments = null;
            if (_committeeRosterRepository != null)
            {
                try
                {
                    _logger.LogInformation("Attempting to find committee assignments for {Name}", transaction.Filing_Information.Name);
                    var (lastName, firstName) = ExtractNameParts(transaction.Filing_Information.Name);
                    _logger.LogInformation("Extracted name parts: LastName='{LastName}', FirstName='{FirstName}'", lastName, firstName);

                    if (!string.IsNullOrEmpty(lastName) && !string.IsNullOrEmpty(firstName))
                    {
                        var member = await _committeeRosterRepository.FindMemberByNameAsync(lastName, firstName);
                        if (member != null)
                        {
                            _logger.LogInformation("Found member in database: {DisplayName} ({MemberKey})", member.DisplayName, member.MemberKey);
                            assignments = await _committeeRosterRepository.GetMemberAssignmentsAsync(member.MemberKey);
                            _logger.LogInformation("Found {Count} committee assignments for {Name}", assignments.Count, member.DisplayName);
                        }
                        else
                        {
                            _logger.LogWarning("Member not found in committee roster database: {Name} (searched for '{LastName}, {FirstName}')",
                                transaction.Filing_Information.Name, lastName, firstName);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Could not extract valid name parts from: {Name}", transaction.Filing_Information.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to retrieve committee assignments for {Name}", transaction.Filing_Information.Name);
                    // Fall back to old committee data if available
                }
            }
            else
            {
                _logger.LogInformation("CommitteeRosterRepository is null, skipping committee lookup");
            }

            var message = FormatTransactionMessage(transaction, assignments);
            await SendMessageAsync(message);

            _logger.LogInformation("Sent Telegram notification for filing {FilingId}", transaction.FilingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram notification for filing {FilingId}", transaction.FilingId);
            // Don't throw - Telegram failures shouldn't break the main flow
        }
    }

    /// <summary>
    /// Sends an error notification to Telegram.
    /// </summary>
    public async Task SendErrorNotificationAsync(string filingId, string error)
    {
        if (!_enabled) return;

        try
        {
            var message = $"❌ *Error Processing Filing*\n\n" +
                         $"Filing ID: `{filingId}`\n" +
                         $"Error: {EscapeMarkdown(error)}";

            await SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram error notification");
        }
    }

    private string FormatTransactionMessage(TransactionDocument transaction, List<AssignmentDocument>? assignments = null)
    {
        var sb = new StringBuilder();

        // Header with emoji
        sb.AppendLine("🚨 *New Congressional Stock Trade*");
        sb.AppendLine();

        // Politician Info
        var partyBadge = GetPartyBadge(transaction.Filing_Information.Party);
        sb.AppendLine($"{partyBadge} *{EscapeMarkdown(transaction.Filing_Information.Name)}*");
        sb.AppendLine($"📍 District: {transaction.Filing_Information.State_District}");

        if (!string.IsNullOrEmpty(transaction.Filing_Date))
        {
            sb.AppendLine($"📅 Filed: {transaction.Filing_Date}");
        }

        if (transaction.IsIPO)
        {
            sb.AppendLine("💎 *IPO Transaction*");
        }

        // Committee Memberships from roster database (preferred)
        if (assignments?.Any() == true)
        {
            sb.AppendLine();

            // Get all unique committee keys from both main committees and subcommittees
            var allCommitteeKeys = assignments
                .Where(a => !string.IsNullOrEmpty(a.CommitteeAssignmentKey) || !string.IsNullOrEmpty(a.SubcommitteeAssignmentKey))
                .SelectMany(a => new[] { a.CommitteeAssignmentKey, a.SubcommitteeAssignmentKey }
                    .Where(key => !string.IsNullOrEmpty(key)))
                .Select(key => key!.Contains("::") ? key.Split("::")[0] : key)
                .Distinct()
                .OrderBy(key => key)
                .ToList();

            if (allCommitteeKeys.Any())
            {
                sb.AppendLine("📋 *Committees:*");
                
                foreach (var committeeKey in allCommitteeKeys)
                {
                    // Find main committee assignment
                    var mainCommitteeAssignment = assignments
                        .FirstOrDefault(a => a.CommitteeAssignmentKey == committeeKey);

                    if (mainCommitteeAssignment != null)
                    {
                        var role = mainCommitteeAssignment.Role != "Member" ? $" \\({mainCommitteeAssignment.Role}\\)" : "";
                        var committeeName = ConvertCommitteeKeyToDisplayName(mainCommitteeAssignment.CommitteeAssignmentKey ?? "");
                        sb.AppendLine($"• {EscapeMarkdown(committeeName)}{role}");
                    }
                    else
                    {
                        // If no main committee assignment, show the committee name from subcommittee
                        var committeeName = ConvertCommitteeKeyToDisplayName(committeeKey);
                        sb.AppendLine($"• {EscapeMarkdown(committeeName)}");
                    }

                    // Find all subcommittees for this main committee
                    var subcommittees = assignments
                        .Where(a => !string.IsNullOrEmpty(a.SubcommitteeAssignmentKey) && 
                                   a.SubcommitteeAssignmentKey.StartsWith(committeeKey + "::"))
                        .OrderBy(a => a.PositionOrder)
                        .ToList();

                    foreach (var subcommittee in subcommittees)
                    {
                        var subRole = subcommittee.Role != "Member" ? $" \\({subcommittee.Role}\\)" : "";
                        var subcommitteeName = ConvertCommitteeKeyToDisplayName(subcommittee.SubcommitteeAssignmentKey ?? "");
                        // Extract just the subcommittee name (after the ::)
                        var subName = subcommitteeName.Contains("::") 
                            ? subcommitteeName.Split("::", 2)[1] 
                            : subcommitteeName;
                        sb.AppendLine($"  - {EscapeMarkdown(subName)}{subRole}");
                    }
                }
            }
        }
        // Fallback to old API committee data if roster data unavailable
        else if (transaction.Filing_Information.Committees?.Any() == true)
        {
            sb.AppendLine();

            // Show key committees prominently
            var keyCommittees = transaction.Filing_Information.Committees
                .Where(c => IsFinanciallyRelevantCommittee(c.CommitteeCode))
                .ToList();

            var otherCommittees = transaction.Filing_Information.Committees
                .Where(c => !IsFinanciallyRelevantCommittee(c.CommitteeCode))
                .ToList();

            if (keyCommittees.Any())
            {
                sb.AppendLine("⚠️ *Key Committees:*");
                foreach (var committee in keyCommittees)
                {
                    var role = !string.IsNullOrEmpty(committee.Role) ? $" \\({committee.Role}\\)" : "";
                    sb.AppendLine($"  • {EscapeMarkdown(committee.CommitteeName)}{role}");
                }
            }

            if (otherCommittees.Any())
            {
                if (keyCommittees.Any()) sb.AppendLine();
                sb.AppendLine("📋 *Other Committees:*");
                foreach (var committee in otherCommittees)
                {
                    var role = !string.IsNullOrEmpty(committee.Role) ? $" \\({committee.Role}\\)" : "";
                    sb.AppendLine($"  • {EscapeMarkdown(committee.CommitteeName)}{role}");
                }
            }
        }

        sb.AppendLine();

        // Transactions
        sb.AppendLine($"📊 *{transaction.Transactions.Count} Transaction(s)*");
        sb.AppendLine();

        foreach (var (tx, index) in transaction.Transactions.Select((t, i) => (t, i + 1)))
        {
            sb.AppendLine($"━━━━━━━━━━━━━━");
            sb.AppendLine($"*Transaction {index}*");
            sb.AppendLine();

            // Asset name (cleaned)
            var assetTypeBadge = GetAssetTypeBadge(tx.AssetType);
            var cleanedAsset = CleanAssetName(tx.Asset);
            sb.AppendLine($"{assetTypeBadge} *{EscapeMarkdown(cleanedAsset)}*");

            // Stock enrichment info (if available)
            if (tx.StockInfo != null)
            {
                sb.AppendLine($"🏢 {EscapeMarkdown(tx.StockInfo.CompanyName)}");
                sb.AppendLine($"📂 {EscapeMarkdown(tx.StockInfo.Sector)} • {EscapeMarkdown(tx.StockInfo.Industry)}");
                sb.AppendLine();
            }

            // Transaction type with descriptive text
            var transactionTypeText = GetTransactionTypeText(tx.Transaction_Type);
            sb.AppendLine($"📈 {transactionTypeText}");
            sb.AppendLine($"📅 {tx.Date}");
            sb.AppendLine($"💰 {EscapeMarkdown(tx.Amount)}");
            sb.AppendLine();
        }

        sb.AppendLine("━━━━━━━━━━━━━━");

        // Investment Vehicles (if any)
        if (transaction.Investment_Vehicles?.Any() == true)
        {
            sb.AppendLine("🏦 *Investment Vehicles:*");
            foreach (var vehicle in transaction.Investment_Vehicles)
            {
                sb.AppendLine($"• {EscapeMarkdown(vehicle)}");
            }
            sb.AppendLine();
        }

        // PDF Link
        sb.AppendLine($"📄 [View PDF]({transaction.PdfUrl})");

        return sb.ToString();
    }

    private async Task SendMessageAsync(string message)
    {
        var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";

        var payload = new
        {
            chat_id = _chatId,
            text = message,
            parse_mode = "Markdown",
            disable_web_page_preview = true
        };

        var response = await _httpClient.PostAsJsonAsync(url, payload);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Telegram API error: {StatusCode} - {Error}", response.StatusCode, error);
            throw new HttpRequestException($"Telegram API returned {response.StatusCode}");
        }
    }

    /// <summary>
    /// Identifies committees that are particularly relevant for financial oversight and policy.
    /// </summary>
    private bool IsFinanciallyRelevantCommittee(string committeeCode)
    {
        // Key committees that oversee financial markets, banking, and related policy
        var relevantCodes = new[]
        {
            "HSBA", // House Financial Services
            "SSFI", // Senate Finance
            "SSBK", // Senate Banking, Housing, and Urban Affairs
            "HSWM", // House Ways and Means
            "HSIF", // House Energy and Commerce
            "SSEG", // Senate Energy and Natural Resources
            "SSCM", // Senate Commerce, Science, and Transportation
            "HSAP", // House Appropriations
            "SSAP", // Senate Appropriations
            "HSBG", // House Budget
            "SSBG"  // Senate Budget
        };

        return relevantCodes.Any(code => committeeCode.Contains(code, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns an emoji badge for the asset type.
    /// </summary>
    private string GetAssetTypeBadge(string? assetType)
    {
        return assetType switch
        {
            "Stock" => "📊",
            "Bond" => "📜",
            "Crypto" => "🪙",
            "Fund" => "💼",
            "Option" => "📈",
            _ => "❓"
        };
    }

    private string GetPartyBadge(string? party)
    {
        if (string.IsNullOrEmpty(party))
            return "👤";

        return party switch
        {
            "Democratic" => "🔵",
            "Republican" => "🔴",
            "Independent" => "⚪",
            _ => "👤"
        };
    }

    /// <summary>
    /// Cleans asset name by removing metadata tags and bracket notations like [ST], [GS].
    /// </summary>
    private string CleanAssetName(string asset)
    {
        if (string.IsNullOrEmpty(asset))
            return asset;

        // Remove metadata tags
        var cleanedAsset = asset;
        var metadataTags = new[]
        {
            "FILING STATUS: New",
            "FILING STATUS: Partial",
            "SUBHOLDING OF:",
            "DESCRIPTION:"
        };

        foreach (var tag in metadataTags)
        {
            var index = cleanedAsset.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                // Find the next newline or end of string after the tag
                var endIndex = cleanedAsset.IndexOf('\n', index);
                if (endIndex >= 0)
                {
                    cleanedAsset = cleanedAsset.Remove(index, endIndex - index + 1);
                }
                else
                {
                    cleanedAsset = cleanedAsset.Substring(0, index);
                }
            }
        }

        // Remove bracket notations like [ST], [GS], [CR], [MF], [OP]
        cleanedAsset = System.Text.RegularExpressions.Regex.Replace(cleanedAsset, @"\s*\[[A-Z]{2}\]\s*", " ");

        // Clean up multiple spaces and trim
        cleanedAsset = System.Text.RegularExpressions.Regex.Replace(cleanedAsset, @"\s+", " ").Trim();

        return cleanedAsset;
    }

    /// <summary>
    /// Returns a human-readable description for transaction type codes.
    /// </summary>
    private string GetTransactionTypeText(string transactionType)
    {
        return transactionType?.ToUpperInvariant() switch
        {
            "P" => "Purchase",
            "S" => "Sale",
            "E" => "Exchange",
            _ => transactionType ?? "Unknown"
        };
    }

    /// <summary>
    /// Escapes special characters for Telegram Markdown.
    /// Only escapes characters that would break formatting, not all special chars.
    /// </summary>
    private string EscapeMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // Only escape characters that actually break Markdown formatting
        // Don't escape: dots, dashes, slashes (used in normal text)
        text = text.Replace("_", "\\_");
        text = text.Replace("*", "\\*");
        text = text.Replace("[", "\\[");
        text = text.Replace("`", "\\`");

        return text;
    }

    /// <summary>
    /// Extracts last name and first name from various formats.
    /// Handles: "Hon. Pete Sessions", "Sessions, Pete", "Pete Sessions"
    /// </summary>
    private (string lastName, string firstName) ExtractNameParts(string fullName)
    {
        if (string.IsNullOrEmpty(fullName))
            return (string.Empty, string.Empty);

        try
        {
            // Remove honorifics
            var cleaned = fullName
                .Replace("Hon. ", "", StringComparison.OrdinalIgnoreCase)
                .Replace("Rep. ", "", StringComparison.OrdinalIgnoreCase)
                .Replace("Sen. ", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

            // Check if name is in "LastName, FirstName" format
            if (cleaned.Contains(","))
            {
                var parts = cleaned.Split(',');
                var lastName = parts[0].Trim();
                var firstName = parts.Length > 1 ? parts[1].Trim().Split(' ')[0] : "";
                return (lastName, firstName);
            }

            // Otherwise assume "FirstName LastName" format
            var nameParts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (nameParts.Length >= 2)
            {
                var firstName = nameParts[0];
                var lastName = nameParts[nameParts.Length - 1];
                return (lastName, firstName);
            }

            return (string.Empty, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract name parts from {Name}", fullName);
            return (string.Empty, string.Empty);
        }
    }

    /// <summary>
    /// Converts a committee key to a display name.
    /// Example: "agriculture" -> "Agriculture"
    /// </summary>
    private string ConvertCommitteeKeyToDisplayName(string committeeKey)
    {
        if (string.IsNullOrEmpty(committeeKey))
            return committeeKey;

        // Replace hyphens with spaces and title case
        var words = committeeKey.Split('-');
        var titleCased = words.Select(w =>
        {
            if (string.IsNullOrEmpty(w)) return w;
            return char.ToUpperInvariant(w[0]) + w.Substring(1);
        });

        return string.Join(" ", titleCased);
    }
}
