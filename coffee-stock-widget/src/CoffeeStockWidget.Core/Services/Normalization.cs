using System;
using System.Security.Cryptography;
using System.Text;

namespace CoffeeStockWidget.Core.Services;

public static class Normalization
{
    public static string ComputeStableKey(string title, Uri url)
    {
        var normalizedTitle = (title ?? string.Empty).Trim().ToLowerInvariant();
        var path = url?.AbsolutePath ?? string.Empty;
        using var sha1 = SHA1.Create();
        var bytes = Encoding.UTF8.GetBytes(normalizedTitle + "|" + path);
        var hash = sha1.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
