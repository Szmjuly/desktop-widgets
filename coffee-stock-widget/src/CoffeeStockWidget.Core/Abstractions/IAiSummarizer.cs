using System.Threading;
using System.Threading.Tasks;
using CoffeeStockWidget.Core.Models;

namespace CoffeeStockWidget.Core.Abstractions;

public interface IAiSummarizer
{
    Task<CoffeeAiSummary?> SummarizeAsync(AiSummarizerRequest request, AiSummarizerConfig config, CancellationToken ct = default);
}
