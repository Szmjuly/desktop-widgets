using System.Collections.Generic;
using System.Linq;
using CoffeeStockWidget.Core.Models;

namespace CoffeeStockWidget.Core.Services;

public class ChangeDetector
{
    // Compares previous and current items, returns list of stock change events
    public IReadOnlyList<StockChangeEvent> Compare(IEnumerable<CoffeeItem> previous, IEnumerable<CoffeeItem> current)
    {
        var prevByKey = previous.ToDictionary(i => i.ItemKey);
        var events = new List<StockChangeEvent>();

        foreach (var item in current)
        {
            if (!prevByKey.TryGetValue(item.ItemKey, out var old))
            {
                events.Add(new StockChangeEvent { EventType = StockEventType.NewItem, ItemId = item.Id ?? 0, SourceId = item.SourceId });
                continue;
            }

            if (!old.InStock && item.InStock)
            {
                events.Add(new StockChangeEvent { EventType = StockEventType.BackInStock, ItemId = item.Id ?? 0, SourceId = item.SourceId });
            }
            else if (old.InStock && !item.InStock)
            {
                events.Add(new StockChangeEvent { EventType = StockEventType.OutOfStock, ItemId = item.Id ?? 0, SourceId = item.SourceId });
            }

            if (old.PriceCents != item.PriceCents)
            {
                events.Add(new StockChangeEvent { EventType = StockEventType.PriceChanged, ItemId = item.Id ?? 0, SourceId = item.SourceId });
            }
        }

        return events;
    }
}
