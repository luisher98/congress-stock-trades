using System;
using System.Collections.Generic;
using System.Linq;

// Test to verify the committee grouping logic
public class CommitteeTest
{
    public static void Main()
    {
        // Simulate Pete Sessions' assignments based on the parser report
        var assignments = new List<AssignmentDocument>
        {
            // Financial Services main committee
            new AssignmentDocument
            {
                CommitteeAssignmentKey = "financial-services",
                SubcommitteeAssignmentKey = null,
                Role = "Member"
            },
            // Financial Services subcommittees
            new AssignmentDocument
            {
                CommitteeAssignmentKey = null,
                SubcommitteeAssignmentKey = "financial-services::capital-markets",
                Role = "Member"
            },
            new AssignmentDocument
            {
                CommitteeAssignmentKey = null,
                SubcommitteeAssignmentKey = "financial-services::national-security-illicit-finance",
                Role = "Member"
            },
            // Natural Resources subcommittee (no main committee assignment)
            new AssignmentDocument
            {
                CommitteeAssignmentKey = null,
                SubcommitteeAssignmentKey = "natural-resources::oversight-and-government-reform",
                Role = "Member"
            },
            // Oversight subcommittees (no main committee assignment)
            new AssignmentDocument
            {
                CommitteeAssignmentKey = null,
                SubcommitteeAssignmentKey = "oversight-and::federal-law-enforcement",
                Role = "Chairman"
            },
            new AssignmentDocument
            {
                CommitteeAssignmentKey = null,
                SubcommitteeAssignmentKey = "oversight-and::health-care-and-financial-services",
                Role = "Member"
            }
        };

        // Apply the new logic
        var allCommitteeKeys = assignments
            .Where(a => !string.IsNullOrEmpty(a.CommitteeAssignmentKey) || !string.IsNullOrEmpty(a.SubcommitteeAssignmentKey))
            .SelectMany(a => new[] { a.CommitteeAssignmentKey, a.SubcommitteeAssignmentKey }
                .Where(key => !string.IsNullOrEmpty(key)))
            .Select(key => key!.Contains("::") ? key.Split("::")[0] : key)
            .Distinct()
            .OrderBy(key => key)
            .ToList();

        Console.WriteLine("📋 Committees:");
        
        foreach (var committeeKey in allCommitteeKeys)
        {
            // Find main committee assignment
            var mainCommitteeAssignment = assignments
                .FirstOrDefault(a => a.CommitteeAssignmentKey == committeeKey);

            if (mainCommitteeAssignment != null)
            {
                var role = mainCommitteeAssignment.Role != "Member" ? $" ({mainCommitteeAssignment.Role})" : "";
                var committeeName = ConvertCommitteeKeyToDisplayName(mainCommitteeAssignment.CommitteeAssignmentKey ?? "");
                Console.WriteLine($"• {committeeName}{role}");
            }
            else
            {
                // If no main committee assignment, show the committee name from subcommittee
                var committeeName = ConvertCommitteeKeyToDisplayName(committeeKey);
                Console.WriteLine($"• {committeeName}");
            }

            // Find all subcommittees for this main committee
            var subcommittees = assignments
                .Where(a => !string.IsNullOrEmpty(a.SubcommitteeAssignmentKey) && 
                           a.SubcommitteeAssignmentKey.StartsWith(committeeKey + "::"))
                .OrderBy(a => a.PositionOrder)
                .ToList();

            foreach (var subcommittee in subcommittees)
            {
                var subRole = subcommittee.Role != "Member" ? $" ({subcommittee.Role})" : "";
                var subcommitteeName = ConvertCommitteeKeyToDisplayName(subcommittee.SubcommitteeAssignmentKey ?? "");
                // Extract just the subcommittee name (after the ::)
                var subName = subcommitteeName.Contains("::") 
                    ? subcommitteeName.Split("::", 2)[1] 
                    : subcommitteeName;
                Console.WriteLine($"  - {subName}{subRole}");
            }
        }
    }

    private static string ConvertCommitteeKeyToDisplayName(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        
        if (key.Contains("::"))
        {
            var parts = key.Split("::");
            var committee = ConvertCommitteeKeyToDisplayName(parts[0]);
            var subcommittee = ConvertSubcommitteeKeyToDisplayName(parts[1]);
            return $"{committee}::{subcommittee}";
        }
        
        return ConvertCommitteeKeyToDisplayName(key);
    }

    private static string ConvertCommitteeKeyToDisplayName(string key)
    {
        return key switch
        {
            "financial-services" => "Financial Services",
            "natural-resources" => "Natural Resources", 
            "oversight-and" => "Oversight and Accountability",
            _ => key.Replace("-", " ").ToTitleCase()
        };
    }

    private static string ConvertSubcommitteeKeyToDisplayName(string key)
    {
        return key switch
        {
            "capital-markets" => "Capital Markets",
            "national-security-illicit-finance" => "National Security, Illicit Finance and International Financial Institutions",
            "oversight-and-government-reform" => "Oversight and Government Reform",
            "federal-law-enforcement" => "Federal Law Enforcement",
            "health-care-and-financial-services" => "Health Care and Financial Services",
            _ => key.Replace("-", " ").ToTitleCase()
        };
    }
}

public class AssignmentDocument
{
    public string? CommitteeAssignmentKey { get; set; }
    public string? SubcommitteeAssignmentKey { get; set; }
    public string Role { get; set; } = "Member";
    public int PositionOrder { get; set; }
}

public static class StringExtensions
{
    public static string ToTitleCase(this string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        var words = input.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
            }
        }
        return string.Join(" ", words);
    }
}
