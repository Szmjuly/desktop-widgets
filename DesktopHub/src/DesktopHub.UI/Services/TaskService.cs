using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;
using DesktopHub.Infrastructure.Settings;

namespace DesktopHub.UI.Services;

/// <summary>
/// Orchestrates task data and config for the Quick Tasks widget
/// </summary>
public class TaskService
{
    private readonly ITaskDataStore _dataStore;
    private TaskWidgetConfig _config;
    private string _currentDate;
    private List<TaskItem> _currentTasks = new();

    /// <summary>
    /// Fired when the task list changes (add, remove, update, date change)
    /// </summary>
    public event EventHandler? TasksChanged;

    /// <summary>
    /// Fired when the widget config changes (from settings window)
    /// </summary>
    public event EventHandler? ConfigChanged;

    /// <summary>
    /// The currently viewed date (ISO format: yyyy-MM-dd)
    /// </summary>
    public string CurrentDate => _currentDate;

    /// <summary>
    /// The current task list for the viewed date
    /// </summary>
    public IReadOnlyList<TaskItem> CurrentTasks => _currentTasks.AsReadOnly();

    /// <summary>
    /// The widget configuration
    /// </summary>
    public TaskWidgetConfig Config => _config;

    public TaskService(ITaskDataStore dataStore)
    {
        _dataStore = dataStore;
        _config = new TaskWidgetConfig();
        _currentDate = DateTime.Now.ToString("yyyy-MM-dd");
    }

    /// <summary>
    /// Initialize the service: create DB tables, load config, load today's tasks
    /// </summary>
    public async Task InitializeAsync()
    {
        await _dataStore.InitializeAsync();
        _config = await TaskWidgetConfig.LoadAsync();

        // Auto carry-over if enabled
        if (_config.AutoCarryOver)
        {
            await PerformCarryOverAsync();
        }

        await RefreshTasksAsync();
    }

    /// <summary>
    /// Add a new task to the current date
    /// </summary>
    public async Task<TaskItem> AddTaskAsync(string title, string? category = null)
    {
        var sortOrder = await _dataStore.GetNextSortOrderAsync(_currentDate);

        var task = new TaskItem
        {
            Id = Guid.NewGuid().ToString(),
            Date = _currentDate,
            Title = title,
            Priority = _config.DefaultPriority,
            SortOrder = sortOrder,
            CreatedAt = DateTime.Now,
            Category = category
        };

        await _dataStore.UpsertTaskAsync(task);
        await RefreshTasksAsync();
        return task;
    }

    /// <summary>
    /// Toggle task completion
    /// </summary>
    public async Task ToggleTaskCompletionAsync(string taskId)
    {
        var task = _currentTasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null) return;

        task.IsCompleted = !task.IsCompleted;
        task.CompletedAt = task.IsCompleted ? DateTime.Now : null;

        await _dataStore.UpsertTaskAsync(task);
        await RefreshTasksAsync();
    }

    /// <summary>
    /// Update a task's title
    /// </summary>
    public async Task UpdateTaskTitleAsync(string taskId, string newTitle)
    {
        var task = _currentTasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null) return;

        task.Title = newTitle;
        await _dataStore.UpsertTaskAsync(task);
        await RefreshTasksAsync();
    }

    /// <summary>
    /// Set task priority
    /// </summary>
    public async Task SetTaskPriorityAsync(string taskId, string priority)
    {
        var task = _currentTasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null) return;

        task.Priority = priority;
        await _dataStore.UpsertTaskAsync(task);
        await RefreshTasksAsync();
    }

    /// <summary>
    /// Set task category
    /// </summary>
    public async Task SetTaskCategoryAsync(string taskId, string? category)
    {
        var task = _currentTasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null) return;

        task.Category = category;
        await _dataStore.UpsertTaskAsync(task);
        await RefreshTasksAsync();
    }

    /// <summary>
    /// Set task notes
    /// </summary>
    public async Task SetTaskNotesAsync(string taskId, string? notes)
    {
        var task = _currentTasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null) return;

        task.Notes = notes;
        await _dataStore.UpsertTaskAsync(task);
        await RefreshTasksAsync();
    }

    /// <summary>
    /// Delete a task
    /// </summary>
    public async Task DeleteTaskAsync(string taskId)
    {
        await _dataStore.DeleteTaskAsync(taskId);
        await RefreshTasksAsync();
    }

    /// <summary>
    /// Navigate to the previous day
    /// </summary>
    public async Task GoToPreviousDayAsync()
    {
        var date = DateTime.Parse(_currentDate).AddDays(-1);
        _currentDate = date.ToString("yyyy-MM-dd");
        await RefreshTasksAsync();
    }

    /// <summary>
    /// Navigate to the next day
    /// </summary>
    public async Task GoToNextDayAsync()
    {
        var date = DateTime.Parse(_currentDate).AddDays(1);
        // Don't go past today
        if (date > DateTime.Now.Date)
            return;

        _currentDate = date.ToString("yyyy-MM-dd");
        await RefreshTasksAsync();
    }

    /// <summary>
    /// Jump back to today
    /// </summary>
    public async Task GoToTodayAsync()
    {
        _currentDate = DateTime.Now.ToString("yyyy-MM-dd");
        await RefreshTasksAsync();
    }

    /// <summary>
    /// Whether the current date is today
    /// </summary>
    public bool IsToday => _currentDate == DateTime.Now.ToString("yyyy-MM-dd");

    /// <summary>
    /// Search across all tasks
    /// </summary>
    public async Task<List<TaskItem>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<TaskItem>();

        return await _dataStore.SearchTasksAsync(query);
    }

    /// <summary>
    /// Get active/completed counts for current date
    /// </summary>
    public async Task<(int active, int completed)> GetCountsAsync()
    {
        return await _dataStore.GetTaskCountsAsync(_currentDate);
    }

    /// <summary>
    /// Get the most recent dates that have tasks
    /// </summary>
    public async Task<List<string>> GetRecentDatesAsync()
    {
        return await _dataStore.GetRecentTaskDatesAsync(_config.DaysToShow);
    }

    /// <summary>
    /// Update and save config
    /// </summary>
    public async Task SaveConfigAsync()
    {
        await _config.SaveAsync();
    }

    /// <summary>
    /// Apply config changes from settings, save, refresh, and notify the widget
    /// </summary>
    public async Task ApplyConfigAsync()
    {
        await _config.SaveAsync();
        await RefreshTasksAsync();
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Reload tasks for the current date from the database
    /// </summary>
    public async Task RefreshTasksAsync()
    {
        _currentTasks = await _dataStore.GetTasksByDateAsync(_currentDate);

        // Apply sorting based on config
        _currentTasks = SortTasks(_currentTasks);

        // Filter completed if configured
        if (!_config.ShowCompletedTasks)
        {
            _currentTasks = _currentTasks.Where(t => !t.IsCompleted).ToList();
        }

        TasksChanged?.Invoke(this, EventArgs.Empty);
    }

    private List<TaskItem> SortTasks(List<TaskItem> tasks)
    {
        return _config.SortBy switch
        {
            "priority" => tasks
                .OrderBy(t => t.IsCompleted)
                .ThenBy(t => t.Priority switch { "high" => 0, "normal" => 1, "low" => 2, _ => 1 })
                .ThenBy(t => t.SortOrder)
                .ToList(),
            "created" => tasks
                .OrderBy(t => t.IsCompleted)
                .ThenBy(t => t.CreatedAt)
                .ToList(),
            _ => tasks // "manual" — use sort_order as-is
                .OrderBy(t => t.IsCompleted)
                .ThenBy(t => t.SortOrder)
                .ToList()
        };
    }

    /// <summary>
    /// If autoCarryOver is on, copy yesterday's incomplete tasks into today
    /// </summary>
    private async Task PerformCarryOverAsync()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var yesterday = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");

        // Only carry over if today has zero tasks
        var todayTasks = await _dataStore.GetTasksByDateAsync(today);
        if (todayTasks.Count > 0)
            return;

        var incomplete = await _dataStore.GetIncompleteTasksAsync(yesterday);
        if (incomplete.Count == 0)
            return;

        var sortOrder = 0;
        foreach (var oldTask in incomplete)
        {
            var newTask = new TaskItem
            {
                Id = Guid.NewGuid().ToString(),
                Date = today,
                Title = oldTask.Title,
                Priority = oldTask.Priority,
                SortOrder = sortOrder++,
                CreatedAt = DateTime.Now,
                Category = oldTask.Category,
                Notes = oldTask.Notes
            };

            await _dataStore.UpsertTaskAsync(newTask);
        }
    }
}
