namespace LoadExtractor.Core.Models;

public class TraneRoomLoad
{
    public string ProjectName { get; set; } = string.Empty;
    public int SourcePage { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public double? TotalCapacityTons { get; set; }
    public double? TotalCapacityMbh { get; set; }
    public double? SensibleCapacityMbh { get; set; }
    public double? CoilAirflowCfm { get; set; }
    public double? GrossFloorAreaSqFt { get; set; }
    public double? SqFtPerTon { get; set; }
    public double? NumberOfPeople { get; set; }
    public double? CalculatedAirflowCfm { get; set; }

    /// <summary>Optional Design Cooling Load Summary data matched by room/zone.</summary>
    public DesignCoolingSupplement? DesignCooling { get; set; }

    public string RoomDisplayName => string.IsNullOrWhiteSpace(RoomNumber)
        ? RoomName
        : $"{RoomNumber} {RoomName}".Trim();
}
