using System;

namespace CoffeeStockWidget.Core.Models;

public class AppSettings
{
    public int PollIntervalSeconds { get; set; } = 300;
    public bool RunAtLogin { get; set; } = false;
}
