using System.IO;
using System.Text.Json;

namespace HeicConvert.App;

internal sealed class UiSettings
{
    public string OutputDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "HeicExport");
    public string Format { get; set; } = "jpg";
    public int Quality { get; set; } = 90;
    public bool Recursive { get; set; } = true;
    public bool Overwrite { get; set; }

    private static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HeicConvert", "settings.json");

    public static UiSettings Load()
    {
        try
        {
            var path = FilePath;
            if (!File.Exists(path))
            {
                return new UiSettings();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<UiSettings>(json) ?? new UiSettings();
        }
        catch
        {
            return new UiSettings();
        }
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }
}
