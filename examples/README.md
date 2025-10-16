# Examples

This directory contains example client implementations for connecting to the Congress Stock Trades system.

## SignalR Client Example

[signalr-client.html](signalr-client.html)

A standalone HTML page that demonstrates how to connect to the Azure Functions SignalR hub and receive real-time updates about congressional stock trading filings.

### Features

- **Real-time Updates**: Automatically receives notifications when new filings are detected
- **Connection Management**: Connect/disconnect buttons with visual status indicators
- **Notification Display**: Shows all updates with timestamps and detailed transaction information
- **Configurable Endpoint**: Supports both local development and production Azure Functions
- **Auto-reconnect**: Automatically reconnects if the connection is lost

### Usage

#### Local Development

1. **Start the Azure Functions locally**:
   ```bash
   cd src/CongressStockTrades.Functions
   func start
   ```

2. **Open the HTML file**:
   - Simply open `signalr-client.html` in any modern web browser
   - The default URL (`http://localhost:7071`) is already configured for local development

3. **Click "Connect"**:
   - The page will connect to your local Function App
   - You'll start receiving updates as the timer function runs (every 5 minutes)

#### Production (Azure)

1. **Deploy the Function App** to Azure (see [infra/README.md](../infra/README.md))

2. **Update the URL**:
   - Open `signalr-client.html` in a browser
   - Enter your Azure Function App URL:
     ```
     https://your-function-app.azurewebsites.net
     ```

3. **Click "Connect"**:
   - The page will connect to your Azure-hosted Function App
   - You'll receive real-time updates from the production system

### Notification Types

The client displays three types of notifications:

1. **Alert (Blue)** - New filing detected
   - Shows politician name, district, filing ID
   - Displays all transactions with details
   - Provides link to original PDF

2. **Error (Red)** - Processing error occurred
   - Shows filing ID and error message
   - Helps monitor system health

3. **Info (Green)** - Status updates
   - "Checking for new filings..."
   - "No new filings found"
   - Connection status changes

### Example Update Payload

When a new filing is detected, the client receives a JSON payload like this:

```json
{
  "status": "alert",
  "message": "New filing data found!",
  "time": "2025-10-16T10:30:00Z",
  "pdfUrl": "https://disclosures-clerk.house.gov/public_disc/ptr-pdfs/2025/20250123456.pdf",
  "transaction": {
    "FilingId": "20250123456",
    "PdfUrl": "https://...",
    "Filing_Information": {
      "Name": "Doe, John",
      "Status": "Filed",
      "State_District": "CA12"
    },
    "Transactions": [
      {
        "Asset": "Apple Inc. - Common Stock",
        "Transaction_Type": "Purchase",
        "Date": "2025-01-15",
        "Amount": "$1,001 - $15,000",
        "ID_Owner": "Self"
      }
    ]
  }
}
```

### Integration with Other Frameworks

While the example uses vanilla JavaScript, you can easily integrate SignalR with:

- **React**: Use `@microsoft/signalr` npm package with hooks
- **Vue**: Use `@microsoft/signalr` with Vue components
- **Angular**: Use `@microsoft/signalr` with services
- **Mobile Apps**: SignalR client libraries available for iOS, Android, Xamarin

### Example: React Integration

```tsx
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { useEffect, useState } from 'react';

function TransactionMonitor() {
  const [transactions, setTransactions] = useState([]);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('https://your-app.azurewebsites.net/api')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    connection.on('transactionUpdate', (data) => {
      if (data.status === 'alert') {
        setTransactions(prev => [data.transaction, ...prev]);
      }
    });

    connection.start();

    return () => connection.stop();
  }, []);

  return (
    <div>
      {transactions.map(t => (
        <div key={t.FilingId}>
          {t.Filing_Information.Name} - {t.Transactions.length} transactions
        </div>
      ))}
    </div>
  );
}
```

### Troubleshooting

**Connection fails with CORS error:**
- Ensure CORS is enabled in your Function App settings
- Add your domain to the allowed origins in Azure Portal

**No updates received:**
- Check that the CheckNewFilings timer function is running (every 5 minutes)
- Verify there are new filings to detect on house.gov
- Check Function App logs in Application Insights

**Connection keeps dropping:**
- Check your network connectivity
- Verify SignalR Service tier (Free tier has connection limits)
- Review Application Insights for errors

### Browser Support

- Chrome/Edge: ✅ Full support
- Firefox: ✅ Full support
- Safari: ✅ Full support
- IE11: ❌ Not supported (use polyfills)

### Security Notes

- SignalR connections are authenticated through Azure Functions
- All data is transmitted over HTTPS in production
- No sensitive credentials are stored in the client
- PDF URLs are public house.gov links

---

For more information about the system architecture, see the [main README](../README.md).
