using CongressStockTrades.Core.Models;
using CongressStockTrades.Core.Services;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace CongressStockTrades.Infrastructure.Services;

public class CommitteeRosterParser : ICommitteeRosterParser
{
    private readonly ILogger<CommitteeRosterParser> _logger;

    // Regex patterns
    // Match date with any prefix text (e.g., "https://clerk.house.govSEPTEMBER 16, 2025")
    // Looks for: (optionaltext)(MONTH) (DAY), (YEAR)
    private static readonly Regex CoverDatePattern = new(@"(JANUARY|FEBRUARY|MARCH|APRIL|MAY|JUNE|JULY|AUGUST|SEPTEMBER|OCTOBER|NOVEMBER|DECEMBER)\s+(\d{1,2}),\s+(\d{4})", RegexOptions.Compiled |  RegexOptions.IgnoreCase);
    // Committee headers are single words or phrases in all caps (e.g., "AGRICULTURE", "ARMED SERVICES")
    // Must be at least 3 chars and not match common non-committee phrases
    private static readonly Regex CommitteeHeaderPattern = new(@"^([A-Z][A-Z\s,&'-]+)$", RegexOptions.Compiled);
    private static readonly Regex SubcommitteeHeaderPattern = new(@"^SUBCOMMITTEES?\s+OF\s+THE\s+COMMITTEE\s+ON\s+(.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MemberLinePattern = new(@"^\s*(\d+)\.\s*(.+?)(?:,\s*of\s+([A-Z][a-z\s]+))?(?:,\s*(.+))?$", RegexOptions.Compiled);
    private static readonly Regex SubcommitteeMemberLinePattern = new(@"^([A-Z][a-zA-Z\.\s]+(?:,\s*(?:Jr\.|Sr\.|III|IV|V))?)\s*,\s*([A-Z]{2})(?:\s*,\s*(.+))?", RegexOptions.Compiled);
    private static readonly Regex RolePattern = new(@"\b(Chair|Ranking Member|Vice Chair|Ex Officio|Chairman)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // List of known standing committee names (used to detect when we've left a subcommittee section)
    private static readonly HashSet<string> KnownStandingCommittees = new(StringComparer.OrdinalIgnoreCase)
    {
        "AGRICULTURE",
        "APPROPRIATIONS",
        "ARMED SERVICES",
        "BUDGET",
        "EDUCATION AND THE WORKFORCE",
        "ENERGY AND COMMERCE",
        "ETHICS",
        "FINANCIAL SERVICES",
        "FOREIGN AFFAIRS",
        "HOMELAND SECURITY",
        "HOUSE ADMINISTRATION",
        "JUDICIARY",
        "NATURAL RESOURCES",
        "OVERSIGHT AND ACCOUNTABILITY",
        "RULES",
        "SCIENCE, SPACE, AND TECHNOLOGY",
        "SMALL BUSINESS",
        "TRANSPORTATION AND INFRASTRUCTURE",
        "VETERANS' AFFAIRS",
        "WAYS AND MEANS"
    };

    // Known Select Committees
    private static readonly HashSet<string> KnownSelectCommittees = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT COMMITTEE ON THE CHINESE COMMUNIST PARTY",
        "SELECT COMMITTEE ON THE STRATEGIC COMPETITION BETWEEN THE UNITED STATES AND THE CHINESE COMMUNIST PARTY",
        "SELECT COMMITTEE ON THE MODERNIZATION OF CONGRESS"
    };

    // Known Joint Committees
    private static readonly HashSet<string> KnownJointCommittees = new(StringComparer.OrdinalIgnoreCase)
    {
        "JOINT COMMITTEE ON TAXATION",
        "JOINT COMMITTEE ON THE LIBRARY",
        "JOINT ECONOMIC COMMITTEE",
        "JOINT COMMITTEE ON PRINTING"
    };

    public CommitteeRosterParser(ILogger<CommitteeRosterParser> logger)
    {
        _logger = logger;
    }

    public Task<string?> ExtractCoverDateAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting cover date from PDF");

        if (pdfStream.CanSeek)
        {
            pdfStream.Position = 0;
        }

        try
        {
            using var document = PdfDocument.Open(pdfStream, new UglyToad.PdfPig.ParsingOptions { ClipPaths = true });

            if (document.NumberOfPages < 1)
            {
                _logger.LogWarning("PDF has no pages");
                return Task.FromResult<string?>(null);
            }

            var firstPage = document.GetPage(1);
            var text = firstPage.Text;

            _logger.LogInformation("First 500 characters of PDF page 1: {Text}", text.Substring(0, Math.Min(500, text.Length)));

            var match = CoverDatePattern.Match(text);
            if (match.Success)
            {
                var monthName = match.Groups[1].Value; // Month name (e.g., "September")
                var day = int.Parse(match.Groups[2].Value);
                var year = int.Parse(match.Groups[3].Value);

                try
                {
                    var month = DateTime.ParseExact(monthName, "MMMM", CultureInfo.InvariantCulture).Month;
                    var date = new DateTime(year, month, day);
                    var formattedDate = date.ToString("yyyy-MM-dd");

                    _logger.LogInformation("Extracted cover date: {SourceDate}", formattedDate);
                    return Task.FromResult<string?>(formattedDate);
                }
                catch (FormatException ex)
                {
                    _logger.LogWarning("Failed to parse month name '{MonthName}': {Error}", monthName, ex.Message);
                }
            }

            _logger.LogWarning("Could not extract cover date from PDF");
            return Task.FromResult<string?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open or parse PDF document");
            return Task.FromResult<string?>(null);
        }
    }

    public Task<CommitteeRosterParseResult> ParseSCSOALAsync(
        Stream pdfStream,
        string sourceDate,
        string blobUri,
        string pdfHash,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Parsing SCSOAL PDF for source date {SourceDate}", sourceDate);

        if (pdfStream.CanSeek)
        {
            pdfStream.Position = 0;
        }

        var result = new CommitteeRosterParseResult
        {
            Committees = new List<CommitteeDocument>(),
            Subcommittees = new List<SubcommitteeDocument>(),
            Members = new List<MemberDocument>(),
            Assignments = new List<AssignmentDocument>()
        };

        try
        {
            using var document = PdfDocument.Open(pdfStream, new UglyToad.PdfPig.ParsingOptions { ClipPaths = true });

        _logger.LogInformation("PDF has {PageCount} pages", document.NumberOfPages);

        // Parse page by page
        string? currentCommitteeName = null;
        string? currentCommitteeKey = null;
        string? currentSubcommitteeName = null;
        string? currentSubcommitteeKey = null;
        bool inMajoritySection = true;
        bool inSubcommitteeSection = false; // Track when we're in a subcommittees section
        int pageNumber = 0;

        foreach (var page in document.GetPages())
        {
            pageNumber++;
            var lines = GetLinesFromPage(page);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Skip empty lines
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                // Check for main committee listing section header (resets subcommittee mode)
                if (trimmed.Contains("ALPHABETICAL LIST OF STANDING COMMITTEES", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Contains("STANDING COMMITTEES", StringComparison.OrdinalIgnoreCase) && trimmed.Length < 30)
                {
                    inSubcommitteeSection = false;
                    _logger.LogDebug("Entering main committee listing section");
                    continue;
                }

                // Check for subcommittee section header FIRST (e.g., "SUBCOMMITTEES OF THE COMMITTEE ON AGRICULTURE")
                // This must come before the general all-caps pattern check
                var subcommitteeMatch = SubcommitteeHeaderPattern.Match(trimmed);
                if (subcommitteeMatch.Success)
                {
                    // Extract parent committee name from the header
                    var parentCommitteeName = subcommitteeMatch.Groups[1].Value.Trim();

                    // Fix truncated committee names by mapping them to their full names
                    var committeeNameMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "OVERSIGHT AND", "OVERSIGHT AND ACCOUNTABILITY" },
                        { "SCIENCE, SPACE, AND", "SCIENCE, SPACE, AND TECHNOLOGY" },
                        { "EDUCATION AND THE", "EDUCATION AND THE WORKFORCE" },
                        { "TRANSPORTATION AND", "TRANSPORTATION AND INFRASTRUCTURE" }
                    };

                    // Check if we need to fix a truncated name
                    foreach (var mapping in committeeNameMappings)
                    {
                        if (parentCommitteeName.Equals(mapping.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            parentCommitteeName = mapping.Value;
                            _logger.LogDebug("Fixed truncated committee name: {OldName} -> {NewName}", mapping.Key, mapping.Value);
                            break;
                        }
                    }

                    var parentCommitteeKey = NormalizeName(parentCommitteeName);

                    // Ensure the parent committee exists in our collection
                    if (!result.Committees.Any(c => c.CommitteeKey == parentCommitteeKey))
                    {
                        var committee = new CommitteeDocument
                        {
                            Id = parentCommitteeKey,
                            CommitteeKey = parentCommitteeKey,
                            Name = parentCommitteeName,
                            Chamber = "House",
                            Type = "Standing",
                            Provenance = new CommitteeProvenance
                            {
                                SourceDate = sourceDate,
                                PageNumber = pageNumber,
                                BlobUri = blobUri,
                                PdfHash = pdfHash
                            }
                        };
                        result.Committees.Add(committee);
                        _logger.LogDebug("Implicitly created parent committee from subcommittee section: {CommitteeName}", parentCommitteeName);
                    }

                    // Set current committee context to the parent committee
                    currentCommitteeName = parentCommitteeName;
                    currentCommitteeKey = parentCommitteeKey;
                    currentSubcommitteeName = null;
                    currentSubcommitteeKey = null;
                    inSubcommitteeSection = true;
                    inMajoritySection = true;

                    _logger.LogDebug("Entering subcommittee section for committee: {CommitteeName}", parentCommitteeName);
                    continue;
                }

                // Check for committee/subcommittee header (all-caps line)
                var committeeMatch = CommitteeHeaderPattern.Match(trimmed);
                if (committeeMatch.Success)
                {
                    var potentialName = committeeMatch.Groups[1].Value.Trim();

                    // Filter out common non-committee phrases
                    var ignorePatterns = new[]
                    {
                        "STANDING COMMITTEES",
                        "SELECT COMMITTEES",
                        "JOINT COMMITTEES",
                        "ALPHABETICAL LIST",
                        "HOUSE OF REPRESENTATIVES",
                        "UNITED STATES",
                        "ONE HUNDRED",
                        "CONGRESS",
                        "Ratio",
                        "AND THEIR",
                        "SUBCOMMITTEES",
                        "OF THE",
                        "Republicans in",
                        "Democrats in",
                        "Delegates in",
                        "WASHINGTON",
                        "Prepared under",
                        "[The chairman",  // Skip bracketed notes
                        "CONTENTS"
                    };

                    if (ignorePatterns.Any(p => potentialName.Contains(p, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    // Additional validation: names should be reasonably short but not too short
                    if (potentialName.Length < 4 || potentialName.Length > 100)
                    {
                        continue;
                    }

                    // Skip single words that are likely not committees
                    var singleWordExclusions = new[] { "AND", "THE", "OF", "IN", "TO", "FOR", "WITH" };
                    if (singleWordExclusions.Contains(potentialName, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Check if this is a known standing, select, or joint committee
                    bool isKnownStandingCommittee = KnownStandingCommittees.Contains(potentialName);
                    bool isSelectCommittee = potentialName.Contains("SELECT COMMITTEE", StringComparison.OrdinalIgnoreCase) ||
                                            KnownSelectCommittees.Any(sc => potentialName.Contains(sc, StringComparison.OrdinalIgnoreCase));
                    bool isJointCommittee = potentialName.Contains("JOINT COMMITTEE", StringComparison.OrdinalIgnoreCase) ||
                                           KnownJointCommittees.Any(jc => potentialName.Contains(jc, StringComparison.OrdinalIgnoreCase));

                    bool isMainCommittee = isKnownStandingCommittee || isSelectCommittee || isJointCommittee;

                    // Determine if this is a subcommittee or main committee
                    if (inSubcommitteeSection && currentCommitteeKey != null && !isMainCommittee)
                    {
                        // This is a subcommittee name
                        currentSubcommitteeName = potentialName;
                        currentSubcommitteeKey = $"{currentCommitteeKey}::{NormalizeName(currentSubcommitteeName)}";
                        inMajoritySection = true;

                        var subcommittee = new SubcommitteeDocument
                        {
                            Id = currentSubcommitteeKey,
                            CommitteeKey = currentCommitteeKey,
                            SubcommitteeKey = currentSubcommitteeKey,
                            ParentCommitteeName = currentCommitteeName!,
                            Name = currentSubcommitteeName,
                            Provenance = new CommitteeProvenance
                            {
                                SourceDate = sourceDate,
                                PageNumber = pageNumber,
                                BlobUri = blobUri,
                                PdfHash = pdfHash
                            }
                        };

                        result.Subcommittees.Add(subcommittee);
                        _logger.LogDebug("Found subcommittee: {SubcommitteeName} under {ParentCommittee}", currentSubcommitteeName, currentCommitteeName);
                    }
                    else
                    {
                        // This is a main committee
                        currentCommitteeName = potentialName;
                        currentCommitteeKey = NormalizeName(currentCommitteeName);
                        currentSubcommitteeName = null;
                        currentSubcommitteeKey = null;
                        inMajoritySection = true;
                        inSubcommitteeSection = false; // Reset subcommittee flag when we hit a new main committee

                        // Add committee (avoid duplicates)
                        if (!result.Committees.Any(c => c.CommitteeKey == currentCommitteeKey))
                        {
                            // Determine committee type
                            string committeeType = "Standing"; // Default
                            if (isSelectCommittee)
                                committeeType = "Select";
                            else if (isJointCommittee)
                                committeeType = "Joint";

                            var committee = new CommitteeDocument
                            {
                                Id = currentCommitteeKey,
                                CommitteeKey = currentCommitteeKey,
                                Name = currentCommitteeName,
                                Chamber = "House",
                                Type = committeeType,
                                Provenance = new CommitteeProvenance
                                {
                                    SourceDate = sourceDate,
                                    PageNumber = pageNumber,
                                    BlobUri = blobUri,
                                    PdfHash = pdfHash
                                }
                            };

                            result.Committees.Add(committee);
                            _logger.LogDebug("Found committee: {CommitteeName}", currentCommitteeName);
                        }
                    }

                    continue;
                }

                // Check for section markers
                if (trimmed.Contains("MAJORITY", StringComparison.OrdinalIgnoreCase))
                {
                    inMajoritySection = true;
                    continue;
                }
                if (trimmed.Contains("MINORITY", StringComparison.OrdinalIgnoreCase))
                {
                    inMajoritySection = false;
                    continue;
                }

                // Fix concatenated names (e.g., "PeteSessions,TX" -> "Pete Sessions, TX")
                var fixedLine = FixConcatenatedNames(trimmed);

                // Parse member line - check if it contains two members (majority and minority on same line)
                // Pattern: "14. Carlos A. Gimenez, FL 14. Marilyn Strickland, WA"
                // Always check for dual members first by looking for multiple position numbers
                var splitPattern = new Regex(@"(\d+\.\s*.+?)(?=\s*\d+\.|$)");
                var splits = splitPattern.Matches(fixedLine);

                List<string> memberLines = new();
                if (splits.Count > 1)
                {
                    // Line contains multiple position numbers - split into separate members
                    _logger.LogDebug("Found {Count} members on one line: {Line}", splits.Count, trimmed);
                    foreach (Match split in splits)
                    {
                        memberLines.Add(split.Groups[1].Value.Trim());
                    }
                }
                else
                {
                    // Single member or no match - process as-is
                    memberLines.Add(fixedLine);
                }

                // Process each member line (could be 1 or 2 members)
                bool isMajority = inMajoritySection;
                foreach (var memberLine in memberLines)
                {
                    var memberMatch = MemberLinePattern.Match(memberLine);

                    // If regular pattern doesn't match and we're in a subcommittee, try subcommittee pattern
                    if (!memberMatch.Success && currentSubcommitteeKey != null)
                    {
                        // Try to parse as subcommittee member (no position number)
                        // Split by spaces to find two member names on the same line
                        var subcommitteeMembers = ParseSubcommitteeMembers(memberLine);

                        if (subcommitteeMembers.Count > 0)
                        {
                            bool firstMember = true;
                            foreach (var subMember in subcommitteeMembers)
                            {
                                var subMemberKey = NormalizeMemberKey(subMember.Name, subMember.State, null);

                                // Add member if not already exists
                                if (!result.Members.Any(m => m.MemberKey == subMemberKey))
                                {
                                    var subMemberDoc = new MemberDocument
                                    {
                                        Id = subMemberKey,
                                        MemberKey = subMemberKey,
                                        DisplayName = subMember.Name,
                                        State = subMember.State ?? "Unknown",
                                        District = null,
                                        Provenance = new CommitteeProvenance
                                        {
                                            SourceDate = sourceDate,
                                            PageNumber = pageNumber,
                                            BlobUri = blobUri,
                                            PdfHash = pdfHash
                                        }
                                    };
                                    result.Members.Add(subMemberDoc);
                                }

                                // Add assignment
                                var subAssignmentKey = $"{subMemberKey}::{currentSubcommitteeKey}::{sourceDate}";

                                var subAssignment = new AssignmentDocument
                                {
                                    Id = subAssignmentKey,
                                    CommitteeKey = currentCommitteeKey!,
                                    AssignmentKey = subAssignmentKey,
                                    MemberKey = subMemberKey,
                                    MemberDisplayName = subMember.Name,
                                    CommitteeAssignmentKey = null, // Subcommittee assignment
                                    SubcommitteeAssignmentKey = currentSubcommitteeKey,
                                    Role = subMember.Role ?? "Member",
                                    Group = firstMember ? "Majority" : "Minority", // First is majority, second is minority
                                    PositionOrder = 0, // No position for subcommittee members
                                    Provenance = new AssignmentProvenance
                                    {
                                        SourceDate = sourceDate,
                                        PageNumber = pageNumber,
                                        BlobUri = blobUri,
                                        PdfHash = pdfHash,
                                        RawLine = memberLine
                                    }
                                };

                                result.Assignments.Add(subAssignment);
                                firstMember = false;
                            }
                            continue; // Move to next line
                        }
                    }

                    if (!memberMatch.Success)
                        continue;

                    if (currentCommitteeKey == null)
                    {
                        _logger.LogWarning("Found member line without current committee on page {Page}", pageNumber);
                        continue;
                    }

                    var position = int.Parse(memberMatch.Groups[1].Value);
                    var name = memberMatch.Groups[2].Value.Trim();
                    var state = memberMatch.Groups[3].Value?.Trim();
                    var roleText = memberMatch.Groups[4].Value?.Trim();

                    // Extract role
                    var role = "Member";
                    if (!string.IsNullOrEmpty(roleText))
                    {
                        var roleMatch = RolePattern.Match(roleText);
                        if (roleMatch.Success)
                        {
                            role = roleMatch.Groups[1].Value;
                        }
                    }

                    // Parse state/district
                    string? district = null;
                    if (!string.IsNullOrEmpty(state))
                    {
                        var stateDistrictMatch = Regex.Match(state, @"^([A-Z]{2})(\d{2})?$");
                        if (stateDistrictMatch.Success)
                        {
                            state = stateDistrictMatch.Groups[1].Value;
                            district = stateDistrictMatch.Groups[2].Value;
                        }
                    }

                    var memberKey = NormalizeMemberKey(name, state, district);

                    // Add member if not already exists
                    if (!result.Members.Any(m => m.MemberKey == memberKey))
                    {
                        var member = new MemberDocument
                        {
                            Id = memberKey,
                            MemberKey = memberKey,
                            DisplayName = name,
                            State = state ?? "Unknown",
                            District = district,
                            Provenance = new CommitteeProvenance
                            {
                                SourceDate = sourceDate,
                                PageNumber = pageNumber,
                                BlobUri = blobUri,
                                PdfHash = pdfHash
                            }
                        };
                        result.Members.Add(member);
                    }

                    // Add assignment
                    var assignmentCommitteeKey = currentSubcommitteeKey ?? currentCommitteeKey;
                    var assignmentKey = $"{memberKey}::{assignmentCommitteeKey}::{sourceDate}";

                    var assignment = new AssignmentDocument
                    {
                        Id = assignmentKey,
                        CommitteeKey = currentCommitteeKey,
                        AssignmentKey = assignmentKey,
                        MemberKey = memberKey,
                        MemberDisplayName = name,
                        CommitteeAssignmentKey = currentSubcommitteeKey == null ? currentCommitteeKey : null,
                        SubcommitteeAssignmentKey = currentSubcommitteeKey,
                        Role = role,
                        Group = isMajority ? "Majority" : "Minority",
                        PositionOrder = position,
                        Provenance = new AssignmentProvenance
                        {
                            SourceDate = sourceDate,
                            PageNumber = pageNumber,
                            BlobUri = blobUri,
                            PdfHash = pdfHash,
                            RawLine = memberLine
                        }
                    };

                    result.Assignments.Add(assignment);

                    // After processing first member, switch to minority for second member on same line
                    isMajority = false;
                }
            }
        }

        // Validate results
        if (result.Committees.Count < 8)
        {
            result.Status = "Degraded";
            result.Warnings.Add($"Only found {result.Committees.Count} committees (expected at least 8 standing committees)");
            _logger.LogWarning("Degraded run: only {Count} committees found", result.Committees.Count);
        }

        if (result.Subcommittees.Count == 0)
        {
            result.Status = "Degraded";
            result.Warnings.Add("No subcommittees found");
            _logger.LogWarning("Degraded run: no subcommittees found");
        }

            _logger.LogInformation(
                "Parsing complete: {CommitteeCount} committees, {SubcommitteeCount} subcommittees, {MemberCount} members, {AssignmentCount} assignments",
                result.Committees.Count,
                result.Subcommittees.Count,
                result.Members.Count,
                result.Assignments.Count);

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open or parse PDF document");
            return Task.FromResult(result); // Return empty result
        }
    }

    private List<string> GetLinesFromPage(Page page)
    {
        // Use GetWords() to get properly separated words with position information
        var words = page.GetWords();

        // Group words by Y position (with a small tolerance for floating point comparison)
        const double yTolerance = 2.0;
        var lineGroups = words
            .GroupBy(w => Math.Round(w.BoundingBox.Bottom / yTolerance) * yTolerance)
            .OrderByDescending(g => g.Key) // Top to bottom (higher Y values first in PDF coordinates)
            .ToList();

        // Reconstruct lines by joining words in each group, ordered by X position
        var lines = new List<string>();
        foreach (var group in lineGroups)
        {
            var lineWords = group
                .OrderBy(w => w.BoundingBox.Left) // Left to right
                .Select(w => w.Text)
                .ToList();

            var line = string.Join(" ", lineWords);
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    private string NormalizeName(string name)
    {
        return name
            .ToLowerInvariant()
            .Replace("committee on ", "")
            .Replace("subcommittee on ", "")
            .Replace(" ", "-")
            .Replace("'", "")
            .Replace(",", "")
            .Trim('-');
    }

    private string NormalizeMemberKey(string name, string? state, string? district)
    {
        var normalized = name
            .ToLowerInvariant()
            .Replace(", ", "-")
            .Replace(" ", "-")
            .Replace("'", "")
            .Replace(".", "");

        var key = new StringBuilder(normalized);

        if (!string.IsNullOrEmpty(state))
        {
            key.Append($"-{state.ToLowerInvariant()}");
        }

        if (!string.IsNullOrEmpty(district))
        {
            key.Append(district);
        }

        return key.ToString();
    }

    private string FixConcatenatedNames(string line)
    {
        // Fix patterns like "PeteSessions,TX" -> "Pete Sessions, TX"
        // This regex finds lowercase followed by uppercase within a word
        var pattern = @"([a-z])([A-Z])";
        var fixedLine = Regex.Replace(line, pattern, "$1 $2");

        // Also ensure there's a space after commas if missing
        fixedLine = Regex.Replace(fixedLine, @",([A-Z])", ", $1");

        // Fix patterns where there's no space after a period and number
        fixedLine = Regex.Replace(fixedLine, @"(\d+\.)([A-Z])", "$1 $2");

        return fixedLine;
    }

    private List<(string Name, string? State, string? Role)> ParseSubcommitteeMembers(string line)
    {
        var members = new List<(string Name, string? State, string? Role)>();

        // Pattern to match member names with states
        // E.g., "Pete Sessions, TX Juan Vargas, CA" or "Pete Sessions, TX, Chairman Kweisi Mfume, MD"
        var pattern = @"([A-Z][a-zA-Z\.\s]+?)(?:,\s*(?:Jr\.|Sr\.|III|IV|V))?\s*,\s*([A-Z]{2})(?:\s*,\s*([^,]+?))?(?:\s+|$)";
        var matches = Regex.Matches(line, pattern);

        foreach (Match match in matches)
        {
            var name = match.Groups[1].Value.Trim();
            var state = match.Groups[2].Value.Trim();
            var roleText = match.Groups[3].Value?.Trim();

            // Extract role if present
            string? role = null;
            if (!string.IsNullOrEmpty(roleText))
            {
                var roleMatch = RolePattern.Match(roleText);
                if (roleMatch.Success)
                {
                    role = roleMatch.Groups[1].Value;
                }
            }

            members.Add((name, state, role));
        }

        return members;
    }
}
