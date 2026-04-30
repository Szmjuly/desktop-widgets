namespace LoadExtractor.Core.Models;

/// <summary>
/// Optional fields from a TRACE "Design Cooling Load Summary" page, merged onto a room row.
/// </summary>
public class DesignCoolingSupplement
{
    public string? CalcBy { get; set; }
    /// <summary>Project / building title from the report header (red box #2).</summary>
    public string? HeaderProjectTitle { get; set; }
    /// <summary>Project name from the footer line (red box #3).</summary>
    public string? FooterProjectName { get; set; }
    public string? ZoneText { get; set; }
    public string? SystemName { get; set; }
    public double? CoilSensibleMbh { get; set; }
    public double? CoilTotalMbh { get; set; }
    public double? TotalCoolingAirflowCfm { get; set; }
    public double? EngTotalCoolingLoad { get; set; }
    public double? EngAreaPerLoad { get; set; }
    public double? ReportSensibleBtuH { get; set; }
    public double? ReportLatentBtuH { get; set; }
    public double? ReportTotalBtuH { get; set; }
    public int? DesignSourcePage { get; set; }
    /// <summary>Match / Mismatch / — (no design row)</summary>
    public string LoadsCrossCheck { get; set; } = "—";
}
