using System.Threading;
using System.Threading.Tasks;
using CoffeeStockWidget.Core.Models;

namespace CoffeeStockWidget.Core.Abstractions;

public interface IScheduler
{
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    Task RegisterAsync(Source source, CancellationToken ct = default);
    Task UpdateScheduleAsync(Source source, CancellationToken ct = default);
}
