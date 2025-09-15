using System;
using System.Collections.Generic;

namespace CoffeeStockWidget.Core.Models;

public enum StockEventType
{
    NewItem,
    BackInStock,
    PriceChanged,
    OutOfStock
}

public class StockChangeEvent
{
    public int? Id { get; set; }
    public int SourceId { get; set; }
    public int ItemId { get; set; }
    public StockEventType EventType { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string>? Data { get; set; }
}
