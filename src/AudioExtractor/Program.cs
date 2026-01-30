using System.CommandLine;
using System.Diagnostics;

namespace AudioExtractor;

internal static class Program
{
    private const int DefaultTtsSampleRate = 24000;
    private const int DefaultTtsHighpassHz = 80;
    private const int DefaultTtsLowpassHz = 11000;
    private const int DefaultTargetLufs = -16;

    public static async Task<int> Main(string[] args)
    {
        var inputArg = new Argument<string>("inputFile")
        {
            Arity = ArgumentArity.ExactlyOne,
            Description = "Input media file"
        };

        var outputOption = new Option<string?>(
            new[] { "--output", "-o", "-Output" },
            "Output filename (auto if omitted)");

        var startOption = new Option<string?>(
            new[] { "--start", "-Start" },
            "Start time (HH:MM:SS | MM:SS | SS)");

        var endOption = new Option<string?>(
            new[] { "--end", "-End" },
            "End time (requires --start)");

        var durationOption = new Option<string?>(
            new[] { "--duration", "-Duration" },
            "Duration (requires --start)");

        var sampleRateOption = new Option<int?>(
            new[] { "--sample-rate", "-SampleRate" },
            "Sample rate for --no-tts mode");

        var channelsOption = new Option<int?>(
            new[] { "--channels", "-Channels" },
            "Channels for --no-tts mode (1 or 2)");

        var ffmpegPathOption = new Option<string?>(
            new[] { "--ffmpeg-path", "-FfmpegPath" },
            "Path to ffmpeg.exe");

        var noTtsOption = new Option<bool>(
            new[] { "--no-tts", "-NoTTS" },
            "Preserve original format");

        var ttsSampleRateOption = new Option<int>(
            "--tts-sample-rate",
            getDefaultValue: () => DefaultTtsSampleRate,
            description: "TTS sample rate (default 24000)");

        var ttsHighpassOption = new Option<int>(
            "--tts-highpass-hz",
            getDefaultValue: () => DefaultTtsHighpassHz,
            description: "High-pass filter cutoff (default 80)");

        var ttsLowpassOption = new Option<int>(
            "--tts-lowpass-hz",
            getDefaultValue: () => DefaultTtsLowpassHz,
            description: "Low-pass filter cutoff (default 11000)");

        var targetLufsOption = new Option<int>(
            "--target-lufs",
            getDefaultValue: () => DefaultTargetLufs,
            description: "Target loudness (default -16 LUFS)");

        var forceOption = new Option<bool>(
            new[] { "--force", "-Force" },
            "Overwrite output file");

        var root = new RootCommand("Audio extraction using ffmpeg (Qwen3-friendly defaults)")
        {
            inputArg,
            outputOption,
            startOption,
            endOption,
            durationOption,
            sampleRateOption,
            channelsOption,
            ffmpegPathOption,
            noTtsOption,
            ttsSampleRateOption,
            ttsHighpassOption,
            ttsLowpassOption,
            targetLufsOption,
            forceOption
        };

        root.AddValidator(result =>
        {
            var channels = result.GetValueForOption(channelsOption);
            if (channels is not null && channels is not (1 or 2))
            {
                result.ErrorMessage = "Channels must be 1 or 2.";
            }
        });

        root.SetHandler((
            string inputFile,
            string? output,
            string? start,
            string? end,
            string? duration,
            int? sampleRate,
            int? channels,
            string? ffmpegPath,
            bool noTts,
            int ttsSampleRate,
            int ttsHighpassHz,
            int ttsLowpassHz,
            int targetLufs,
            bool force
        ) =>
        {
            Environment.ExitCode = Run(
                inputFile,
                output,
                start,
                end,
                duration,
                sampleRate,
                channels,
                ffmpegPath,
                noTts,
                ttsSampleRate,
                ttsHighpassHz,
                ttsLowpassHz,
                targetLufs,
                force);
        },
            inputArg,
            outputOption,
            startOption,
            endOption,
            durationOption,
            sampleRateOption,
            channelsOption,
            ffmpegPathOption,
            noTtsOption,
            ttsSampleRateOption,
            ttsHighpassOption,
            ttsLowpassOption,
            targetLufsOption,
            forceOption);

        return await root.InvokeAsync(args);
    }

    private static int Run(
        string inputFile,
        string? output,
        string? start,
        string? end,
        string? duration,
        int? sampleRate,
        int? channels,
        string? ffmpegPath,
        bool noTts,
        int ttsSampleRate,
        int ttsHighpassHz,
        int ttsLowpassHz,
        int targetLufs,
        bool force)
    {
        try
        {
            if (!File.Exists(inputFile))
            {
                return Fail($"Input file not found: {inputFile}");
            }

            if (!string.IsNullOrWhiteSpace(end) && !string.IsNullOrWhiteSpace(duration))
            {
                return Fail("Use either --end OR --duration (not both).");
            }

            if ((end is not null || duration is not null) && string.IsNullOrWhiteSpace(start))
            {
                return Fail("Start time required when using --end/--duration.");
            }

            var startSec = ConvertToSeconds(start);
            var endSec = ConvertToSeconds(end);
            var durationSec = ConvertToSeconds(duration);

            if (durationSec is not null && durationSec <= 0)
            {
                return Fail("Duration must be > 0");
            }

            if (startSec is not null && endSec is not null && endSec <= startSec)
            {
                return Fail($"End time ({end}) must be AFTER Start time ({start}).");
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                output = BuildAutoOutputName(inputFile, noTts, start, end, duration);
            }

            if (!force)
            {
                var newPath = GetNonClobberPath(output);
                if (!string.Equals(newPath, output, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Output exists -> {newPath}");
                    output = newPath;
                }
            }

            var resolvedFfmpeg = ResolveFfmpegPath(ffmpegPath);

            var baseArgs = new List<string> { "-hide_banner", "-loglevel", "warning" };
            if (!string.IsNullOrWhiteSpace(start))
            {
                baseArgs.AddRange(new[] { "-ss", start });
            }

            baseArgs.AddRange(new[] { "-i", inputFile });

            if (!string.IsNullOrWhiteSpace(duration))
            {
                baseArgs.AddRange(new[] { "-t", duration });
            }

            if (!string.IsNullOrWhiteSpace(end))
            {
                baseArgs.AddRange(new[] { "-to", end });
            }

            var wavArgs = new List<string> { "-vn", "-c:a", "pcm_s16le" };
            var outArgs = new List<string> { force ? "-y" : "-n", output };

            if (noTts)
            {
                if (sampleRate.HasValue)
                {
                    wavArgs.AddRange(new[] { "-ar", sampleRate.Value.ToString() });
                }

                if (channels.HasValue)
                {
                    wavArgs.AddRange(new[] { "-ac", channels.Value.ToString() });
                }

                RunFfmpeg(resolvedFfmpeg, baseArgs.Concat(wavArgs).Concat(outArgs));
                Console.WriteLine($"Done -> {output}");
                return 0;
            }

            var filter = $"highpass=f={ttsHighpassHz},lowpass=f={ttsLowpassHz},aresample={ttsSampleRate},loudnorm=I={targetLufs}:TP=-1.5:LRA=11";

            var ttsArgs = baseArgs
                .Concat(wavArgs)
                .Concat(new[] { "-af", filter, "-ac", "1", "-ar", ttsSampleRate.ToString() })
                .Concat(outArgs);

            RunFfmpeg(resolvedFfmpeg, ttsArgs);
            Console.WriteLine($"Done -> {output}");
            return 0;
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    private static int? ConvertToSeconds(string? timeText)
    {
        if (string.IsNullOrWhiteSpace(timeText))
        {
            return null;
        }

        var parts = timeText.Trim().Split(':');
        if (parts.Length > 3)
        {
            throw new ArgumentException($"Invalid time format: {timeText} (use SS, MM:SS, or HH:MM:SS)");
        }

        var numbers = parts.Select(part =>
        {
            if (!int.TryParse(part, out var value))
            {
                throw new ArgumentException($"Invalid time format: {timeText}");
            }

            return value;
        }).ToList();

        while (numbers.Count < 3)
        {
            numbers.Insert(0, 0);
        }

        var hours = numbers[0];
        var minutes = numbers[1];
        var seconds = numbers[2];

        return (hours * 3600) + (minutes * 60) + seconds;
    }

    private static string BuildAutoOutputName(
        string inputPath,
        bool isNoTts,
        string? start,
        string? end,
        string? duration)
    {
        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        var dir = Path.GetDirectoryName(inputPath);
        if (string.IsNullOrWhiteSpace(dir))
        {
            dir = ".";
        }

        var mode = isNoTts ? "_out" : "_tts";
        var tags = new List<string>();

        if (!string.IsNullOrWhiteSpace(start))
        {
            tags.Add($"s{ToFileTimeToken(start)}");
        }

        if (!string.IsNullOrWhiteSpace(end))
        {
            tags.Add($"e{ToFileTimeToken(end)}");
        }

        if (!string.IsNullOrWhiteSpace(duration))
        {
            tags.Add($"d{ToFileTimeToken(duration)}");
        }

        var tag = tags.Count > 0 ? "_" + string.Join("_", tags) : string.Empty;
        return Path.Combine(dir, $"{baseName}{mode}{tag}.wav");
    }

    private static string ToFileTimeToken(string timeText)
    {
        return timeText.Trim().Replace(":", "-").Replace(" ", string.Empty);
    }

    private static string GetNonClobberPath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var dir = Path.GetDirectoryName(path) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);

        for (var i = 1; i <= 9999; i++)
        {
            var candidate = Path.Combine(dir, $"{baseName}_{i:D3}{ext}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(dir, $"{baseName}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
    }

    private static string ResolveFfmpegPath(string? providedPath)
    {
        if (!string.IsNullOrWhiteSpace(providedPath))
        {
            if (!File.Exists(providedPath))
            {
                throw new FileNotFoundException($"ffmpeg not found: {providedPath}");
            }

            return Path.GetFullPath(providedPath);
        }

        if (!IsFfmpegOnPath())
        {
            throw new FileNotFoundException("ffmpeg not in PATH. Install or use --ffmpeg-path");
        }

        return "ffmpeg";
    }

    private static bool IsFfmpegOnPath()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "ffmpeg",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void RunFfmpeg(string ffmpegPath, IEnumerable<string> args)
    {
        var argList = args.ToList();
        Console.WriteLine("Running ffmpeg:");
        Console.WriteLine($"\"{ffmpegPath}\" {string.Join(" ", argList.Select(QuoteIfNeeded))}");

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };

        foreach (var arg in argList)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Failed to start ffmpeg process.");
        }

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffmpeg failed ({process.ExitCode})");
        }
    }

    private static string QuoteIfNeeded(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains(' ') || value.Contains('"'))
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        return value;
    }

    private static int Fail(string message, int code = 2)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ResetColor();
        return code;
    }
}
