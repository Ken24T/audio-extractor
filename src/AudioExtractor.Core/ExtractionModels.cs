using System.Diagnostics;
using System.Globalization;

namespace AudioExtractor.Core;

public static class ExtractionDefaults
{
    public const int TtsSampleRate = 24000;
    public const int TtsHighpassHz = 80;
    public const int TtsLowpassHz = 11000;
    public const int TargetLufs = -16;
}

public sealed class ExtractionOptions
{
    public string InputFile { get; set; } = string.Empty;
    public string? Output { get; set; }
    public string? Start { get; set; }
    public string? End { get; set; }
    public string? Duration { get; set; }
    public int? SampleRate { get; set; }
    public int? Channels { get; set; }
    public string? FfmpegPath { get; set; }
    public bool NoTts { get; set; }
    public int TtsSampleRate { get; set; } = ExtractionDefaults.TtsSampleRate;
    public int TtsHighpassHz { get; set; } = ExtractionDefaults.TtsHighpassHz;
    public int TtsLowpassHz { get; set; } = ExtractionDefaults.TtsLowpassHz;
    public int TargetLufs { get; set; } = ExtractionDefaults.TargetLufs;
    public bool Force { get; set; }
    public bool Autoplay { get; set; }
    public bool Verbose { get; set; }
}

public sealed class ExtractionResult
{
    public bool Success { get; }
    public int ExitCode { get; }
    public string? OutputPath { get; }
    public string? Error { get; }

    private ExtractionResult(bool success, int exitCode, string? outputPath, string? error)
    {
        Success = success;
        ExitCode = exitCode;
        OutputPath = outputPath;
        Error = error;
    }

    public static ExtractionResult Ok(string? outputPath) => new(true, 0, outputPath, null);

    public static ExtractionResult Fail(string message, int exitCode = 2) => new(false, exitCode, null, message);
}

public interface IExtractionReporter
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}

public sealed class NullExtractionReporter : IExtractionReporter
{
    public static readonly NullExtractionReporter Instance = new();

    private NullExtractionReporter()
    {
    }

    public void Info(string message)
    {
    }

    public void Warn(string message)
    {
    }

    public void Error(string message)
    {
    }
}

public sealed class AudioExtractionService
{
    public ExtractionResult Run(ExtractionOptions options, IExtractionReporter? reporter = null)
    {
        reporter ??= NullExtractionReporter.Instance;

        try
        {
            if (string.IsNullOrWhiteSpace(options.InputFile))
            {
                return ExtractionResult.Fail("Input file is required.", exitCode: 1);
            }

            if (!File.Exists(options.InputFile))
            {
                return ExtractionResult.Fail($"Input file not found: {options.InputFile}");
            }

            if (options.Channels is not null && options.Channels is not (1 or 2))
            {
                return ExtractionResult.Fail("Channels must be 1 or 2.");
            }

            if (!string.IsNullOrWhiteSpace(options.End) && !string.IsNullOrWhiteSpace(options.Duration))
            {
                return ExtractionResult.Fail("Use either --end OR --duration (not both).");
            }

            if ((options.End is not null || options.Duration is not null) && string.IsNullOrWhiteSpace(options.Start))
            {
                return ExtractionResult.Fail("Start time required when using --end/--duration.");
            }

            var startSec = AudioExtractionUtils.ConvertToSeconds(options.Start);
            var endSec = AudioExtractionUtils.ConvertToSeconds(options.End);
            var durationSec = AudioExtractionUtils.ConvertToSeconds(options.Duration);

            if (durationSec is not null && durationSec <= 0)
            {
                return ExtractionResult.Fail("Duration must be > 0");
            }

            if (startSec is not null && endSec is not null && endSec <= startSec)
            {
                return ExtractionResult.Fail($"End time ({options.End}) must be AFTER Start time ({options.Start}).");
            }

            var mediaDuration = TryGetMediaDurationSeconds(options.InputFile, options.FfmpegPath);
            if (mediaDuration is not null)
            {
                const double epsilon = 0.0001;
                var total = mediaDuration.Value;

                if (startSec is not null && startSec.Value - total > epsilon)
                {
                    return ExtractionResult.Fail("Start time exceeds input duration.");
                }

                if (endSec is not null && endSec.Value - total > epsilon)
                {
                    return ExtractionResult.Fail("End time exceeds input duration.");
                }

                if (durationSec is not null && startSec is not null && (startSec.Value + durationSec.Value) - total > epsilon)
                {
                    return ExtractionResult.Fail("Start time + duration exceeds input duration.");
                }
            }

            if (string.IsNullOrWhiteSpace(options.Output))
            {
                options.Output = AudioExtractionUtils.BuildAutoOutputName(
                    options.InputFile,
                    options.NoTts,
                    options.Start,
                    options.End,
                    options.Duration);
            }

            if (!options.Force)
            {
                var newPath = GetNonClobberPath(options.Output);
                if (!string.Equals(newPath, options.Output, StringComparison.OrdinalIgnoreCase))
                {
                    reporter.Info($"Output exists -> {newPath}");
                    options.Output = newPath;
                }
            }

            var resolvedFfmpeg = ResolveFfmpegPath(options.FfmpegPath);

            var baseArgs = new List<string> { "-hide_banner", "-loglevel", "warning" };
            if (!string.IsNullOrWhiteSpace(options.Start))
            {
                baseArgs.AddRange(new[] { "-ss", options.Start });
            }

            baseArgs.AddRange(new[] { "-i", options.InputFile });

            if (!string.IsNullOrWhiteSpace(options.Duration))
            {
                baseArgs.AddRange(new[] { "-t", options.Duration });
            }
            else if (!string.IsNullOrWhiteSpace(options.End) && startSec.HasValue && endSec.HasValue)
            {
                var calculatedDuration = endSec.Value - startSec.Value;
                baseArgs.AddRange(new[] { "-t", calculatedDuration.ToString("F3", CultureInfo.InvariantCulture) });
            }

            var wavArgs = new List<string> { "-vn", "-c:a", "pcm_s16le" };
            var outArgs = new List<string> { options.Force ? "-y" : "-n", options.Output };

            if (options.NoTts)
            {
                if (options.SampleRate.HasValue)
                {
                    wavArgs.AddRange(new[] { "-ar", options.SampleRate.Value.ToString() });
                }

                if (options.Channels.HasValue)
                {
                    wavArgs.AddRange(new[] { "-ac", options.Channels.Value.ToString() });
                }

                RunFfmpeg(resolvedFfmpeg, baseArgs.Concat(wavArgs).Concat(outArgs), options.Verbose, reporter);
                reporter.Info($"Done -> {options.Output}");

                if (options.Autoplay)
                {
                    OpenFileInDefaultApp(options.Output, reporter);
                }

                return ExtractionResult.Ok(options.Output);
            }

            var filter = $"highpass=f={options.TtsHighpassHz},lowpass=f={options.TtsLowpassHz},aresample={options.TtsSampleRate},loudnorm=I={options.TargetLufs}:TP=-1.5:LRA=11";

            var ttsArgs = baseArgs
                .Concat(wavArgs)
                .Concat(new[] { "-af", filter, "-ac", "1", "-ar", options.TtsSampleRate.ToString() })
                .Concat(outArgs);

            RunFfmpeg(resolvedFfmpeg, ttsArgs, options.Verbose, reporter);
            reporter.Info($"Done -> {options.Output}");

            if (options.Autoplay)
            {
                OpenFileInDefaultApp(options.Output, reporter);
            }

            return ExtractionResult.Ok(options.Output);
        }
        catch (Exception ex)
        {
            return ExtractionResult.Fail(ex.Message);
        }
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

    private static double? TryGetMediaDurationSeconds(string inputFile, string? providedFfmpegPath)
    {
        var ffprobePath = ResolveFfprobePath(providedFfmpegPath);
        if (string.IsNullOrWhiteSpace(ffprobePath))
        {
            return null;
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffprobePath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.StartInfo.ArgumentList.Add("-v");
            process.StartInfo.ArgumentList.Add("error");
            process.StartInfo.ArgumentList.Add("-show_entries");
            process.StartInfo.ArgumentList.Add("format=duration");
            process.StartInfo.ArgumentList.Add("-of");
            process.StartInfo.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
            process.StartInfo.ArgumentList.Add(inputFile);

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                return null;
            }

            if (double.TryParse(output.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var duration))
            {
                return duration;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveFfprobePath(string? providedFfmpegPath)
    {
        if (!string.IsNullOrWhiteSpace(providedFfmpegPath))
        {
            try
            {
                var ffmpegFull = Path.GetFullPath(providedFfmpegPath);
                var ffmpegDir = Path.GetDirectoryName(ffmpegFull);
                if (!string.IsNullOrWhiteSpace(ffmpegDir))
                {
                    var candidate = Path.Combine(ffmpegDir, "ffprobe.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        return IsFfprobeOnPath() ? "ffprobe" : null;
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

    private static bool IsFfprobeOnPath()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "ffprobe",
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

    private static void RunFfmpeg(string ffmpegPath, IEnumerable<string> args, bool verbose, IExtractionReporter reporter)
    {
        var argList = args.ToList();
        if (verbose)
        {
            reporter.Info("Running ffmpeg:");
            reporter.Info($"\"{ffmpegPath}\" {string.Join(" ", argList.Select(QuoteIfNeeded))}");
        }

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

    private static void OpenFileInDefaultApp(string filePath, IExtractionReporter reporter)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            reporter.Warn($"Warning: Could not open file in default app: {ex.Message}");
        }
    }
}
