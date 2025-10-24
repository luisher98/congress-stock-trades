using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CongressStockTrades.Core.Models;
using CongressStockTrades.Core.Services;
using CongressStockTrades.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace CongressStockTrades.Tests.Services;

public class TelegramNotificationServiceTests
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<ILogger<TelegramNotificationService>> _mockLogger;
    private readonly Mock<ICommitteeRosterRepository> _mockRepository;

    public TelegramNotificationServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _mockLogger = new Mock<ILogger<TelegramNotificationService>>();
        _mockRepository = new Mock<ICommitteeRosterRepository>();
    }

    [Fact]
    public void ExtractNameParts_ShouldHandleHonorifics()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        var service = new TelegramNotificationService(config, _mockLogger.Object, _mockRepository.Object);
        
        // Act & Assert - Test the private method via reflection
        var method = typeof(TelegramNotificationService).GetMethod("ExtractNameParts", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var result = method?.Invoke(service, new object[] { "Hon. Pete Sessions" }) as (string lastName, string firstName)?;
        
        Assert.NotNull(result);
        Assert.Equal("Sessions", result.Value.lastName);
        Assert.Equal("Pete", result.Value.firstName);
    }

    [Fact]
    public void ExtractNameParts_ShouldHandleLastNameFirstFormat()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        var service = new TelegramNotificationService(config, _mockLogger.Object, _mockRepository.Object);
        
        // Act & Assert - Test the private method via reflection
        var method = typeof(TelegramNotificationService).GetMethod("ExtractNameParts", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var result = method?.Invoke(service, new object[] { "Sessions, Pete" }) as (string lastName, string firstName)?;
        
        Assert.NotNull(result);
        Assert.Equal("Sessions", result.Value.lastName);
        Assert.Equal("Pete", result.Value.firstName);
    }

    [Fact]
    public void ExtractNameParts_ShouldHandleFirstNameLastFormat()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        var service = new TelegramNotificationService(config, _mockLogger.Object, _mockRepository.Object);
        
        // Act & Assert - Test the private method via reflection
        var method = typeof(TelegramNotificationService).GetMethod("ExtractNameParts", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var result = method?.Invoke(service, new object[] { "Pete Sessions" }) as (string lastName, string firstName)?;
        
        Assert.NotNull(result);
        Assert.Equal("Sessions", result.Value.lastName);
        Assert.Equal("Pete", result.Value.firstName);
    }

    [Fact]
    public async Task SendTransactionNotificationAsync_ShouldFindMemberInDatabase()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Telegram__BotToken"] = "test-token",
                ["Telegram__ChatId"] = "test-chat"
            })
            .Build();

        var mockMember = new MemberDocument
        {
            Id = "pete-sessions-tx",
            MemberKey = "pete-sessions-tx",
            DisplayName = "Pete Sessions, TX",
            State = "TX",
            District = null,
            Provenance = new CommitteeProvenance
            {
                SourceDate = "2025-09-16",
                PageNumber = 1,
                BlobUri = "test-uri",
                PdfHash = "test-hash"
            }
        };

        var mockAssignments = new List<AssignmentDocument>
        {
            new AssignmentDocument
            {
                Id = "assignment1",
                CommitteeKey = "financial-services",
                AssignmentKey = "assignment1",
                MemberKey = "pete-sessions-tx",
                MemberDisplayName = "Pete Sessions, TX",
                CommitteeAssignmentKey = "financial-services",
                SubcommitteeAssignmentKey = null,
                Role = "Member",
                Group = "Majority",
                PositionOrder = 3,
                Provenance = new AssignmentProvenance
                {
                    SourceDate = "2025-09-16",
                    PageNumber = 23,
                    BlobUri = "test-uri",
                    PdfHash = "test-hash",
                    RawLine = "3. Pete Sessions, TX"
                }
            },
            new AssignmentDocument
            {
                Id = "assignment2",
                CommitteeKey = "financial-services",
                AssignmentKey = "assignment2",
                MemberKey = "pete-sessions-tx",
                MemberDisplayName = "Pete Sessions, TX",
                CommitteeAssignmentKey = null,
                SubcommitteeAssignmentKey = "financial-services::capital-markets",
                Role = "Member",
                Group = "Majority",
                PositionOrder = 0,
                Provenance = new AssignmentProvenance
                {
                    SourceDate = "2025-09-16",
                    PageNumber = 24,
                    BlobUri = "test-uri",
                    PdfHash = "test-hash",
                    RawLine = "Pete Sessions, TX Juan Vargas, CA"
                }
            },
            new AssignmentDocument
            {
                Id = "assignment3",
                CommitteeKey = "oversight-and",
                AssignmentKey = "assignment3",
                MemberKey = "pete-sessions-tx",
                MemberDisplayName = "Pete Sessions, TX",
                CommitteeAssignmentKey = "oversight-and",
                SubcommitteeAssignmentKey = null,
                Role = "Member",
                Group = "Majority",
                PositionOrder = 1,
                Provenance = new AssignmentProvenance
                {
                    SourceDate = "2025-09-16",
                    PageNumber = 40,
                    BlobUri = "test-uri",
                    PdfHash = "test-hash",
                    RawLine = "Pete Sessions, TX"
                }
            },
            new AssignmentDocument
            {
                Id = "assignment4",
                CommitteeKey = "oversight-and",
                AssignmentKey = "assignment4",
                MemberKey = "pete-sessions-tx",
                MemberDisplayName = "Pete Sessions, TX",
                CommitteeAssignmentKey = null,
                SubcommitteeAssignmentKey = "oversight-and::federal-law-enforcement",
                Role = "Chairman",
                Group = "Majority",
                PositionOrder = 0,
                Provenance = new AssignmentProvenance
                {
                    SourceDate = "2025-09-16",
                    PageNumber = 40,
                    BlobUri = "test-uri",
                    PdfHash = "test-hash",
                    RawLine = "Pete Sessions, TX, Chairman Kweisi Mfume, MD"
                }
            }
        };

        _mockRepository.Setup(x => x.FindMemberByNameAsync("Sessions", "Pete", default))
            .ReturnsAsync(mockMember);
        
        _mockRepository.Setup(x => x.GetMemberAssignmentsAsync("pete-sessions-tx", default))
            .ReturnsAsync(mockAssignments);

        var service = new TelegramNotificationService(config, _mockLogger.Object, _mockRepository.Object);

        var transaction = new TransactionDocument
        {
            Id = "test-filing",
            FilingId = "test-filing",
            PdfUrl = "https://disclosures-clerk.house.gov/public_disc/ptr-pdfs/2025/20033330.pdf",
            Filing_Information = new FilingInformation
            {
                Name = "Hon. Pete Sessions",
                Status = "Filed",
                State_District = "TX17",
                Party = "Republican"
            },
            Filing_Date = "10/22/2025",
            Transactions = new List<Transaction>
            {
                new Transaction
                {
                    Asset = "VLG FL CDD #14 SPL ASSMNT REV DUE 05/01/2053 05.500%",
                    Transaction_Type = "P",
                    Date = "10/24/2025",
                    Amount = "$15,001 - $50,000"
                }
            },
            Investment_Vehicles = new List<string> { "FMS (Owner: JT)" }
        };

        // Act
        await service.SendTransactionNotificationAsync(transaction);

        // Assert
        _mockRepository.Verify(x => x.FindMemberByNameAsync("Sessions", "Pete", default), Times.Once);
        _mockRepository.Verify(x => x.GetMemberAssignmentsAsync("pete-sessions-tx", default), Times.Once);
    }

    [Fact]
    public async Task SendTransactionNotificationAsync_ShouldFormatCommitteesHierarchically()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"Telegram__BotToken", "test_token"},
                {"Telegram__ChatId", "test_chat_id"}
            })
            .Build();

        var mockMember = new MemberDocument
        {
            Id = "pete-sessions-tx",
            MemberKey = "pete-sessions-tx",
            DisplayName = "Pete Sessions, TX",
            State = "TX",
            District = "17",
            Provenance = new CommitteeProvenance
            {
                SourceDate = "2025-09-16",
                PageNumber = 1,
                BlobUri = "test-uri",
                PdfHash = "test-hash"
            }
        };

        // Create assignments that should be grouped hierarchically
        var mockAssignments = new List<AssignmentDocument>
        {
            // Financial Services main committee
            new AssignmentDocument
            {
                Id = "assignment1",
                CommitteeKey = "financial-services",
                AssignmentKey = "assignment1",
                MemberKey = "pete-sessions-tx",
                MemberDisplayName = "Pete Sessions, TX",
                CommitteeAssignmentKey = "financial-services",
                SubcommitteeAssignmentKey = null,
                Role = "Member",
                Group = "Majority",
                PositionOrder = 3,
                Provenance = new AssignmentProvenance
                {
                    SourceDate = "2025-09-16",
                    PageNumber = 23,
                    BlobUri = "test-uri",
                    PdfHash = "test-hash",
                    RawLine = "3. Pete Sessions, TX"
                }
            },
            // Financial Services subcommittees
            new AssignmentDocument
            {
                Id = "assignment2",
                CommitteeKey = "financial-services",
                AssignmentKey = "assignment2",
                MemberKey = "pete-sessions-tx",
                MemberDisplayName = "Pete Sessions, TX",
                CommitteeAssignmentKey = null,
                SubcommitteeAssignmentKey = "financial-services::capital-markets",
                Role = "Member",
                Group = "Majority",
                PositionOrder = 0,
                Provenance = new AssignmentProvenance
                {
                    SourceDate = "2025-09-16",
                    PageNumber = 24,
                    BlobUri = "test-uri",
                    PdfHash = "test-hash",
                    RawLine = "Pete Sessions, TX Juan Vargas, CA"
                }
            },
            new AssignmentDocument
            {
                Id = "assignment3",
                CommitteeKey = "financial-services",
                AssignmentKey = "assignment3",
                MemberKey = "pete-sessions-tx",
                MemberDisplayName = "Pete Sessions, TX",
                CommitteeAssignmentKey = null,
                SubcommitteeAssignmentKey = "financial-services::national-security-illicit-finance",
                Role = "Member",
                Group = "Majority",
                PositionOrder = 0,
                Provenance = new AssignmentProvenance
                {
                    SourceDate = "2025-09-16",
                    PageNumber = 25,
                    BlobUri = "test-uri",
                    PdfHash = "test-hash",
                    RawLine = "Pete Sessions, TX Bill Foster, IL"
                }
            },
            // Oversight main committee
            new AssignmentDocument
            {
                Id = "assignment4",
                CommitteeKey = "oversight-and",
                AssignmentKey = "assignment4",
                MemberKey = "pete-sessions-tx",
                MemberDisplayName = "Pete Sessions, TX",
                CommitteeAssignmentKey = "oversight-and",
                SubcommitteeAssignmentKey = null,
                Role = "Member",
                Group = "Majority",
                PositionOrder = 1,
                Provenance = new AssignmentProvenance
                {
                    SourceDate = "2025-09-16",
                    PageNumber = 40,
                    BlobUri = "test-uri",
                    PdfHash = "test-hash",
                    RawLine = "Pete Sessions, TX"
                }
            },
            // Oversight subcommittees
            new AssignmentDocument
            {
                Id = "assignment5",
                CommitteeKey = "oversight-and",
                AssignmentKey = "assignment5",
                MemberKey = "pete-sessions-tx",
                MemberDisplayName = "Pete Sessions, TX",
                CommitteeAssignmentKey = null,
                SubcommitteeAssignmentKey = "oversight-and::federal-law-enforcement",
                Role = "Chairman",
                Group = "Majority",
                PositionOrder = 0,
                Provenance = new AssignmentProvenance
                {
                    SourceDate = "2025-09-16",
                    PageNumber = 40,
                    BlobUri = "test-uri",
                    PdfHash = "test-hash",
                    RawLine = "Pete Sessions, TX, Chairman Kweisi Mfume, MD"
                }
            },
            new AssignmentDocument
            {
                Id = "assignment6",
                CommitteeKey = "oversight-and",
                AssignmentKey = "assignment6",
                MemberKey = "pete-sessions-tx",
                MemberDisplayName = "Pete Sessions, TX",
                CommitteeAssignmentKey = null,
                SubcommitteeAssignmentKey = "oversight-and::health-care-and-financial-services",
                Role = "Member",
                Group = "Majority",
                PositionOrder = 0,
                Provenance = new AssignmentProvenance
                {
                    SourceDate = "2025-09-16",
                    PageNumber = 41,
                    BlobUri = "test-uri",
                    PdfHash = "test-hash",
                    RawLine = "Pete Sessions, TX Wesley Bell, MO"
                }
            }
        };

        _mockRepository.Setup(x => x.FindMemberByNameAsync("Sessions", "Pete", default))
            .ReturnsAsync(mockMember);
        
        _mockRepository.Setup(x => x.GetMemberAssignmentsAsync("pete-sessions-tx", default))
            .ReturnsAsync(mockAssignments);

        var service = new TelegramNotificationService(config, _mockLogger.Object, _mockRepository.Object);

        var transaction = new TransactionDocument
        {
            Id = "test-filing",
            FilingId = "test-filing",
            PdfUrl = "https://disclosures-clerk.house.gov/public_disc/ptr-pdfs/2025/20033330.pdf",
            Filing_Information = new FilingInformation
            {
                Name = "Hon. Pete Sessions",
                Status = "Filed",
                State_District = "TX17",
                Party = "Republican"
            },
            Filing_Date = "10/22/2025",
            Transactions = new List<Transaction>
            {
                new Transaction
                {
                    Asset = "VLG FL CDD #14 SPL ASSMNT REV DUE 05/01/2053 05.500%",
                    Transaction_Type = "P",
                    Date = "10/24/2025",
                    Amount = "$15,001 - $50,000"
                }
            },
            Investment_Vehicles = new List<string> { "FMS (Owner: JT)" }
        };

        // Act
        await service.SendTransactionNotificationAsync(transaction);

        // Assert
        _mockRepository.Verify(x => x.FindMemberByNameAsync("Sessions", "Pete", default), Times.Once);
        _mockRepository.Verify(x => x.GetMemberAssignmentsAsync("pete-sessions-tx", default), Times.Once);
        
        // Verify that the service found all 6 assignments
        _mockLogger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Found 6 committee assignments for Pete Sessions, TX")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }
}
