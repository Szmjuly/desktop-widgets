using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoffeeStockWidget.Core.Models;

namespace CoffeeStockWidget.Core.Abstractions;

public interface IDataStore
{
    Task UpsertSourceAsync(Source source, CancellationToken ct = default);
    Task UpsertItemsAsync(IEnumerable<CoffeeItem> items, CancellationToken ct = default);
    Task<IReadOnlyList<CoffeeItem>> GetItemsBySourceAsync(int sourceId, CancellationToken ct = default);
    Task RecordEventsAsync(IEnumerable<StockChangeEvent> eventsToRecord, CancellationToken ct = default);
    Task PruneAsync(RetentionPolicy policy, CancellationToken ct = default);
}
