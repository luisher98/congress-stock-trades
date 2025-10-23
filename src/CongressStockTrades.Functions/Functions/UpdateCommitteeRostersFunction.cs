using CongressStockTrades.Core.Models;
using CongressStockTrades.Core.Services;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CongressStockTrades.Functions.Functions;

/// <summary>
/// Timer-triggered function that updates committee rosters weekly.
/// Checks for new editions of the House Standing and Select Committees PDF,
/// parses the roster data, and upserts to Cosmos DB with full provenance.
/// </summary>
public class UpdateCommitteeRostersFunction
{
    private readonly ILogger<UpdateCommitteeRostersFunction> _logger;
    private readonly CommitteeRosterSettings _settings;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ICommitteeRosterParser _parser;
    private readonly ICommitteeRosterRepository _repository;
    private readonly ICommitteeRosterQAService _qaService;
    private readonly TelemetryClient _telemetryClient;

    public UpdateCommitteeRostersFunction(
        ILogger<UpdateCommitteeRostersFunction> logger,
        IOptions<CommitteeRosterSettings> settings,
        IBlobStorageService blobStorageService,
        ICommitteeRosterParser parser,
        ICommitteeRosterRepository repository,
        ICommitteeRosterQAService qaService,
        TelemetryClient telemetryClient)
    {
        _logger = logger;
        _settings = settings.Value;
        _blobStorageService = blobStorageService;
        _parser = parser;
        _repository = repository;
        _qaService = qaService;
        _telemetryClient = telemetryClient;
    }

    /// <summary>
    /// Runs weekly on Sundays at 2 AM UTC.
    /// NCRONTAB format: "0 0 2 * * 0" = (second minute hour day month dayOfWeek)
    /// </summary>
    [Function("UpdateCommitteeRosters")]
    public async Task Run(
        [TimerTrigger("0 0 2 * * 0")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        using var operation = _telemetryClient.StartOperation<RequestTelemetry>("UpdateCommitteeRosters");

        try
        {
            _logger.LogInformation("UpdateCommitteeRosters function started at {Time}", DateTime.UtcNow);
            _telemetryClient.TrackEvent("RunStarted");

            // Check if feature is enabled
            if (!_settings.Enabled)
            {
                _logger.LogInformation("Committee roster updater is disabled");
                return;
            }

            // Download PDF
            _logger.LogInformation("Downloading SCSOAL PDF from {Url}", _settings.SCSOALUrl);
            var (pdfStream, byteLength) = await _blobStorageService.DownloadPdfAsync(_settings.SCSOALUrl, cancellationToken);

            // Compute hash
            var pdfHash = _blobStorageService.ComputeHash(pdfStream);
            _logger.LogInformation("PDF hash: {Hash}", pdfHash);

            // Extract cover date
            var sourceDate = await _parser.ExtractCoverDateAsync(pdfStream, cancellationToken);
            if (sourceDate == null)
            {
                _logger.LogError("Failed to extract cover date from PDF");
                _telemetryClient.TrackEvent("RunFailed", new Dictionary<string, string>
                {
                    { "Reason", "CoverDateExtractionFailed" }
                });
                return;
            }

            _logger.LogInformation("Source date: {SourceDate}", sourceDate);

            // Check for changes
            var lastSource = await _repository.GetLastSourceAsync(_settings.SCSOALUrl, cancellationToken);
            if (lastSource != null && lastSource.SourceDate == sourceDate && lastSource.PdfHash == pdfHash)
            {
                _logger.LogInformation("No changes detected (date={SourceDate}, hash={Hash}). Skipping run.", sourceDate, pdfHash);
                _telemetryClient.TrackEvent("SkippedNoChange", new Dictionary<string, string>
                {
                    { "SourceDate", sourceDate },
                    { "PdfHash", pdfHash }
                });
                return;
            }

            // Upload PDF to blob storage
            var blobUri = await _blobStorageService.UploadPdfAsync(pdfStream, sourceDate, "scsoal.pdf", cancellationToken);
            _logger.LogInformation("Uploaded PDF to {BlobUri}", blobUri);

            // Parse PDF
            _logger.LogInformation("Parsing PDF...");
            var parseResult = await _parser.ParseSCSOALAsync(pdfStream, sourceDate, blobUri, pdfHash, cancellationToken);

            _telemetryClient.TrackEvent("Parsed", new Dictionary<string, string>
            {
                { "CommitteesCount", parseResult.Committees.Count.ToString() },
                { "SubcommitteesCount", parseResult.Subcommittees.Count.ToString() },
                { "MembersCount", parseResult.Members.Count.ToString() },
                { "AssignmentsCount", parseResult.Assignments.Count.ToString() },
                { "Status", parseResult.Status }
            });

            _telemetryClient.TrackMetric("committees_count", parseResult.Committees.Count);
            _telemetryClient.TrackMetric("subcommittees_count", parseResult.Subcommittees.Count);
            _telemetryClient.TrackMetric("members_seen", parseResult.Members.Count);
            _telemetryClient.TrackMetric("assignments_count", parseResult.Assignments.Count);

            // Check for degraded status
            if (parseResult.Status == "Degraded")
            {
                _logger.LogWarning("Parse result is degraded: {Warnings}", string.Join("; ", parseResult.Warnings));

                // Check if we should enable DI fallback on next run
                if (_settings.EnableDocIntelFallback)
                {
                    _logger.LogWarning("Document Intelligence fallback is enabled but not yet implemented");
                    // TODO: Implement DI fallback
                }

                // Do not proceed with upserts on degraded run
                _telemetryClient.TrackEvent("RunFailed", new Dictionary<string, string>
                {
                    { "Reason", "DegradedParse" },
                    { "Warnings", string.Join("; ", parseResult.Warnings) }
                });
                return;
            }

            // Check churn threshold
            var previousAssignmentCount = await _repository.GetPreviousAssignmentCountAsync(_settings.SCSOALUrl, cancellationToken);
            if (previousAssignmentCount > 0)
            {
                var churnPercent = Math.Abs(parseResult.Assignments.Count - previousAssignmentCount) / (double)previousAssignmentCount;
                if (churnPercent > _settings.ChurnThresholdPercent)
                {
                    _logger.LogWarning(
                        "High churn detected: {ChurnPercent:P} (previous={Previous}, current={Current})",
                        churnPercent,
                        previousAssignmentCount,
                        parseResult.Assignments.Count);

                    _telemetryClient.TrackMetric("churn_percent", churnPercent);
                }
            }

            // Upsert entities
            _logger.LogInformation("Upserting committees...");
            await _repository.UpsertCommitteesAsync(parseResult.Committees, cancellationToken);

            _logger.LogInformation("Upserting subcommittees...");
            await _repository.UpsertSubcommitteesAsync(parseResult.Subcommittees, cancellationToken);

            _logger.LogInformation("Upserting members...");
            await _repository.UpsertMembersAsync(parseResult.Members, cancellationToken);

            _logger.LogInformation("Upserting assignments...");
            await _repository.UpsertAssignmentsAsync(parseResult.Assignments, cancellationToken);

            _telemetryClient.TrackEvent("Upserted", new Dictionary<string, string>
            {
                { "CommitteesCount", parseResult.Committees.Count.ToString() },
                { "SubcommitteesCount", parseResult.Subcommittees.Count.ToString() },
                { "MembersCount", parseResult.Members.Count.ToString() },
                { "AssignmentsCount", parseResult.Assignments.Count.ToString() }
            });

            // Store source document
            var sourceDocument = new SourceDocument
            {
                Id = sourceDate,
                Url = _settings.SCSOALUrl,
                SourceDate = sourceDate,
                PdfHash = pdfHash,
                BlobUri = blobUri,
                ByteLength = byteLength,
                ParserVersion = _settings.ParserVersion,
                ProcessedAt = DateTime.UtcNow,
                ResultCounts = new SourceResultCounts
                {
                    CommitteesCount = parseResult.Committees.Count,
                    SubcommitteesCount = parseResult.Subcommittees.Count,
                    MembersCount = parseResult.Members.Count,
                    AssignmentsCount = parseResult.Assignments.Count
                },
                Status = "Success"
            };

            await _repository.UpsertSourceAsync(sourceDocument, cancellationToken);

            // Optional QA validation
            if (_settings.UseOALForQA && !string.IsNullOrEmpty(_settings.OALUrl))
            {
                _logger.LogInformation("Running QA validation against OAL...");
                var findings = await _qaService.ValidateAgainstOALAsync(_settings.OALUrl, parseResult, sourceDate, cancellationToken);

                foreach (var finding in findings)
                {
                    await _repository.StoreQAFindingAsync(finding, cancellationToken);
                }

                _telemetryClient.TrackEvent("QACompleted", new Dictionary<string, string>
                {
                    { "FindingsCount", findings.Count.ToString() }
                });

                _telemetryClient.TrackMetric("qa_discrepancies", findings.Count);
            }

            _logger.LogInformation("UpdateCommitteeRosters function completed successfully at {Time}", DateTime.UtcNow);
            operation.Telemetry.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateCommitteeRosters function failed: {Message}", ex.Message);
            _telemetryClient.TrackEvent("RunFailed", new Dictionary<string, string>
            {
                { "Reason", "Exception" },
                { "Message", ex.Message }
            });
            operation.Telemetry.Success = false;
            throw;
        }
    }
}
