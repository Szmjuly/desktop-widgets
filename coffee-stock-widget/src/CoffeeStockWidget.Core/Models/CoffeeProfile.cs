using System.Collections.Generic;

namespace CoffeeStockWidget.Core.Models;

public class CoffeeProfile
{
    public string? Producer { get; set; }
    public string? Origin { get; set; }
    public string? Process { get; set; }
    public List<string>? TastingNotes { get; set; }
}
