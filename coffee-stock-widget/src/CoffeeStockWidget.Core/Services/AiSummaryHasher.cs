using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CoffeeStockWidget.Core.Models;

namespace CoffeeStockWidget.Core.Services;

public static class AiSummaryHasher
{
    public static string ComputeFingerprint(CoffeeItem item)
    {
        var sb = new StringBuilder();
        sb.AppendLine(item.ItemKey);
        sb.AppendLine(item.Title);
        sb.AppendLine(item.PriceCents?.ToString() ?? string.Empty);
        if (item.Attributes != null)
        {
            foreach (var kv in item.Attributes.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                sb.Append(kv.Key);
                sb.Append('=');
                sb.AppendLine(kv.Value ?? string.Empty);
            }
        }
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes);
    }
}
