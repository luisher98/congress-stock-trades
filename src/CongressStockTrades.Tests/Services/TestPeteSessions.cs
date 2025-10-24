using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CongressStockTrades.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace CongressStockTrades.Tests.Services;

public class TestPeteSessions
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<CommitteeRosterParser> _logger;

    public TestPeteSessions(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        _logger = loggerFactory.CreateLogger<CommitteeRosterParser>();
    }

    [Fact]
    public async Task ParsePeteSessionsAssignments()
    {
        // Arrange
        var parser = new CommitteeRosterParser(_logger);
        var pdfPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "examples", "scsoal.pdf");

        _output.WriteLine($"PDF Path: {pdfPath}");
        _output.WriteLine($"PDF Exists: {File.Exists(pdfPath)}");

        using var stream = File.OpenRead(pdfPath);

        // Act
        var result = await parser.ParseSCSOALAsync(
            stream,
            "2025-09-16",
            "https://stcstdev6w7mva4oml.blob.core.windows.net/committee-rosters/2025-09-16/scsoal.pdf",
            "d26eb8b4cb7e066f1cf20dd6abe99d399779ae64fffbced155a7702af4c173a8");

        // Assert and output results
        _output.WriteLine($"\nTotal Committees: {result.Committees.Count}");
        _output.WriteLine($"Total Subcommittees: {result.Subcommittees.Count}");
        _output.WriteLine($"Total Members: {result.Members.Count}");
        _output.WriteLine($"Total Assignments: {result.Assignments.Count}");

        // Find Pete Sessions assignments
        var peteSessionsAssignments = result.Assignments
            .Where(a => a.MemberDisplayName.Contains("Sessions", StringComparison.OrdinalIgnoreCase))
            .ToList();

        _output.WriteLine($"\n\nPete Sessions Assignments Found: {peteSessionsAssignments.Count}");
        _output.WriteLine("=" + new string('=', 79));
        int assignmentNumber = 1;
        foreach (var assignment in peteSessionsAssignments.OrderBy(a => a.Provenance.PageNumber))
        {
            _output.WriteLine($"\n{assignmentNumber}. Page {assignment.Provenance.PageNumber}:");
            _output.WriteLine($"   Committee: {assignment.CommitteeKey}");
            if (assignment.SubcommitteeAssignmentKey != null)
                _output.WriteLine($"   Subcommittee: {assignment.SubcommitteeAssignmentKey}");
            else
                _output.WriteLine($"   Type: Main Committee Assignment");
            _output.WriteLine($"   Role: {assignment.Role}, Group: {assignment.Group}");
            _output.WriteLine($"   Raw Line: \"{assignment.Provenance.RawLine}\"");
            assignmentNumber++;
        }

        // Output count for verification
        _output.WriteLine($"\n\nFinal Count: Pete Sessions has {peteSessionsAssignments.Count} total assignments");

        // We expect at least 5-7 assignments based on PDF analysis
        Assert.True(peteSessionsAssignments.Count >= 5,
            $"Expected at least 5 Pete Sessions assignments but found {peteSessionsAssignments.Count}");
    }
}