using System.Collections.Generic;

namespace CoffeeStockWidget.Core.Models;

public class Settings
{
    public List<Source> Sources { get; set; } = new();
    public RetentionPolicy Retention { get; set; } = new();
    public NetworkSettings Network { get; set; } = new();
    public NotificationSettings Notification { get; set; } = new();
}

public class NetworkSettings
{
    public int MinDelayPerHostMs { get; set; } = 1500;
    public int TimeoutSeconds { get; set; } = 15;
}

public class NotificationSettings
{
    public bool Enabled { get; set; } = true;
    public QuietHours? QuietHours { get; set; }
}

public class QuietHours
{
    public string Start { get; set; } = "22:00";
    public string End { get; set; } = "07:00";
}
