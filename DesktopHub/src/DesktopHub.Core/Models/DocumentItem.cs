namespace DesktopHub.Core.Models;

/// <summary>
/// Represents a file (drawing, document, etc.) found inside a project folder
/// </summary>
public class DocumentItem
{
    /// <summary>
    /// Full filesystem path to the file
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// File name without path (e.g., "M-101 HVAC Plan.pdf")
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// File extension without dot, lowercased (e.g., "pdf")
    /// </summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Last modified timestamp
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Category derived from parent subfolder or extension (e.g., "Drawing", "Spec", "Submittal")
    /// </summary>
    public string Category { get; set; } = "Other";

    /// <summary>
    /// Relative path from project root (e.g., "Drawings\M-101 HVAC Plan.pdf")
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// Parent subfolder name (e.g., "Drawings")
    /// </summary>
    public string Subfolder { get; set; } = string.Empty;

    /// <summary>
    /// Whether this file is pinned by the user
    /// </summary>
    public bool IsPinned { get; set; }

    /// <summary>
    /// Human-readable file size
    /// </summary>
    public string SizeDisplay
    {
        get
        {
            if (SizeBytes < 1024) return $"{SizeBytes} B";
            if (SizeBytes < 1024 * 1024) return $"{SizeBytes / 1024.0:F1} KB";
            if (SizeBytes < 1024 * 1024 * 1024) return $"{SizeBytes / (1024.0 * 1024):F1} MB";
            return $"{SizeBytes / (1024.0 * 1024 * 1024):F1} GB";
        }
    }
}
