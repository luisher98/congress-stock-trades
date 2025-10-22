# Telegram Notifications Setup

Get real-time congressional stock trade alerts sent directly to your Telegram!

## Step 1: Create Your Telegram Bot

1. Open Telegram and search for `@BotFather`
2. Send `/newbot`
3. Choose a name (e.g., "Congress Trades Alert")
4. Choose a username (e.g., "congress_trades_bot")
5. **Save your Bot Token**: `123456789:ABCdefGHIjklMNOpqrsTUVwxyz`

## Step 2: Get Your Chat ID

### Option A: Using your bot
1. Send any message to your bot (e.g., "/start")
2. Visit: `https://api.telegram.org/bot<YOUR_BOT_TOKEN>/getUpdates`
3. Find `"chat":{"id":123456789}` in the JSON
4. **Save your Chat ID**: `123456789`

### Option B: Using @userinfobot
1. Search for `@userinfobot` in Telegram
2. Send `/start`
3. Bot will reply with your Chat ID

## Step 3: Configure Local Settings

Add to `src/CongressStockTrades.Functions/local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",

    "Telegram__BotToken": "123456789:ABCdefGHIjklMNOpqrsTUVwxyz",
    "Telegram__ChatId": "123456789",

    "CosmosDb__Endpoint": "...",
    "CosmosDb__Key": "...",
    "DocumentIntelligence__Endpoint": "...",
    "DocumentIntelligence__Key": "...",
    "SignalR__ConnectionString": "..."
  }
}
```

## Step 4: Test It!

```bash
# Start the function
cd src/CongressStockTrades.Functions
func start

# When a new filing is processed, you'll get a Telegram message like:
```

**Example Telegram Message:**
```
🚨 New Congressional Stock Trade

👤 Hon. Eleanor Holmes Norton
📍 District: DC00
📅 Filed: 01/06/2025

📊 1 Transaction(s)

Transaction 1:
🏢 Asset: Berkshire Hathaway Inc. New Common Stock (BRK.B) [ST]
📈 Type: S (partial)
📅 Date: 10/14/2025
💰 Amount: $15,001 - $50,000

📄 View PDF
```

## Step 5: Deploy to Azure

Add to your Azure Function App settings:

```bash
az functionapp config appsettings set \
  --name <your-function-app-name> \
  --resource-group <your-resource-group> \
  --settings \
    "Telegram__BotToken=123456789:ABCdefGHIjklMNOpqrsTUVwxyz" \
    "Telegram__ChatId=123456789"
```

Or via Azure Portal:
1. Go to your Function App
2. Settings → Configuration
3. Add New Application Setting:
   - Name: `Telegram__BotToken`
   - Value: `your-bot-token`
4. Add New Application Setting:
   - Name: `Telegram__ChatId`
   - Value: `your-chat-id`
5. Click Save

## Features

- ✅ **Formatted Messages** - Beautiful Markdown formatting with emojis
- ✅ **All Transaction Details** - Name, district, asset, type, date, amount
- ✅ **Direct PDF Link** - Click to view original filing
- ✅ **IPO Indicator** - Shows 💎 when it's an IPO transaction
- ✅ **Investment Vehicles** - Lists any investment vehicles
- ✅ **Error Notifications** - Get alerts when processing fails
- ✅ **Optional** - Works even if not configured (gracefully disabled)

## Troubleshooting

### Bot doesn't send messages
- Check Bot Token is correct
- Check Chat ID is correct
- Make sure you sent at least one message to the bot first

### Messages have broken formatting
- Telegram uses Markdown - special characters are escaped automatically
- If issues persist, check TelegramNotificationService.cs

### Want to send to a group chat?
1. Add your bot to the group
2. Make the bot an admin
3. Use the group chat ID instead of your personal chat ID
4. Get group chat ID from `/getUpdates` after sending a message in the group

### Want multiple recipients?
You can send to multiple chats by:
1. Creating a channel
2. Adding your bot as admin
3. Using the channel ID

Or modify the code to loop through multiple chat IDs.

## Cost

**FREE!** Telegram Bot API has no limits or costs.

## Privacy

- Messages go directly to your Telegram
- No data stored by Telegram longer than needed for delivery
- Bot can only send to chats that messaged it first
- You control the bot token

## Disable Telegram

Simply remove the config values:
- Don't set `Telegram__BotToken` or `Telegram__ChatId`
- The service will log "Telegram notifications disabled" and skip sending

No code changes needed!
