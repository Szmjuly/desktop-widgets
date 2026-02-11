using DesktopHub.Core.Models;

namespace DesktopHub.Core.Abstractions;

/// <summary>
/// Data persistence layer for Quick Tasks widget
/// </summary>
public interface ITaskDataStore
{
    /// <summary>
    /// Initialize the data store (create tables, indexes, etc.)
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Get all tasks for a specific date
    /// </summary>
    Task<List<TaskItem>> GetTasksByDateAsync(string date);

    /// <summary>
    /// Get tasks across a range of dates (for browsing history)
    /// </summary>
    Task<List<TaskItem>> GetTasksByDateRangeAsync(string startDate, string endDate);

    /// <summary>
    /// Get the most recent N dates that have tasks
    /// </summary>
    Task<List<string>> GetRecentTaskDatesAsync(int count);

    /// <summary>
    /// Insert or update a task
    /// </summary>
    Task UpsertTaskAsync(TaskItem task);

    /// <summary>
    /// Delete a task by ID
    /// </summary>
    Task DeleteTaskAsync(string taskId);

    /// <summary>
    /// Search tasks by title across all dates
    /// </summary>
    Task<List<TaskItem>> SearchTasksAsync(string query, int limit = 50);

    /// <summary>
    /// Get count of active (incomplete) and completed tasks for a date
    /// </summary>
    Task<(int active, int completed)> GetTaskCountsAsync(string date);

    /// <summary>
    /// Get all incomplete tasks for a specific date (used for carry-over)
    /// </summary>
    Task<List<TaskItem>> GetIncompleteTasksAsync(string date);

    /// <summary>
    /// Get the next sort order value for a date
    /// </summary>
    Task<int> GetNextSortOrderAsync(string date);

    /// <summary>
    /// Get a single task by ID
    /// </summary>
    Task<TaskItem?> GetTaskByIdAsync(string taskId);

    /// <summary>
    /// Get all incomplete original tasks (not carry-over copies) from dates before the given date
    /// </summary>
    Task<List<TaskItem>> GetAllIncompleteOriginalTasksBeforeDateAsync(string beforeDate);

    /// <summary>
    /// Get all carry-over copies on a specific date (tasks where carried_from_task_id is not null)
    /// </summary>
    Task<List<TaskItem>> GetCarriedOverCopiesOnDateAsync(string date);

    /// <summary>
    /// Delete all incomplete carry-over copies and return the original task IDs that were un-carried
    /// </summary>
    Task<List<string>> DeleteIncompleteCarriedOverCopiesAsync();
}
