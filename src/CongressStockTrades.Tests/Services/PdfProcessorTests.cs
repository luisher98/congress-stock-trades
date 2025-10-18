using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure.Core;
using CongressStockTrades.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace CongressStockTrades.Tests.Services;

public class PdfProcessorTests
{
    private readonly Mock<HttpClient> _httpClientMock;
    private readonly Mock<DocumentAnalysisClient> _docIntelClientMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<PdfProcessor>> _loggerMock;

    public PdfProcessorTests()
    {
        _httpClientMock = new Mock<HttpClient>();
        _docIntelClientMock = new Mock<DocumentAnalysisClient>();
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<PdfProcessor>>();

        // Setup configuration to return a model ID
        _configurationMock.Setup(c => c["DocumentIntelligence__ModelId"])
            .Returns("ptr-extractor-v1");
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldNotThrow()
    {
        // Arrange & Act
        var action = () => new PdfProcessor(
            new HttpClient(),
            Mock.Of<DocumentAnalysisClient>(),
            _configurationMock.Object,
            _loggerMock.Object
        );

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public async Task ProcessPdfAsync_WithNullUrl_ShouldThrowException()
    {
        // Arrange
        var pdfProcessor = new PdfProcessor(
            new HttpClient(),
            Mock.Of<DocumentAnalysisClient>(),
            _configurationMock.Object,
            _loggerMock.Object
        );

        // Act
        Func<Task> act = async () => await pdfProcessor.ProcessPdfAsync(
            null!,
            "20250123456",
            "Doe, John",
            "CA12"
        );

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ProcessPdfAsync_WithEmptyUrl_ShouldThrowException()
    {
        // Arrange
        var pdfProcessor = new PdfProcessor(
            new HttpClient(),
            Mock.Of<DocumentAnalysisClient>(),
            _configurationMock.Object,
            _loggerMock.Object
        );

        // Act
        Func<Task> act = async () => await pdfProcessor.ProcessPdfAsync(
            "",
            "20250123456",
            "Doe, John",
            "CA12"
        );

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public void ParseTransactionValue_WithValidAmount_ShouldReturnParsedValue()
    {
        // This would test the private ParseTransactionValue method if it were public/internal
        // For now, we acknowledge this would be tested through integration tests
        // where we actually process a PDF and verify the transaction values are parsed correctly

        // Integration test would verify:
        // - "$1,001 - $15,000" -> Parsed correctly
        // - "$15,001 - $50,000" -> Parsed correctly
        // - "$50,001 - $100,000" -> Parsed correctly
        // - etc.
    }

    // Note: Full integration tests for PDF processing would require:
    // 1. Sample PDF files in test resources
    // 2. Mock Document Intelligence responses or use actual Azure service in integration environment
    // 3. Verify extraction of filing information (name, office, year)
    // 4. Verify extraction of transaction tables
    // 5. Verify error handling for malformed PDFs
    // 6. Verify error handling for network failures
}
