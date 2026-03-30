namespace HeicConvert.Core;

public sealed class ConvertOptions
{
    public string? InputPath { get; set; }
    public string? OutputDirectory { get; set; }
    public string Format { get; set; } = "jpg";
    public int Quality { get; set; } = 90;
    public bool? Recursive { get; set; }
    public bool? Overwrite { get; set; }
}
