using CongressStockTrades.Core.Models;
using CongressStockTrades.Core.Services;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace CongressStockTrades.Infrastructure.Services;

public class CommitteeRosterQAService : ICommitteeRosterQAService
{
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<CommitteeRosterQAService> _logger;

    public CommitteeRosterQAService(
        IBlobStorageService blobStorageService,
        ILogger<CommitteeRosterQAService> logger)
    {
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    public async Task<List<QAFindingDocument>> ValidateAgainstOALAsync(
        string oalUrl,
        CommitteeRosterParseResult parseResult,
        string sourceDate,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<QAFindingDocument>();

        try
        {
            _logger.LogInformation("Starting QA validation against OAL");

            // Download OAL PDF
            var (oalStream, _) = await _blobStorageService.DownloadPdfAsync(oalUrl, cancellationToken);

            // Extract member→committee mappings from OAL
            var oalMappings = ParseOAL(oalStream);

            _logger.LogInformation("Extracted {Count} member→committee mappings from OAL", oalMappings.Count);

            // Sample validation: check 10 random members
            var sampleSize = Math.Min(10, parseResult.Members.Count);
            var random = new Random(42); // Fixed seed for reproducibility
            var sampleMembers = parseResult.Members
                .OrderBy(_ => random.Next())
                .Take(sampleSize)
                .ToList();

            foreach (var member in sampleMembers)
            {
                var memberAssignments = parseResult.Assignments
                    .Where(a => a.MemberKey == member.MemberKey)
                    .Select(a => a.CommitteeAssignmentKey ?? a.SubcommitteeAssignmentKey)
                    .Where(k => k != null)
                    .ToHashSet();

                if (oalMappings.TryGetValue(member.DisplayName, out var oalCommittees))
                {
                    // Check for discrepancies
                    var missingInSCSOAL = oalCommittees.Except(memberAssignments).ToList();
                    var missingInOAL = memberAssignments.Except(oalCommittees).ToList();

                    foreach (var committee in missingInSCSOAL)
                    {
                        findings.Add(new QAFindingDocument
                        {
                            Id = Guid.NewGuid().ToString(),
                            SourceDate = sourceDate,
                            DiscrepancyType = "MissingInSCSOAL",
                            MemberName = member.DisplayName,
                            CommitteeName = committee!,
                            Details = $"OAL lists {member.DisplayName} on {committee}, but SCSOAL does not"
                        });
                    }

                    foreach (var committee in missingInOAL)
                    {
                        findings.Add(new QAFindingDocument
                        {
                            Id = Guid.NewGuid().ToString(),
                            SourceDate = sourceDate,
                            DiscrepancyType = "MissingInOAL",
                            MemberName = member.DisplayName,
                            CommitteeName = committee!,
                            Details = $"SCSOAL lists {member.DisplayName} on {committee}, but OAL does not"
                        });
                    }
                }
                else
                {
                    _logger.LogWarning("Member {MemberName} not found in OAL", member.DisplayName);
                }
            }

            _logger.LogInformation("QA validation complete: {FindingCount} discrepancies found", findings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QA validation failed (non-blocking): {Message}", ex.Message);
            // Do not throw - QA is non-blocking
        }

        return findings;
    }

    private Dictionary<string, HashSet<string>> ParseOAL(Stream oalStream)
    {
        var mappings = new Dictionary<string, HashSet<string>>();

        try
        {
            if (oalStream.CanSeek)
            {
                oalStream.Position = 0;
            }

            using var document = PdfDocument.Open(oalStream, new UglyToad.PdfPig.ParsingOptions { ClipPaths = true });

            foreach (var page in document.GetPages())
            {
                var lines = page.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                string? currentMember = null;
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();

                    // Simple heuristic: if line contains comma followed by state, it's a member name
                    if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[A-Z][a-z]+,\s+[A-Z][a-z]+.*,\s+[A-Z]{2}"))
                    {
                        currentMember = trimmed.Split(',')[0].Trim();
                        if (!mappings.ContainsKey(currentMember))
                        {
                            mappings[currentMember] = new HashSet<string>();
                        }
                    }
                    // If line looks like a committee name, associate it with current member
                    else if (currentMember != null && trimmed.StartsWith("Committee on", StringComparison.OrdinalIgnoreCase))
                    {
                        mappings[currentMember].Add(trimmed);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse OAL: {Message}", ex.Message);
        }

        return mappings;
    }
}
