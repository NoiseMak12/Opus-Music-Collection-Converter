using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MusicCollectionConverter;

class Program
{
    // may want to update with more formats, but these are the ones I have in my collection
    private static readonly string[] ExtensionsToConvert = { ".flac", ".wav", ".m4a", ".mp3", ".ape"};

    static async Task Main(string[] args)
    {
        string sourcePath = null;
        string destinationDir = null;
        string bitrate = "128k"; // good bang for your buck bitrate 

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--help" || args[i] == "-h")
            {
                PrintHelp();
                return;
            }

            if ((args[i] == "--bitrate" || args[i] == "-b") && i + 1 < args.Length)
            {
                bitrate = args[i + 1];
                i++;
            }
            else if (sourcePath == null)
            {
                sourcePath = args[i];
            }
            else if (destinationDir == null)
            {
                destinationDir = args[i];
            }
        }

        if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationDir))
        {
            Console.WriteLine("Oops Missing required arguments.\n");
            PrintHelp();
            return;
        }

        await RunConversionAsync(sourcePath, destinationDir, bitrate);
    }

    static void PrintHelp()
    {
        Console.WriteLine("Music Collection Converter - Convert audio files to Opus and copy directory structure (with misc files)");
        Console.WriteLine("\nUsage:");
        Console.WriteLine("  MusicCollectionConverter <source> <destination> [options]");
        Console.WriteLine("\nOptions:");
        Console.WriteLine("  -b, --bitrate  The output bitrate for the Opus format (Default: 128k)");
        Console.WriteLine("  -h, --help     HELP...");
    }

    static async Task RunConversionAsync(string sourcePath, string destDir, string bitrate)
    {
        if (!Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        if (File.Exists(sourcePath))
        {
            await ProcessFileAsync(sourcePath, destDir, bitrate);
        }
        else if (Directory.Exists(sourcePath))
        {
            await ProcessDirectoryConcurrentlyAsync(sourcePath, destDir, bitrate);
        }
        else
        {
            Console.WriteLine($"Source path not found: {sourcePath}");
        }
    }

    static async Task ProcessDirectoryConcurrentlyAsync(string sourceDir, string destDir, string bitrate)
    {
        Console.WriteLine($"Scanning directory: {sourceDir}...");

        foreach (var dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dirPath.Replace(sourceDir, destDir));
        }

        var allFiles = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);
        Console.WriteLine($"Found {allFiles.Length} files. Starting parallel processing...");

        int completedCount = 0;

        // quite taxing and at that point disk IO becomes bottleneck, adjust to your will
        // max core usage (-1 so not freezing the whole system)
        //var parallelOptions = new ParallelOptions
        //{
        //    MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
        //};
        // limit concurrency to prevent disk thrashing and cpu bottlenecks
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 4 
        };

        await Parallel.ForEachAsync(allFiles, parallelOptions, async (sourceFile, cancellationToken) =>
        {
            string relativePath = Path.GetRelativePath(sourceDir, sourceFile);
            string destFileDir = Path.Combine(destDir, Path.GetDirectoryName(relativePath) ?? "");

            await ProcessFileAsync(sourceFile, destFileDir, bitrate);

            int progress = Interlocked.Increment(ref completedCount);
            Console.WriteLine($"[{progress}/{allFiles.Length}] Finished processing: {Path.GetFileName(sourceFile)}");
        });

        Console.WriteLine("\nSuccess! Conversion complete!");
        

    }

    static async Task ProcessFileAsync(string sourceFile, string destDir, string bitrate)
    {
        string extension = Path.GetExtension(sourceFile).ToLower();
        string fileName = Path.GetFileName(sourceFile);
        string destFile = Path.Combine(destDir, fileName);

        if (ExtensionsToConvert.Contains(extension))
        {
            string convertedDestFile = Path.ChangeExtension(destFile, ".opus");

            if (File.Exists(convertedDestFile))
            {
                return;
            }

            await ConvertAudioToOpusAsync(sourceFile, convertedDestFile, bitrate);
        }
        else
        {
            if (!File.Exists(destFile))
            {
                File.Copy(sourceFile, destFile, overwrite: false);
            }
        }
    }

    static async Task ConvertAudioToOpusAsync(string sourceFile, string destFile, string bitrate)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-nostdin -i \"{sourceFile}\" -c:a libopus -b:a {bitrate} -vbr on -y \"{destFile}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            process.StandardInput.Close();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            // timeout after 5 minutes to prevent FFmpeg from freezing indefinitely
            // remove this segment to disable timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
           
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (TaskCanceledException)
            {
                process.Kill();
                Console.WriteLine($"\nFFmpeg froze on {Path.GetFileName(sourceFile)} (Skipping)");
                return;
            }
            // end timeout code 

            await Task.WhenAll(stdoutTask, stderrTask);

            if (process.ExitCode != 0)
            {
                string errorOutput = await stderrTask;
                Console.WriteLine($"\n Conversion failed for {Path.GetFileName(sourceFile)}");
                Console.WriteLine($"        FFmpeg Error: {errorOutput}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n Failed to start FFmpeg for {Path.GetFileName(sourceFile)}. {ex.Message}");
        }
    }
}