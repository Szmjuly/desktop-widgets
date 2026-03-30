using System.IO;
using System.Windows.Media.Imaging;

namespace HeicConvert.Core;

public static class HeicConverter
{
    public static string NormalizeFormat(string value) =>
        value.Trim().TrimStart('.').ToLowerInvariant() switch
        {
            "jpeg" => "jpg",
            var x => x
        };

    /// <summary>
    /// Runs the same conversion workflow as the CLI for a single input path (file or folder).
    /// </summary>
    public static BatchConversionResult RunConversion(
        ConvertOptions options,
        IProgress<ConversionProgress>? progress = null,
        Action<int>? onSourceFilesReady = null,
        Action<string, string>? onConverted = null,
        Action<string>? onSkipped = null,
        Action<string, string>? onFailed = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.InputPath) || string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            throw new ArgumentException("Input path and output directory are required.");
        }

        if (!Directory.Exists(options.InputPath) && !File.Exists(options.InputPath))
        {
            throw new ArgumentException($"Input path does not exist: {options.InputPath}");
        }

        Directory.CreateDirectory(options.OutputDirectory);

        var sourceFiles = CollectSourceFiles(options.InputPath, options.Recursive.GetValueOrDefault()).ToList();
        onSourceFilesReady?.Invoke(sourceFiles.Count);
        if (sourceFiles.Count == 0)
        {
            return new BatchConversionResult { TotalSourceFiles = 0 };
        }

        var converted = 0;
        var skipped = 0;
        var failed = 0;
        var total = sourceFiles.Count;

        for (var i = 0; i < sourceFiles.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = sourceFiles[i];
            progress?.Report(new ConversionProgress
            {
                Message = source,
                CurrentIndex = i + 1,
                Total = total
            });

            var destination = BuildDestinationPath(source, options.InputPath!, options.OutputDirectory, options.Format);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            if (File.Exists(destination) && !options.Overwrite.GetValueOrDefault())
            {
                onSkipped?.Invoke(destination);
                skipped++;
                continue;
            }

            var result = ConvertFile(source, destination, options.Format, options.Quality);
            if (result.Success)
            {
                onConverted?.Invoke(source, destination);
                converted++;
            }
            else
            {
                onFailed?.Invoke(source, result.ErrorMessage);
                failed++;
            }
        }

        return new BatchConversionResult
        {
            TotalSourceFiles = total,
            Converted = converted,
            Skipped = skipped,
            Failed = failed
        };
    }

    public static IEnumerable<string> CollectSourceFiles(string inputPath, bool recursive)
    {
        if (File.Exists(inputPath))
        {
            if (IsHeicOrHeif(inputPath))
            {
                yield return Path.GetFullPath(inputPath);
            }
            yield break;
        }

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        foreach (var path in Directory.EnumerateFiles(inputPath, "*.*", option))
        {
            if (IsHeicOrHeif(path))
            {
                yield return path;
            }
        }
    }

    public static bool IsHeicOrHeif(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".heic", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".heif", StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildDestinationPath(string sourceFile, string inputRoot, string outputRoot, string format)
    {
        if (File.Exists(inputRoot))
        {
            var fileName = Path.GetFileNameWithoutExtension(sourceFile);
            return Path.Combine(outputRoot, $"{fileName}.{format}");
        }

        var relativePath = Path.GetRelativePath(inputRoot, sourceFile);
        var changedExtension = Path.ChangeExtension(relativePath, format);
        return Path.Combine(outputRoot, changedExtension);
    }

    public static ConversionResult ConvertFile(string sourceFile, string destinationFile, string format, int quality)
    {
        try
        {
            using var readStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            var decoder = BitmapDecoder.Create(readStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];

            BitmapEncoder encoder = format.ToLowerInvariant() switch
            {
                "jpg" => new JpegBitmapEncoder { QualityLevel = quality },
                "png" => new PngBitmapEncoder(),
                _ => throw new ArgumentException($"Unsupported format: {format}")
            };

            encoder.Frames.Add(BitmapFrame.Create(frame));

            using var writeStream = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None);
            encoder.Save(writeStream);

            return ConversionResult.Ok();
        }
        catch (Exception ex)
        {
            return ConversionResult.Fail(ex.ToString());
        }
    }
}
