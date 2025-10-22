using CongressStockTrades.Core.Models;
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
    private readonly string _botToken;
    private readonly string _chatId;
    private readonly bool _enabled;

    public TelegramNotificationService(
        IConfiguration configuration,
        ILogger<TelegramNotificationService> logger)
    {
        _httpClient = new HttpClient();
        _logger = logger;

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
            var message = FormatTransactionMessage(transaction);
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

    private string FormatTransactionMessage(TransactionDocument transaction)
    {
        var sb = new StringBuilder();

        // Header with emoji
        sb.AppendLine("🚨 *New Congressional Stock Trade*");
        sb.AppendLine();

        // Politician Info
        sb.AppendLine($"👤 *{EscapeMarkdown(transaction.Filing_Information.Name)}*");
        sb.AppendLine($"📍 District: {transaction.Filing_Information.State_District}");

        if (!string.IsNullOrEmpty(transaction.Filing_Date))
        {
            sb.AppendLine($"📅 Filed: {transaction.Filing_Date}");
        }

        if (transaction.IsIPO)
        {
            sb.AppendLine("💎 *IPO Transaction*");
        }

        sb.AppendLine();

        // Transactions
        sb.AppendLine($"📊 *{transaction.Transactions.Count} Transaction(s)*");
        sb.AppendLine();

        foreach (var (tx, index) in transaction.Transactions.Select((t, i) => (t, i + 1)))
        {
            sb.AppendLine($"*Transaction {index}:*");
            sb.AppendLine($"🏢 Asset: {EscapeMarkdown(tx.Asset)}");
            sb.AppendLine($"📈 Type: {tx.Transaction_Type}");
            sb.AppendLine($"📅 Date: {tx.Date}");
            sb.AppendLine($"💰 Amount: {tx.Amount}");
            sb.AppendLine();
        }

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
    /// Escapes special characters for Telegram Markdown.
    /// </summary>
    private string EscapeMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // Escape special Markdown characters
        var specialChars = new[] { '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!' };

        foreach (var c in specialChars)
        {
            text = text.Replace(c.ToString(), $"\\{c}");
        }

        return text;
    }
}
