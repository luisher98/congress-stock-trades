using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using CongressStockTrades.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using System.Text.Json;

namespace CongressStockTrades.Tests.Services;

public class CompareParserWithPdf
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<CommitteeRosterParser> _logger;

    public CompareParserWithPdf(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        _logger = loggerFactory.CreateLogger<CommitteeRosterParser>();
    }

    [Fact]
    public async Task CompareParserResultsWithPdfContent()
    {
        // Arrange
        var parser = new CommitteeRosterParser(_logger);
        var pdfPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "examples", "scsoal.pdf");

        using var stream = File.OpenRead(pdfPath);

        // Act - Run the parser
        var result = await parser.ParseSCSOALAsync(
            stream,
            "2025-09-16",
            "https://stcstdev6w7mva4oml.blob.core.windows.net/committee-rosters/2025-09-16/scsoal.pdf",
            "d26eb8b4cb7e066f1cf20dd6abe99d399779ae64fffbced155a7702af4c173a8");

        // Output comprehensive statistics
        _output.WriteLine("=" + new string('=', 79));
        _output.WriteLine("PARSER RESULTS ANALYSIS");
        _output.WriteLine("=" + new string('=', 79));
        _output.WriteLine($"\nTotal Committees: {result.Committees.Count}");
        _output.WriteLine($"Total Subcommittees: {result.Subcommittees.Count}");
        _output.WriteLine($"Total Members: {result.Members.Count}");
        _output.WriteLine($"Total Assignments: {result.Assignments.Count}");

        // List all committees found
        _output.WriteLine($"\n\nCOMMITTEES FOUND ({result.Committees.Count}):");
        _output.WriteLine("-" + new string('-', 79));
        foreach (var committee in result.Committees.OrderBy(c => c.Name))
        {
            _output.WriteLine($"  - {committee.Name} (key: {committee.CommitteeKey})");
        }

        // Show subcommittees by parent committee
        _output.WriteLine($"\n\nSUBCOMMITTEES BY COMMITTEE:");
        _output.WriteLine("-" + new string('-', 79));
        var subcommitteesByParent = result.Subcommittees
            .GroupBy(s => s.ParentCommitteeName)
            .OrderBy(g => g.Key);

        foreach (var group in subcommitteesByParent)
        {
            _output.WriteLine($"\n{group.Key}: ({group.Count()} subcommittees)");
            foreach (var sub in group.OrderBy(s => s.Name))
            {
                _output.WriteLine($"  - {sub.Name}");
            }
        }

        // Analyze Pete Sessions specifically
        var peteSessionsAssignments = result.Assignments
            .Where(a => a.MemberDisplayName.Contains("Pete Sessions", StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.Provenance.PageNumber)
            .ToList();

        _output.WriteLine($"\n\nPETE SESSIONS DETAILED ANALYSIS:");
        _output.WriteLine("-" + new string('-', 79));
        _output.WriteLine($"Total Assignments: {peteSessionsAssignments.Count}");

        foreach (var assignment in peteSessionsAssignments)
        {
            _output.WriteLine($"\nPage {assignment.Provenance.PageNumber}:");
            _output.WriteLine($"  Committee: {assignment.CommitteeKey}");
            if (assignment.SubcommitteeAssignmentKey != null)
            {
                var subcommittee = result.Subcommittees.FirstOrDefault(s => s.SubcommitteeKey == assignment.SubcommitteeAssignmentKey);
                _output.WriteLine($"  Subcommittee: {subcommittee?.Name ?? assignment.SubcommitteeAssignmentKey}");
            }
            _output.WriteLine($"  Role: {assignment.Role}");
            _output.WriteLine($"  Group: {assignment.Group}");
            _output.WriteLine($"  Raw: \"{assignment.Provenance.RawLine}\"");
        }

        // Find members with most assignments
        var memberAssignmentCounts = result.Assignments
            .GroupBy(a => a.MemberDisplayName)
            .Select(g => new { Member = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10);

        _output.WriteLine($"\n\nTOP 10 MEMBERS BY ASSIGNMENT COUNT:");
        _output.WriteLine("-" + new string('-', 79));
        foreach (var member in memberAssignmentCounts)
        {
            _output.WriteLine($"  {member.Member}: {member.Count} assignments");
        }

        // Verify data quality
        _output.WriteLine($"\n\nDATA QUALITY CHECKS:");
        _output.WriteLine("-" + new string('-', 79));

        // Check for orphaned subcommittees
        var orphanedSubcommittees = result.Subcommittees
            .Where(s => !result.Committees.Any(c => c.CommitteeKey == s.CommitteeKey))
            .ToList();

        if (orphanedSubcommittees.Any())
        {
            _output.WriteLine($"⚠️  Found {orphanedSubcommittees.Count} orphaned subcommittees:");
            foreach (var orphan in orphanedSubcommittees)
            {
                _output.WriteLine($"    - {orphan.Name} (parent: {orphan.ParentCommitteeName})");
            }
        }
        else
        {
            _output.WriteLine("✓ No orphaned subcommittees");
        }

        // Check for assignments without members
        var assignmentsWithoutMembers = result.Assignments
            .Where(a => !result.Members.Any(m => m.MemberKey == a.MemberKey))
            .ToList();

        if (assignmentsWithoutMembers.Any())
        {
            _output.WriteLine($"⚠️  Found {assignmentsWithoutMembers.Count} assignments without member records");
        }
        else
        {
            _output.WriteLine("✓ All assignments have corresponding member records");
        }

        // Save results to JSON for external comparison
        var jsonOutput = new
        {
            Statistics = new
            {
                Committees = result.Committees.Count,
                Subcommittees = result.Subcommittees.Count,
                Members = result.Members.Count,
                Assignments = result.Assignments.Count
            },
            PeteSessions = peteSessionsAssignments.Select(a => new
            {
                a.CommitteeKey,
                a.SubcommitteeAssignmentKey,
                a.Role,
                a.Group,
                Page = a.Provenance.PageNumber,
                Line = a.Provenance.RawLine
            }),
            CommitteeList = result.Committees.Select(c => c.Name).OrderBy(n => n),
            Warnings = result.Warnings
        };

        var jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "parser_results.json");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(jsonOutput, new JsonSerializerOptions { WriteIndented = true }));
        _output.WriteLine($"\n\nResults saved to: {jsonPath}");

        // Assertions
        Assert.True(result.Committees.Count >= 20, $"Expected at least 20 committees but found {result.Committees.Count}");
        Assert.True(result.Subcommittees.Count >= 100, $"Expected at least 100 subcommittees but found {result.Subcommittees.Count}");
        Assert.True(peteSessionsAssignments.Count >= 6, $"Expected at least 6 Pete Sessions assignments but found {peteSessionsAssignments.Count}");
    }
}