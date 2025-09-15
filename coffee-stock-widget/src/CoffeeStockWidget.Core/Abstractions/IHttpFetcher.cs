using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CoffeeStockWidget.Core.Abstractions;

public interface IHttpFetcher
{
    Task<string> GetStringAsync(Uri uri, IDictionary<string, string>? headers = null, CancellationToken ct = default);
}
