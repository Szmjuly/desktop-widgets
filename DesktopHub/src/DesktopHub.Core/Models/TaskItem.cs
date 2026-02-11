namespace DesktopHub.Core.Models;

/// <summary>
/// Represents a single task in the Quick Tasks widget
/// </summary>
public class TaskItem
{
    /// <summary>
    /// Unique identifier (GUID)
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The date this task belongs to (ISO format: 2026-02-09)
    /// </summary>
    public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

    /// <summary>
    /// Task title/description
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Whether the task is completed
    /// </summary>
    public bool IsCompleted { get; set; } = false;

    /// <summary>
    /// Priority level: "low", "normal", "high"
    /// </summary>
    public string Priority { get; set; } = "normal";

    /// <summary>
    /// Sort position within the day's task list
    /// </summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// When the task was created (ISO 8601)
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// When the task was completed (null if not completed)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Optional user-defined category
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Optional short note
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// If this task is a carry-over copy, the ID of the original task it was carried from
    /// </summary>
    public string? CarriedFromTaskId { get; set; }

    /// <summary>
    /// If this task is a carry-over copy, the date of the original task
    /// </summary>
    public string? CarriedFromDate { get; set; }

    /// <summary>
    /// Whether this original task has been carried over to another date
    /// </summary>
    public bool IsCarriedOver { get; set; } = false;
}
