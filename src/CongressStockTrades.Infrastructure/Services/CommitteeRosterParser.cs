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
    private static readonly Regex CoverDatePattern = new(@"([A-Z]+)\s+(\d{1,2}),\s+(\d{4})", RegexOptions.Compiled);
    private static readonly Regex CommitteeHeaderPattern = new(@"^COMMITTEE\s+ON\s+(.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SubcommitteeHeaderPattern = new(@"^SUBCOMMITTEES?\s+OF\s+THE\s+COMMITTEE\s+ON\s+(.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MemberLinePattern = new(@"^\s*(\d+)\.\s+(.+?)(?:,\s+of\s+([A-Z][a-z\s]+))?(?:,\s+(.+))?$", RegexOptions.Compiled);
    private static readonly Regex RolePattern = new(@"\b(Chair|Ranking Member|Vice Chair|Ex Officio)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

        using var document = PdfDocument.Open(pdfStream, new UglyToad.PdfPig.ParsingOptions { ClipPaths = true });

        if (document.NumberOfPages < 1)
        {
            _logger.LogWarning("PDF has no pages");
            return null;
        }

        var firstPage = document.GetPage(1);
        var text = firstPage.Text.ToUpperInvariant();

        var match = CoverDatePattern.Match(text);
        if (match.Success)
        {
            var monthName = match.Groups[1].Value;
            var day = int.Parse(match.Groups[2].Value);
            var year = int.Parse(match.Groups[3].Value);

            var month = DateTime.ParseExact(monthName, "MMMM", CultureInfo.InvariantCulture).Month;
            var date = new DateTime(year, month, day);
            var formattedDate = date.ToString("yyyy-MM-dd");

            _logger.LogInformation("Extracted cover date: {SourceDate}", formattedDate);
            return Task.FromResult<string?>(formattedDate);
        }

        _logger.LogWarning("Could not extract cover date from PDF");
        return Task.FromResult<string?>(null);
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

        using var document = PdfDocument.Open(pdfStream, new UglyToad.PdfPig.ParsingOptions { ClipPaths = true });

        _logger.LogInformation("PDF has {PageCount} pages", document.NumberOfPages);

        // Parse page by page
        string? currentCommitteeName = null;
        string? currentCommitteeKey = null;
        string? currentSubcommitteeName = null;
        string? currentSubcommitteeKey = null;
        bool inMajoritySection = true;
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

                // Check for committee header
                var committeeMatch = CommitteeHeaderPattern.Match(trimmed);
                if (committeeMatch.Success)
                {
                    currentCommitteeName = committeeMatch.Groups[1].Value.Trim();
                    currentCommitteeKey = NormalizeName(currentCommitteeName);
                    currentSubcommitteeName = null;
                    currentSubcommitteeKey = null;
                    inMajoritySection = true;

                    // Add committee
                    var committee = new CommitteeDocument
                    {
                        Id = currentCommitteeKey,
                        CommitteeKey = currentCommitteeKey,
                        Name = currentCommitteeName,
                        Chamber = "House",
                        Type = "Standing", // Default; can be enhanced
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
                    continue;
                }

                // Check for subcommittee header
                var subcommitteeMatch = SubcommitteeHeaderPattern.Match(trimmed);
                if (subcommitteeMatch.Success)
                {
                    if (currentCommitteeKey == null)
                    {
                        _logger.LogWarning("Found subcommittee without parent committee on page {Page}", pageNumber);
                        continue;
                    }

                    currentSubcommitteeName = trimmed;
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
                    _logger.LogDebug("Found subcommittee: {SubcommitteeName}", currentSubcommitteeName);
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

                // Parse member line
                var memberMatch = MemberLinePattern.Match(trimmed);
                if (memberMatch.Success)
                {
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
                        Group = inMajoritySection ? "Majority" : "Minority",
                        PositionOrder = position,
                        Provenance = new AssignmentProvenance
                        {
                            SourceDate = sourceDate,
                            PageNumber = pageNumber,
                            BlobUri = blobUri,
                            PdfHash = pdfHash,
                            RawLine = trimmed
                        }
                    };

                    result.Assignments.Add(assignment);
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

    private List<string> GetLinesFromPage(Page page)
    {
        var text = page.Text;
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
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
}
