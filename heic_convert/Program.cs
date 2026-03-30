using HeicConvert.Core;

namespace HeicConvert.Cli;

internal static class Program
{
    // WPF/WIC requires STA for reliable codec use from a console host.
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        if (args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
        {
            PrintHelp();
            return 0;
        }

        var options = ParseArgs(args);
        if (!TryResolveMissingValuesInteractively(options))
        {
            return 1;
        }

        if (string.IsNullOrWhiteSpace(options.InputPath) || string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            Console.Error.WriteLine("Input path and output directory are required.");
            PrintHelp();
            return 1;
        }

        if (!Directory.Exists(options.InputPath) && !File.Exists(options.InputPath))
        {
            Console.Error.WriteLine($"Input path does not exist: {options.InputPath}");
            return 1;
        }

        Directory.CreateDirectory(options.OutputDirectory);

        var batch = HeicConverter.RunConversion(
            options,
            onSourceFilesReady: count =>
            {
                if (count == 0)
                {
                    Console.WriteLine("No HEIC/HEIF files found.");
                }
                else
                {
                    Console.WriteLine($"Found {count} file(s). Starting conversion...");
                    Console.WriteLine();
                }
            },
            onConverted: (src, dest) => Console.WriteLine($"[OK] {src} -> {dest}"),
            onSkipped: dest => Console.WriteLine($"[SKIP] Exists: {dest}"),
            onFailed: (src, err) =>
            {
                Console.WriteLine($"[FAIL] {src}");
                Console.WriteLine(err);
            });

        if (batch.TotalSourceFiles == 0)
        {
            return 0;
        }

        Console.WriteLine();
        Console.WriteLine("Done.");
        Console.WriteLine($"Converted: {batch.Converted}");
        Console.WriteLine($"Skipped:   {batch.Skipped}");
        Console.WriteLine($"Failed:    {batch.Failed}");

        return batch.Failed > 0 ? 2 : 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("HEIC Convert CLI (core functionality)");
        Console.WriteLine();
        Console.WriteLine("Windows only: uses WPF/WIC (built into .NET Windows Desktop) — no extra NuGet packages.");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  heic-convert --input <path> --output-dir <path> [--format jpg|png] [--quality 1-100]");
        Console.WriteLine("              [--recursive] [--overwrite]");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine(@"  heic-convert --input ""C:\Photos\HEIC"" --output-dir ""C:\Photos\Export"" --format jpg --quality 90 --recursive");
        Console.WriteLine(@"  heic-convert --input ""C:\Photos\img1.heic"" --output-dir ""C:\Photos\Export"" --format png");
        Console.WriteLine();
        Console.WriteLine("Quality details:");
        Console.WriteLine("  - JPEG: 1-100 maps to JpegBitmapEncoder.QualityLevel (higher = larger file, fewer artifacts).");
        Console.WriteLine("  - PNG: effectively lossless; quality has little practical impact.");
        Console.WriteLine();
        Console.WriteLine("HEIC decode requires Microsoft \"HEIF Image Extension\" (Store). You already have this installed.");
    }

    private static ConvertOptions ParseArgs(string[] args)
    {
        var options = new ConvertOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i].Trim();

            switch (token.ToLowerInvariant())
            {
                case "--input":
                case "-i":
                    options.InputPath = GetValue(args, ref i, token);
                    break;
                case "--output-dir":
                case "-o":
                    options.OutputDirectory = GetValue(args, ref i, token);
                    break;
                case "--format":
                case "-f":
                    options.Format = HeicConverter.NormalizeFormat(GetValue(args, ref i, token));
                    break;
                case "--quality":
                case "-q":
                    var raw = GetValue(args, ref i, token);
                    if (!int.TryParse(raw, out var parsed) || parsed < 1 || parsed > 100)
                    {
                        throw new ArgumentException($"Invalid quality: {raw}. Must be 1-100.");
                    }
                    options.Quality = parsed;
                    break;
                case "--recursive":
                case "-r":
                    options.Recursive = true;
                    break;
                case "--overwrite":
                    options.Overwrite = true;
                    break;
                default:
                    break;
            }
        }

        return options;
    }

    private static string GetValue(string[] args, ref int i, string token)
    {
        if (i + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for argument {token}.");
        }
        i++;
        return args[i];
    }

    private static bool TryResolveMissingValuesInteractively(ConvertOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.InputPath))
        {
            Console.Write("Input file/folder path: ");
            options.InputPath = Console.ReadLine()?.Trim();
        }

        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            Console.Write("Output folder path: ");
            options.OutputDirectory = Console.ReadLine()?.Trim();
        }

        if (string.IsNullOrWhiteSpace(options.Format))
        {
            Console.Write("Output format (jpg/png) [jpg]: ");
            var entered = Console.ReadLine()?.Trim();
            options.Format = string.IsNullOrWhiteSpace(entered) ? "jpg" : HeicConverter.NormalizeFormat(entered);
        }

        if (options.Quality == 0)
        {
            Console.Write("Quality 1-100 [90]: ");
            var entered = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(entered))
            {
                options.Quality = 90;
            }
            else if (!int.TryParse(entered, out var parsed))
            {
                Console.Error.WriteLine("Quality must be a number from 1-100.");
                return false;
            }
            else
            {
                options.Quality = parsed;
            }
        }

        if (!options.Recursive.HasValue)
        {
            Console.Write("Recursive folder scan? (y/N): ");
            var entered = Console.ReadLine()?.Trim();
            options.Recursive = entered?.Equals("y", StringComparison.OrdinalIgnoreCase) == true;
        }

        if (!options.Overwrite.HasValue)
        {
            Console.Write("Overwrite existing files? (y/N): ");
            var entered = Console.ReadLine()?.Trim();
            options.Overwrite = entered?.Equals("y", StringComparison.OrdinalIgnoreCase) == true;
        }

        if (!new[] { "jpg", "png" }.Contains(options.Format))
        {
            Console.Error.WriteLine("Format must be one of: jpg, png. (WebP needs the Windows SDK NuGet package on your feed.)");
            return false;
        }

        if (options.Quality is < 1 or > 100)
        {
            Console.Error.WriteLine("Quality must be 1-100.");
            return false;
        }

        return true;
    }
}
