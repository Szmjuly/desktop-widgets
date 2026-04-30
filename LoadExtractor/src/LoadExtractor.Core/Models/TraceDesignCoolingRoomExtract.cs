namespace LoadExtractor.Core.Models;

/// <summary>One parsed page from a TRACE Design Cooling Load Summary PDF.</summary>
public class TraceDesignCoolingRoomExtract
{
    public int SourcePage { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string? CalcBy { get; set; }
    public string? HeaderProjectTitle { get; set; }
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
}
