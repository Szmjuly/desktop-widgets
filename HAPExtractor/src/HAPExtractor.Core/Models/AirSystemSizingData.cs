namespace HAPExtractor.Core.Models;

public class AirSystemSizingData
{
    public string SystemName { get; set; } = string.Empty;
    public double SqftPerTon { get; set; }
    public double FloorArea { get; set; }
    public double TotalCoilLoadTons { get; set; }
    public double CfmPerTon { get; set; }
}
