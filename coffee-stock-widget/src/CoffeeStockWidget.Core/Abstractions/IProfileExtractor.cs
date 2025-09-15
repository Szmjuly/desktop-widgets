using System.Collections.Generic;
using CoffeeStockWidget.Core.Models;

namespace CoffeeStockWidget.Core.Abstractions;

public interface IProfileExtractor
{
    CoffeeProfile BuildProfile(string html, IDictionary<string, string[]> dictionaries);
}
