using CongressStockTrades.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CongressStockTrades.Tests.Services;

public class CommitteeRosterParserTests
{
    private readonly Mock<ILogger<CommitteeRosterParser>> _mockLogger;
    private readonly CommitteeRosterParser _parser;

    public CommitteeRosterParserTests()
    {
        _mockLogger = new Mock<ILogger<CommitteeRosterParser>>();
        _parser = new CommitteeRosterParser(_mockLogger.Object);
    }

    [Fact]
    public async Task ExtractCoverDateAsync_ShouldReturnNull_WhenPdfStreamIsEmpty()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        var result = await _parser.ExtractCoverDateAsync(stream);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ParseSCSOALAsync_ShouldReturnEmptyResult_WhenPdfStreamIsEmpty()
    {
        // Arrange
        using var stream = new MemoryStream();
        var sourceDate = "2025-01-01";
        var blobUri = "https://example.com/test.pdf";
        var pdfHash = "test-hash";

        // Act
        var result = await _parser.ParseSCSOALAsync(stream, sourceDate, blobUri, pdfHash);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Committees);
        Assert.Empty(result.Subcommittees);
        Assert.Empty(result.Members);
        Assert.Empty(result.Assignments);
    }
}
