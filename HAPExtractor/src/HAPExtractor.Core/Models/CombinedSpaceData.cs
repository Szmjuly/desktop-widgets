namespace HAPExtractor.Core.Models;

public class CombinedSpaceData
{
    // From PDF1
    public string RoomName { get; set; } = string.Empty;
    public string SystemName { get; set; } = string.Empty;
    public double FloorAreaSqFt { get; set; }

    // From PDF2 — Totals
    public double TotalPeopleDetails { get; set; }
    public double TotalCoolingSensible { get; set; }
    public double TotalCoolingLatent { get; set; }

    // From PDF2 — full component loads
    public SpaceComponentLoads? ComponentLoads { get; set; }
}
