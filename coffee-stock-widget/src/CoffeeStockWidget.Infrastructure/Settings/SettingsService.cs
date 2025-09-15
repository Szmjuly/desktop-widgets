using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CoffeeStockWidget.Core.Models;

namespace CoffeeStockWidget.Infrastructure.Settings;

public class SettingsService
{
    private readonly string _settingsPath;

    public SettingsService(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? GetDefaultPath();
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
    }

    private static string GetDefaultPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "CoffeeStockWidget");
        return Path.Combine(dir, "settings.json");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_settingsPath)) return new AppSettings();
        await using var fs = File.OpenRead(_settingsPath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(fs, cancellationToken: ct);
        return settings ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        await using var fs = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(fs, settings, opts, ct);
    }
}
