using System.Threading.Tasks;
using CoffeeStockWidget.Core.Models;

namespace CoffeeStockWidget.Core.Abstractions;

public interface INotificationService
{
    Task ShowToastAsync(StockChangeEvent e);
    Task ShowInWidgetBubbleAsync(StockChangeEvent e);
}
