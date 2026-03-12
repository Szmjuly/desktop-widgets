namespace HAPExtractor.Core.Models;

public class SpaceLoad
{
    public string SpaceName { get; set; } = string.Empty;
    public int Multiplier { get; set; } = 1;
    public double CoolingSensible { get; set; }
    public string TimeOfPeakSensible { get; set; } = string.Empty;
    public double AirFlow { get; set; }
    public double HeatingLoad { get; set; }
    public double FloorArea { get; set; }
    public double SpaceCfmPerSqFt { get; set; }
}
