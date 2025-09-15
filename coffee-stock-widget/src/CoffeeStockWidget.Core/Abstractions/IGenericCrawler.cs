using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoffeeStockWidget.Core.Models;

namespace CoffeeStockWidget.Core.Abstractions;

public interface IGenericCrawler
{
    Task<IReadOnlyList<CoffeeItem>> CrawlAsync(Uri root, CancellationToken ct = default);
}
