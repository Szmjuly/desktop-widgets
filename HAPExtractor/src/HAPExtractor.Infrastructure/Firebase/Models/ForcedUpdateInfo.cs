namespace HAPExtractor.Infrastructure.Firebase.Models;

public class ForcedUpdateInfo
{
    public required string TargetVersion { get; set; }
    public required string DownloadUrl { get; set; }
    public string? PushedBy { get; set; }
    public DateTime? PushedAt { get; set; }
    public string Status { get; set; } = "pending";
    public int RetryCount { get; set; }
    public string? Error { get; set; }
}
