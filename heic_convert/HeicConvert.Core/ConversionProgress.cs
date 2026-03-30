namespace HeicConvert.Core;

public sealed class ConversionProgress
{
    public required string Message { get; init; }
    public int CurrentIndex { get; init; }
    public int Total { get; init; }
}
