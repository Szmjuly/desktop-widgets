namespace LoadExtractor.Core.Models;

public class SpaceComponentLoads
{
    public string SpaceName { get; set; } = string.Empty;
    public string ZoneName { get; set; } = string.Empty;
    public string SystemName { get; set; } = string.Empty;

    // TABLE 1.1.A rows in order
    public ComponentLoadRow WindowSkylightSolar { get; set; } = new() { RowName = "Window & Skylight Solar Loads" };
    public ComponentLoadRow WallTransmission { get; set; } = new() { RowName = "Wall Transmission" };
    public ComponentLoadRow RoofTransmission { get; set; } = new() { RowName = "Roof Transmission" };
    public ComponentLoadRow WindowTransmission { get; set; } = new() { RowName = "Window Transmission" };
    public ComponentLoadRow SkylightTransmission { get; set; } = new() { RowName = "Skylight Transmission" };
    public ComponentLoadRow DoorLoads { get; set; } = new() { RowName = "Door Loads" };
    public ComponentLoadRow FloorTransmission { get; set; } = new() { RowName = "Floor Transmission" };
    public ComponentLoadRow Partitions { get; set; } = new() { RowName = "Partitions" };
    public ComponentLoadRow Ceiling { get; set; } = new() { RowName = "Ceiling" };
    public ComponentLoadRow OverheadLighting { get; set; } = new() { RowName = "Overhead Lighting" };
    public ComponentLoadRow TaskLighting { get; set; } = new() { RowName = "Task Lighting" };
    public ComponentLoadRow ElectricEquipment { get; set; } = new() { RowName = "Electric Equipment" };
    public ComponentLoadRow People { get; set; } = new() { RowName = "People" };
    public ComponentLoadRow Infiltration { get; set; } = new() { RowName = "Infiltration" };
    public ComponentLoadRow Miscellaneous { get; set; } = new() { RowName = "Miscellaneous" };
    public ComponentLoadRow SafetyFactor { get; set; } = new() { RowName = "Safety Factor" };
    public ComponentLoadRow TotalZoneLoads { get; set; } = new() { RowName = ">> Total Zone Loads" };

    public List<ComponentLoadRow> AllRows => new()
    {
        WindowSkylightSolar, WallTransmission, RoofTransmission,
        WindowTransmission, SkylightTransmission, DoorLoads,
        FloorTransmission, Partitions, Ceiling,
        OverheadLighting, TaskLighting, ElectricEquipment,
        People, Infiltration, Miscellaneous, SafetyFactor, TotalZoneLoads
    };

    // Envelope section: rows 1-9 (Window & Skylight -> Ceiling)
    public List<ComponentLoadRow> EnvelopeRows => new()
    {
        WindowSkylightSolar, WallTransmission, RoofTransmission,
        WindowTransmission, SkylightTransmission, DoorLoads,
        FloorTransmission, Partitions, Ceiling
    };

    // Internal gains: rows 10-12 (Overhead Lighting -> Electrical Equipment)
    public List<ComponentLoadRow> InternalGainRows => new()
    {
        OverheadLighting, TaskLighting, ElectricEquipment
    };
}
