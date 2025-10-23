using CongressStockTrades.Core.Models;
using CongressStockTrades.Core.Services;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace CongressStockTrades.Functions.Functions;

/// <summary>
/// HTTP-triggered function for manual/on-demand committee roster updates.
/// Useful for testing and immediate updates without waiting for the weekly timer.
/// </summary>
public class ManualUpdateCommitteeRostersFunction
{
    private readonly ILogger<ManualUpdateCommitteeRostersFunction> _logger;
    private readonly CommitteeRosterSettings _settings;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ICommitteeRosterParser _parser;
    private readonly ICommitteeRosterRepository _repository;
    private readonly ICommitteeRosterQAService _qaService;
    private readonly TelemetryClient _telemetryClient;

    public ManualUpdateCommitteeRostersFunction(
        ILogger<ManualUpdateCommitteeRostersFunction> logger,
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
    /// Triggers committee roster update on-demand via HTTP POST.
    /// Query parameter: force=true to bypass change detection
    /// </summary>
    [Function("ManualUpdateCommitteeRosters")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "committee-rosters/update")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        using var operation = _telemetryClient.StartOperation<RequestTelemetry>("ManualUpdateCommitteeRosters");

        try
        {
            _logger.LogInformation("Manual UpdateCommitteeRosters triggered at {Time}", DateTime.UtcNow);

            // Check for force parameter
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var force = bool.TryParse(query["force"], out var forceValue) && forceValue;

            // Check if feature is enabled
            if (!_settings.Enabled)
            {
                _logger.LogWarning("Committee roster updater is disabled");
                var disabledResponse = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
                await disabledResponse.WriteStringAsync("Committee roster updater is disabled");
                return disabledResponse;
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
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Failed to extract cover date from PDF");
                return errorResponse;
            }

            _logger.LogInformation("Source date: {SourceDate}", sourceDate);

            // Check for changes (unless forced)
            if (!force)
            {
                var lastSource = await _repository.GetLastSourceAsync(_settings.SCSOALUrl, cancellationToken);
                if (lastSource != null && lastSource.SourceDate == sourceDate && lastSource.PdfHash == pdfHash)
                {
                    _logger.LogInformation("No changes detected (date={SourceDate}, hash={Hash}). Use ?force=true to override.", sourceDate, pdfHash);
                    var noChangeResponse = req.CreateResponse(HttpStatusCode.OK);
                    await noChangeResponse.WriteAsJsonAsync(new
                    {
                        status = "skipped",
                        reason = "no_changes_detected",
                        sourceDate,
                        pdfHash,
                        message = "No changes since last run. Use ?force=true to reprocess."
                    });
                    return noChangeResponse;
                }
            }

            // Upload PDF to blob storage
            var blobUri = await _blobStorageService.UploadPdfAsync(pdfStream, sourceDate, "scsoal.pdf", cancellationToken);
            _logger.LogInformation("Uploaded PDF to {BlobUri}", blobUri);

            // Parse PDF
            _logger.LogInformation("Parsing PDF...");
            var parseResult = await _parser.ParseSCSOALAsync(pdfStream, sourceDate, blobUri, pdfHash, cancellationToken);

            _telemetryClient.TrackMetric("committees_count", parseResult.Committees.Count);
            _telemetryClient.TrackMetric("subcommittees_count", parseResult.Subcommittees.Count);
            _telemetryClient.TrackMetric("members_seen", parseResult.Members.Count);
            _telemetryClient.TrackMetric("assignments_count", parseResult.Assignments.Count);

            // Check for degraded status
            if (parseResult.Status == "Degraded")
            {
                _logger.LogWarning("Parse result is degraded: {Warnings}", string.Join("; ", parseResult.Warnings));
                var degradedResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await degradedResponse.WriteAsJsonAsync(new
                {
                    status = "degraded",
                    warnings = parseResult.Warnings,
                    counts = new
                    {
                        committees = parseResult.Committees.Count,
                        subcommittees = parseResult.Subcommittees.Count,
                        members = parseResult.Members.Count,
                        assignments = parseResult.Assignments.Count
                    }
                });
                return degradedResponse;
            }

            // Check churn threshold
            var previousAssignmentCount = await _repository.GetPreviousAssignmentCountAsync(_settings.SCSOALUrl, cancellationToken);
            double? churnPercent = null;
            if (previousAssignmentCount > 0)
            {
                churnPercent = Math.Abs(parseResult.Assignments.Count - previousAssignmentCount) / (double)previousAssignmentCount;
                if (churnPercent > _settings.ChurnThresholdPercent)
                {
                    _logger.LogWarning(
                        "High churn detected: {ChurnPercent:P} (previous={Previous}, current={Current})",
                        churnPercent,
                        previousAssignmentCount,
                        parseResult.Assignments.Count);

                    _telemetryClient.TrackMetric("churn_percent", churnPercent.Value);
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
            int qaFindingsCount = 0;
            if (_settings.UseOALForQA && !string.IsNullOrEmpty(_settings.OALUrl))
            {
                _logger.LogInformation("Running QA validation against OAL...");
                var findings = await _qaService.ValidateAgainstOALAsync(_settings.OALUrl, parseResult, sourceDate, cancellationToken);

                foreach (var finding in findings)
                {
                    await _repository.StoreQAFindingAsync(finding, cancellationToken);
                }

                qaFindingsCount = findings.Count;
                _telemetryClient.TrackMetric("qa_discrepancies", qaFindingsCount);
            }

            _logger.LogInformation("Manual UpdateCommitteeRosters completed successfully at {Time}", DateTime.UtcNow);
            operation.Telemetry.Success = true;

            // Return success response
            var successResponse = req.CreateResponse(HttpStatusCode.OK);
            await successResponse.WriteAsJsonAsync(new
            {
                status = "success",
                sourceDate,
                pdfHash,
                blobUri,
                forced = force,
                counts = new
                {
                    committees = parseResult.Committees.Count,
                    subcommittees = parseResult.Subcommittees.Count,
                    members = parseResult.Members.Count,
                    assignments = parseResult.Assignments.Count
                },
                churnPercent = churnPercent?.ToString("P2"),
                qaFindings = qaFindingsCount,
                processedAt = sourceDocument.ProcessedAt
            });
            return successResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual UpdateCommitteeRosters failed: {Message}", ex.Message);
            operation.Telemetry.Success = false;

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                status = "failed",
                error = ex.Message,
                stackTrace = ex.StackTrace
            });
            return errorResponse;
        }
    }
}
