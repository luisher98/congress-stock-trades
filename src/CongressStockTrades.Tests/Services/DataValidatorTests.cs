using CongressStockTrades.Core.Models;
using CongressStockTrades.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CongressStockTrades.Tests.Services;

public class DataValidatorTests
{
    private readonly DataValidator _validator;
    private readonly Mock<ILogger<DataValidator>> _loggerMock;

    public DataValidatorTests()
    {
        _loggerMock = new Mock<ILogger<DataValidator>>();
        _validator = new DataValidator(_loggerMock.Object);
    }

    [Fact]
    public void Validate_WithValidData_ShouldNotThrow()
    {
        // Arrange
        var document = CreateValidDocument();

        // Act
        var act = () => _validator.Validate(document, "Doe, John", "CA12");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithMissingTransactions_ShouldThrowValidationException()
    {
        // Arrange
        var document = CreateValidDocument();
        document.Transactions = new List<Transaction>();

        // Act
        var act = () => _validator.Validate(document, "Doe, John", "CA12");

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*No transactions found*");
    }

    [Fact]
    public void Validate_WithMissingFilingId_ShouldThrowValidationException()
    {
        // Arrange
        var document = CreateValidDocument();
        document.FilingId = "";

        // Act
        var act = () => _validator.Validate(document, "Doe, John", "CA12");

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*Filing ID is missing*");
    }

    [Fact]
    public void Validate_WithMissingPdfUrl_ShouldThrowValidationException()
    {
        // Arrange
        var document = CreateValidDocument();
        document.PdfUrl = "";

        // Act
        var act = () => _validator.Validate(document, "Doe, John", "CA12");

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*PDF URL is missing*");
    }

    [Fact]
    public void Validate_WithNameMismatch_ShouldThrowValidationException()
    {
        // Arrange
        var document = CreateValidDocument();

        // Act
        var act = () => _validator.Validate(document, "Smith, Jane", "CA12");

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*Data mismatch*");
    }

    [Fact]
    public void Validate_WithNormalizedNameMatch_ShouldNotThrow()
    {
        // Arrange
        var document = CreateValidDocument();
        document.Filing_Information.Name = "Hon. John Doe";

        // Act - Different format but same name
        var act = () => _validator.Validate(document, "Doe, John", "CA12");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithMissingTransactionFields_ShouldThrowValidationException()
    {
        // Arrange
        var document = CreateValidDocument();
        document.Transactions[0].Asset = "";

        // Act
        var act = () => _validator.Validate(document, "Doe, John", "CA12");

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*Asset is missing*");
    }

    [Fact]
    public void Validate_WithMissingWebsiteData_ShouldOnlyCheckTransactions()
    {
        // Arrange
        var document = CreateValidDocument();

        // Act - No expected name or office
        var act = () => _validator.Validate(document, "", "");

        // Assert
        act.Should().NotThrow();
    }

    private static TransactionDocument CreateValidDocument()
    {
        return new TransactionDocument
        {
            Id = "20250123456",
            FilingId = "20250123456",
            PdfUrl = "https://example.com/filing.pdf",
            Filing_Information = new FilingInformation
            {
                Name = "Doe, John",
                Status = "Filed",
                State_District = "CA12"
            },
            Transactions = new List<Transaction>
            {
                new Transaction
                {
                    ID_Owner = "Self",
                    Asset = "Apple Inc. - Common Stock",
                    Transaction_Type = "Purchase",
                    Date = "2025-01-15",
                    Amount = "$1,001 - $15,000"
                }
            }
        };
    }
}
