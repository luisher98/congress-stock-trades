# Congress Stock Trading Tracker - Documentation Guide

## Table of Contents
1. [Overview](#overview)
2. [XML Documentation Comments](#xml-documentation-comments)
3. [DocFX Setup](#docfx-setup)
4. [Swagger/OpenAPI Documentation](#swaggeropenapi-documentation)
5. [Building Documentation](#building-documentation)

---

## 1. Overview

This project uses multiple documentation approaches:
- **XML Documentation Comments** (`///`) in C# code for API documentation
- **DocFX** for generating static documentation websites from XML comments
- **Swagger/Swashbuckle** for interactive REST API documentation

---

## 2. XML Documentation Comments

### 2.1 XML Documentation Enabled

All C# projects have XML documentation file generation enabled:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);CS1591</NoWarn>
</PropertyGroup>
```

### 2.2 Writing XML Comments

Use triple-slash (`///`) comments above classes, methods, properties, and parameters:

```csharp
/// <summary>
/// Fetches the latest filing for a given year from the House disclosure website
/// </summary>
/// <param name="year">The year to fetch filings for</param>
/// <param name="cancellationToken">Cancellation token for async operations</param>
/// <returns>The most recent filing, or null if none found</returns>
/// <exception cref="HttpRequestException">Thrown when the HTTP request fails</exception>
Task<Filing?> GetLatestFilingAsync(int year, CancellationToken cancellationToken = default);
```

### 2.3 XML Documentation Tags

| Tag | Purpose | Example |
|-----|---------|---------|
| `<summary>` | Brief description of the member | `<summary>Processes PDF files</summary>` |
| `<param>` | Parameter description | `<param name="url">PDF URL</param>` |
| `<returns>` | Return value description | `<returns>Processed transaction data</returns>` |
| `<exception>` | Exceptions that can be thrown | `<exception cref="ArgumentNullException">...</exception>` |
| `<remarks>` | Additional notes | `<remarks>This method is thread-safe</remarks>` |
| `<example>` | Code example | `<example><code>var result = await Process();</code></example>` |
| `<see>` | Cross-reference | `<see cref="IFilingFetcher"/>` |

---

## 3. DocFX Setup

### 3.1 Install DocFX

```bash
# Install globally
dotnet tool install -g docfx

# Verify installation
docfx --version
```

### 3.2 Initialize DocFX Project

```bash
# From project root
mkdir docfx
cd docfx
docfx init --quiet
```

### 3.3 Configure DocFX

Edit `docfx/docfx.json`:

```json
{
  "metadata": [
    {
      "src": [
        {
          "files": [
            "src/**.csproj"
          ],
          "exclude": [
            "**/bin/**",
            "**/obj/**",
            "**/Tests/**"
          ]
        }
      ],
      "dest": "api",
      "disableGitFeatures": false,
      "disableDefaultFilter": false
    }
  ],
  "build": {
    "content": [
      {
        "files": [
          "api/**.yml",
          "api/index.md"
        ]
      },
      {
        "files": [
          "articles/**.md",
          "articles/**/toc.yml",
          "toc.yml",
          "*.md"
        ]
      }
    ],
    "resource": [
      {
        "files": [
          "images/**"
        ]
      }
    ],
    "overwrite": [
      {
        "files": [
          "apidoc/**.md"
        ],
        "exclude": [
          "obj/**",
          "_site/**"
        ]
      }
    ],
    "dest": "_site",
    "globalMetadataFiles": [],
    "fileMetadataFiles": [],
    "template": [
      "default",
      "modern"
    ],
    "postProcessors": [],
    "markdownEngineName": "markdig",
    "noLangKeyword": false,
    "keepFileLink": false,
    "cleanupCacheHistory": false,
    "disableGitFeatures": false
  }
}
```

### 3.4 Create Documentation Structure

```bash
docfx/
├── docfx.json           # Configuration file
├── index.md             # Homepage
├── toc.yml              # Table of contents
├── api/                 # Auto-generated API docs
├── articles/            # Manual documentation articles
│   ├── getting-started.md
│   ├── architecture.md
│   └── toc.yml
└── images/              # Images and diagrams
```

### 3.5 Sample index.md

```markdown
# Congress Stock Trading Tracker

## Overview

A serverless Azure Functions application that monitors and processes U.S. Congress politician stock trading disclosure data in real-time.

## Features

- Automated filing monitoring
- AI-powered PDF data extraction
- Real-time notifications via SignalR
- RESTful API
- Cosmos DB storage

## Quick Start

See [Getting Started](articles/getting-started.md) for setup instructions.

## API Reference

Browse the [API Documentation](api/index.md) for detailed class and method information.
```

---

## 4. Swagger/OpenAPI Documentation

### 4.1 Install Swashbuckle

Add to `CongressStockTrades.Functions.csproj`:

```xml
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
```

### 4.2 Configure Swagger in Program.cs

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        // ... other services ...

        // Add Swagger/OpenAPI
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Congress Stock Trading Tracker API",
                Version = "v1",
                Description = "REST API for accessing congressional stock trading data",
                Contact = new OpenApiContact
                {
                    Name = "Your Name",
                    Email = "your.email@example.com"
                }
            });

            // Include XML comments
            var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            c.IncludeXmlComments(xmlPath);
        });
    })
    .Build();

host.Run();
```

### 4.3 Add Swagger Middleware

```csharp
// In HTTP trigger function
[Function("swagger")]
public async Task<HttpResponseData> SwaggerUI(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "swagger/ui")] HttpRequestData req)
{
    var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
    response.Headers.Add("Content-Type", "text/html");

    await response.WriteStringAsync(@"
        <!DOCTYPE html>
        <html>
        <head>
            <title>API Documentation</title>
            <link rel=""stylesheet"" type=""text/css"" href=""https://cdn.jsdelivr.net/npm/swagger-ui-dist@5.9.0/swagger-ui.css"">
        </head>
        <body>
            <div id=""swagger-ui""></div>
            <script src=""https://cdn.jsdelivr.net/npm/swagger-ui-dist@5.9.0/swagger-ui-bundle.js""></script>
            <script>
                SwaggerUIBundle({
                    url: '/api/swagger.json',
                    dom_id: '#swagger-ui'
                });
            </script>
        </body>
        </html>
    ");

    return response;
}
```

### 4.4 Accessing Swagger UI

Once deployed, access Swagger UI at:
```
https://your-function-app.azurewebsites.net/api/swagger/ui
```

---

## 5. Building Documentation

### 5.1 Build API Docs with DocFX

```bash
# Generate metadata from XML comments
docfx metadata docfx/docfx.json

# Build the documentation site
docfx build docfx/docfx.json

# Serve locally for preview
docfx serve docfx/_site
```

Visit `http://localhost:8080` to view the documentation.

### 5.2 Build XML Documentation

```bash
# Build all projects with XML docs
dotnet build

# XML files are generated in:
# - src/CongressStockTrades.Core/bin/Debug/net8.0/CongressStockTrades.Core.xml
# - src/CongressStockTrades.Infrastructure/bin/Debug/net8.0/CongressStockTrades.Infrastructure.xml
# - src/CongressStockTrades.Functions/bin/Debug/net8.0/CongressStockTrades.Functions.xml
```

### 5.3 Verify Documentation

Check that XML files are created:

```bash
find src -name "*.xml" -path "*/bin/*"
```

Expected output:
```
src/CongressStockTrades.Core/bin/Debug/net8.0/CongressStockTrades.Core.xml
src/CongressStockTrades.Infrastructure/bin/Debug/net8.0/CongressStockTrades.Infrastructure.xml
src/CongressStockTrades.Functions/bin/Debug/net8.0/CongressStockTrades.Functions.xml
```

---

## 6. Publishing Documentation

### 6.1 GitHub Pages

```bash
# Build documentation
docfx build docfx/docfx.json

# Publish to GitHub Pages
git add docfx/_site
git commit -m "Update documentation"
git subtree push --prefix docfx/_site origin gh-pages
```

### 6.2 Azure Static Web Apps

```bash
# Build documentation
docfx build docfx/docfx.json

# Deploy to Azure Static Web Apps
az staticwebapp create \
  --name congress-stock-docs \
  --resource-group congress-stock-trades-rg \
  --source docfx/_site \
  --location eastus
```

### 6.3 Continuous Documentation

Add to `.github/workflows/documentation.yml`:

```yaml
name: Build Documentation

on:
  push:
    branches: [main]
    paths:
      - 'src/**/*.cs'
      - 'docfx/**'

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Install DocFX
        run: dotnet tool install -g docfx

      - name: Build Documentation
        run: |
          docfx metadata docfx/docfx.json
          docfx build docfx/docfx.json

      - name: Deploy to GitHub Pages
        uses: peaceiris/actions-gh-pages@v3
        with:
          github_token: ${{ secrets.GITHUB_TOKEN }}
          publish_dir: ./docfx/_site
```

---

## 7. Best Practices

### 7.1 Documentation Guidelines

1. **Write clear summaries**: Describe what the method does, not how
2. **Document all public APIs**: Classes, methods, properties, events
3. **Include examples**: For complex APIs, show usage examples
4. **Document exceptions**: List all exceptions that can be thrown
5. **Keep it updated**: Update comments when code changes

### 7.2 XML Comment Examples

#### Class Documentation
```csharp
/// <summary>
/// Processes PDF transaction filings using Azure Document Intelligence.
/// </summary>
/// <remarks>
/// This service downloads PDFs from the House disclosure website,
/// extracts structured transaction data using OCR, and validates
/// the results against website metadata.
/// </remarks>
public class PdfProcessor : IPdfProcessor
{
}
```

#### Method Documentation
```csharp
/// <summary>
/// Downloads and processes a PDF to extract transaction data.
/// </summary>
/// <param name="pdfUrl">Full URL to the PDF file on house.gov</param>
/// <param name="filingId">Unique filing identifier</param>
/// <param name="expectedName">Politician name from website for validation</param>
/// <param name="expectedOffice">Office/district from website for validation</param>
/// <param name="cancellationToken">Token to cancel the async operation</param>
/// <returns>A fully populated transaction document with filing info and transactions</returns>
/// <exception cref="HttpRequestException">Thrown when PDF download fails</exception>
/// <exception cref="ValidationException">Thrown when extracted data doesn't match expected values</exception>
/// <example>
/// <code>
/// var document = await processor.ProcessPdfAsync(
///     "https://disclosures-clerk.house.gov/public_disc/ptr-pdfs/2025/20250123456.pdf",
///     "20250123456",
///     "Doe, John",
///     "CA12"
/// );
/// </code>
/// </example>
Task<TransactionDocument> ProcessPdfAsync(
    string pdfUrl,
    string filingId,
    string expectedName,
    string expectedOffice,
    CancellationToken cancellationToken = default);
```

---

## 8. Resources

- [DocFX Documentation](https://dotnet.github.io/docfx/)
- [XML Documentation Comments (C#)](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/)
- [Swashbuckle Documentation](https://github.com/domaindrivendev/Swashbuckle.AspNetCore)
- [Azure Functions OpenAPI Extension](https://github.com/Azure/azure-functions-openapi-extension)

---

**Document Version**: 1.0
**Last Updated**: 2025-10-16
**Status**: Final
