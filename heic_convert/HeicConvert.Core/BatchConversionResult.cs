namespace HeicConvert.Core;

public sealed class BatchConversionResult
{
    /// <summary>HEIC/HEIF inputs discovered for this run (before per-file outcomes).</summary>
    public int TotalSourceFiles { get; init; }
    public int Converted { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
}
