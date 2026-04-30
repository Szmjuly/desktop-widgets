namespace LoadExtractor.Core.Models;

public class HapProject
{
    public string ProjectName { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public List<AirSystem> AirSystems { get; set; } = new();
}
