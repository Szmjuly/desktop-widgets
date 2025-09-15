using System.Threading.Tasks;
using CoffeeStockWidget.Core.Models;

namespace CoffeeStockWidget.Core.Abstractions;

public interface ISettingsService
{
    Task<Settings> LoadAsync();
    Task SaveAsync(Settings settings);
}
