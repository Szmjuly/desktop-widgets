namespace HAPExtractor.Core.Models;

public class ComponentLoadRow
{
    public string RowName { get; set; } = string.Empty;
    public string CoolingDetails { get; set; } = string.Empty;
    public double CoolingSensible { get; set; }
    public double CoolingLatent { get; set; }
    public string HeatingDetails { get; set; } = string.Empty;
    public double HeatingSensible { get; set; }
    public double HeatingLatent { get; set; }
}
