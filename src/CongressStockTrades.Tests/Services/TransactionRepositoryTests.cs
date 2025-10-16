using CongressStockTrades.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace CongressStockTrades.Tests.Services;

public class TransactionRepositoryTests
{
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<TransactionRepository>> _loggerMock;

    public TransactionRepositoryTests()
    {
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<TransactionRepository>>();

        // Setup configuration mock to return test values
        // Use a valid base64 key format that Cosmos DB expects
        var validBase64Key = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("test-key-for-cosmos-db-that-is-long-enough"));
        _configurationMock.Setup(c => c["CosmosDb__Endpoint"]).Returns("https://test.documents.azure.com:443/");
        _configurationMock.Setup(c => c["CosmosDb__Key"]).Returns(validBase64Key);
        _configurationMock.Setup(c => c["CosmosDb__DatabaseName"]).Returns("CongressTrades");
    }

    [Fact]
    public void Constructor_WithValidConfiguration_ShouldNotThrow()
    {
        // Arrange & Act
        var action = () => new TransactionRepository(
            _configurationMock.Object,
            _loggerMock.Object
        );

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithMissingEndpoint_ShouldThrow()
    {
        // Arrange
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["CosmosDb__Endpoint"]).Returns((string?)null);
        config.Setup(c => c["CosmosDb__Key"]).Returns("test-key");

        // Act
        var action = () => new TransactionRepository(config.Object, _loggerMock.Object);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_WithMissingKey_ShouldThrow()
    {
        // Arrange
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["CosmosDb__Endpoint"]).Returns("https://test.documents.azure.com:443/");
        config.Setup(c => c["CosmosDb__Key"]).Returns((string?)null);

        // Act
        var action = () => new TransactionRepository(config.Object, _loggerMock.Object);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    // Note: Full integration tests would require:
    // 1. Cosmos DB emulator or test account
    // 2. Test data seeding
    // 3. Verification of:
    //    - Document creation with correct partition keys (StoreTransactionAsync)
    //    - Checking if filing is processed (IsFilingProcessedAsync)
    //    - Marking filing as processed (MarkAsProcessedAsync)
    //    - Query results ordering and filtering (GetLatestTransactionAsync)
    //    - Error handling for network/service failures
    //    - Duplicate filing detection (409 Conflict handling)
}
