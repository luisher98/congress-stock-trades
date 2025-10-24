using System;
using System.IO;
using System.Threading.Tasks;
using CongressStockTrades.Infrastructure.Services;
using Microsoft.Extensions.Logging;

class TestParser
{
    static async Task Main()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        var logger = loggerFactory.CreateLogger<CommitteeRosterParser>();
        var parser = new CommitteeRosterParser(logger);

        var pdfPath = "/Users/Luis/dev/congress-stock-trades/examples/scsoal.pdf";
        using var stream = File.OpenRead(pdfPath);

        var result = await parser.ParseSCSOALAsync(
            stream,
            "2025-09-16",
            "https://stcstdev6w7mva4oml.blob.core.windows.net/committee-rosters/2025-09-16/scsoal.pdf",
            "d26eb8b4cb7e066f1cf20dd6abe99d399779ae64fffbced155a7702af4c173a8");

        Console.WriteLine($"\nTotal Committees: {result.Committees.Count}");
        Console.WriteLine($"Total Subcommittees: {result.Subcommittees.Count}");
        Console.WriteLine($"Total Members: {result.Members.Count}");
        Console.WriteLine($"Total Assignments: {result.Assignments.Count}");

        // Find Pete Sessions assignments
        var peteSessionsAssignments = result.Assignments
            .Where(a => a.MemberDisplayName.Contains("Sessions", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Console.WriteLine($"\n\nPete Sessions Assignments: {peteSessionsAssignments.Count}");
        foreach (var assignment in peteSessionsAssignments)
        {
            Console.WriteLine($"  Committee: {assignment.CommitteeKey}");
            if (assignment.SubcommitteeAssignmentKey != null)
                Console.WriteLine($"    Subcommittee: {assignment.SubcommitteeAssignmentKey}");
            Console.WriteLine($"    Role: {assignment.Role}, Group: {assignment.Group}");
            Console.WriteLine($"    Page: {assignment.Provenance.PageNumber}, Line: {assignment.Provenance.RawLine}");
            Console.WriteLine();
        }
    }
}