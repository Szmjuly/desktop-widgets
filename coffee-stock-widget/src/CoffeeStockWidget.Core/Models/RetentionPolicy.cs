namespace CoffeeStockWidget.Core.Models;

public class RetentionPolicy
{
    public int ItemsPerSource { get; set; } = 500;
    public int EventsPerSource { get; set; } = 1000;
    public int Days { get; set; } = 30;
}
