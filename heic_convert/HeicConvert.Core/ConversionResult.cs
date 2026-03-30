namespace HeicConvert.Core;

public sealed class ConversionResult
{
    public bool Success { get; private init; }
    public string ErrorMessage { get; private init; } = string.Empty;

    public static ConversionResult Ok() => new() { Success = true };
    public static ConversionResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
