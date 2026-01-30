using System.Diagnostics;

namespace AudioExtractor;

internal static class Program
{
    private const int DefaultTtsSampleRate = 24000;
    private const int DefaultTtsHighpassHz = 80;
    private const int DefaultTtsLowpassHz = 11000;
    private const int DefaultTargetLufs = -16;

    public static Task<int> Main(string[] args)
    {
        try
        {
            var options = ParseArgs(args);
            if (options.ShowHelp)
            {
                ShowHelp();
                return Task.FromResult(0);
            }

            if (string.IsNullOrWhiteSpace(options.InputFile))
            {
                ShowHelp();
                return Task.FromResult(1);
            }

            if (options.Channels is not null && options.Channels is not (1 or 2))
            {
                return Task.FromResult(Fail("Channels must be 1 or 2."));
            }

            var exitCode = Run(
                options.InputFile,
                options.Output,
                options.Start,
                options.End,
                options.Duration,
                options.SampleRate,
                options.Channels,
                options.FfmpegPath,
                options.NoTts,
                options.TtsSampleRate,
                options.TtsHighpassHz,
                options.TtsLowpassHz,
                options.TargetLufs,
                options.Force);

            return Task.FromResult(exitCode);
        }
        catch (ArgumentException ex)
        {
            return Task.FromResult(Fail(ex.Message));
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine("audio-extractor - Extract audio using ffmpeg");
        Console.WriteLine();
        Console.WriteLine("DEFAULT: Qwen3-friendly WAV (mono, 24kHz, speech filtered, loudnorm)");
        Console.WriteLine();
        Console.WriteLine("USAGE:");
        Console.WriteLine("  audio-extractor <inputFile> [options]");
        Console.WriteLine();
        Console.WriteLine("OPTIONS:");
        Console.WriteLine("  --start <time>           HH:MM:SS | MM:SS | SS");
        Console.WriteLine("  --end <time>             Requires --start");
        Console.WriteLine("  --duration <time>        Requires --start");
        Console.WriteLine();
        Console.WriteLine("  --output <file>          Output filename (auto if omitted)");
        Console.WriteLine("  --no-tts                 Preserve original format");
        Console.WriteLine("  --force                  Overwrite output file");
        Console.WriteLine("  --ffmpeg-path <path>     Path to ffmpeg.exe");
        Console.WriteLine();
        Console.WriteLine("  --sample-rate <int>      Only in --no-tts mode");
        Console.WriteLine("  --channels <1|2>         Only in --no-tts mode");
        Console.WriteLine();
        Console.WriteLine($"  --tts-sample-rate <int>  Default {DefaultTtsSampleRate}");
        Console.WriteLine($"  --tts-highpass-hz <int>  Default {DefaultTtsHighpassHz}");
        Console.WriteLine($"  --tts-lowpass-hz <int>   Default {DefaultTtsLowpassHz}");
        Console.WriteLine($"  --target-lufs <int>      Default {DefaultTargetLufs}");
        Console.WriteLine();
        Console.WriteLine("EXAMPLES:");
        Console.WriteLine("  audio-extractor Lockdown.mp4");
        Console.WriteLine("  audio-extractor Lockdown.mp4 --start 00:01:00 --duration 00:00:20");
        Console.WriteLine("  audio-extractor Lockdown.mp4 --start 00:01:00 --end 00:01:20");
    }

    private static Options ParseArgs(string[] args)
    {
        if (args.Length == 0)
        {
            return Options.WithHelp();
        }

        if (IsHelpToken(args[0]))
        {
            return Options.WithHelp();
        }

        var options = new Options
        {
            InputFile = args[0],
            TtsSampleRate = DefaultTtsSampleRate,
            TtsHighpassHz = DefaultTtsHighpassHz,
            TtsLowpassHz = DefaultTtsLowpassHz,
            TargetLufs = DefaultTargetLufs
        };

        var i = 1;
        while (i < args.Length)
        {
            var token = args[i];
            if (IsHelpToken(token))
            {
                return Options.WithHelp();
            }

            switch (token)
            {
                case "--output":
                case "-o":
                case "-Output":
                    options.Output = ReadValue(args, ref i, token);
                    break;
                case "--start":
                case "-Start":
                    options.Start = ReadValue(args, ref i, token);
                    break;
                case "--end":
                case "-End":
                    options.End = ReadValue(args, ref i, token);
                    break;
                case "--duration":
                case "-Duration":
                    options.Duration = ReadValue(args, ref i, token);
                    break;
                case "--sample-rate":
                case "-SampleRate":
                    options.SampleRate = ReadInt(args, ref i, token);
                    break;
                case "--channels":
                case "-Channels":
                    options.Channels = ReadInt(args, ref i, token);
                    break;
                case "--ffmpeg-path":
                case "-FfmpegPath":
                    options.FfmpegPath = ReadValue(args, ref i, token);
                    break;
                case "--no-tts":
                case "-NoTTS":
                    options.NoTts = true;
                    i++;
                    break;
                case "--force":
                case "-Force":
                    options.Force = true;
                    i++;
                    break;
                case "--tts-sample-rate":
                    options.TtsSampleRate = ReadInt(args, ref i, token);
                    break;
                case "--tts-highpass-hz":
                    options.TtsHighpassHz = ReadInt(args, ref i, token);
                    break;
                case "--tts-lowpass-hz":
                    options.TtsLowpassHz = ReadInt(args, ref i, token);
                    break;
                case "--target-lufs":
                    options.TargetLufs = ReadInt(args, ref i, token);
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {token}");
            }
        }

        return options;
    }

    private static bool IsHelpToken(string token)
    {
        return token is "--help" or "-h" or "/?" or "-?";
    }

    private static string ReadValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {optionName}.");
        }

        var value = args[index + 1];
        index += 2;
        return value;
    }

    private static int ReadInt(string[] args, ref int index, string optionName)
    {
        var raw = ReadValue(args, ref index, optionName);
        if (!int.TryParse(raw, out var value))
        {
            throw new ArgumentException($"Invalid integer for {optionName}: {raw}");
        }

        return value;
    }

    private sealed class Options
    {
        public string? InputFile { get; init; }
        public string? Output { get; set; }
        public string? Start { get; set; }
        public string? End { get; set; }
        public string? Duration { get; set; }
        public int? SampleRate { get; set; }
        public int? Channels { get; set; }
        public string? FfmpegPath { get; set; }
        public bool NoTts { get; set; }
        public int TtsSampleRate { get; set; }
        public int TtsHighpassHz { get; set; }
        public int TtsLowpassHz { get; set; }
        public int TargetLufs { get; set; }
        public bool Force { get; set; }
        public bool ShowHelp { get; set; }

        public static Options WithHelp() => new() { ShowHelp = true };
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
