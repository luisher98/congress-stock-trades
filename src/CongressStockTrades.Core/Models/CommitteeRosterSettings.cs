namespace CongressStockTrades.Core.Models;

/// <summary>
/// Configuration settings for the Committee Roster Updater feature.
/// Binds to "CommitteeRosters" section in appsettings.json and local.settings.json.
/// </summary>
public class CommitteeRosterSettings
{
    /// <summary>
    /// Feature flag to enable/disable the committee roster updater.
    /// Default: false
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// URL to the House Standing and Select Committees PDF (scsoal.pdf).
    /// Example: "https://clerk.house.gov/committee_info/scsoal.pdf"
    /// </summary>
    public required string SCSOALUrl { get; set; }

    /// <summary>
    /// Optional URL to the Official Alphabetical List PDF (oal.pdf) for QA validation.
    /// Example: "https://clerk.house.gov/committee_info/oal.pdf"
    /// </summary>
    public string? OALUrl { get; set; }

    /// <summary>
    /// Enable QA validation using the OAL PDF (non-blocking).
    /// Default: false
    /// </summary>
    public bool UseOALForQA { get; set; } = false;

    /// <summary>
    /// Enable fallback to Azure AI Document Intelligence on degraded runs.
    /// Default: false
    /// </summary>
    public bool EnableDocIntelFallback { get; set; } = false;

    /// <summary>
    /// Parser version for provenance tracking.
    /// Updated when parser logic changes.
    /// </summary>
    public string ParserVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Churn threshold (percentage) to trigger alerts.
    /// Example: 0.25 = 25% change in assignments triggers an alert.
    /// Default: 0.25
    /// </summary>
    public double ChurnThresholdPercent { get; set; } = 0.25;
}
