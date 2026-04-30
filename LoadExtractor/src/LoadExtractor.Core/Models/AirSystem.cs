namespace LoadExtractor.Core.Models;

public class AirSystem
{
    public string Name { get; set; } = string.Empty;
    public double FloorArea { get; set; }
    public List<SpaceLoad> Spaces { get; set; } = new();
}
