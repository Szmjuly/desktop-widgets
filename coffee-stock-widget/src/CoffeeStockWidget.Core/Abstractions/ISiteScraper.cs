using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoffeeStockWidget.Core.Models;

namespace CoffeeStockWidget.Core.Abstractions;

public interface ISiteScraper
{
    Task<IReadOnlyList<CoffeeItem>> FetchAsync(Source source, CancellationToken ct = default);
}
