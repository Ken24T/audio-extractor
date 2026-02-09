using AudioExtractor.Core;

namespace AudioExtractor;

public static class Program
{
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

            var service = new AudioExtractionService();
            var reporter = new ConsoleExtractionReporter();
            var result = service.Run(new ExtractionOptions
            {
                InputFile = options.InputFile ?? string.Empty,
                Output = options.Output,
                Start = options.Start,
                End = options.End,
                Duration = options.Duration,
                SampleRate = options.SampleRate,
                Channels = options.Channels,
                FfmpegPath = options.FfmpegPath,
                NoTts = options.NoTts,
                TtsSampleRate = options.TtsSampleRate,
                TtsHighpassHz = options.TtsHighpassHz,
                TtsLowpassHz = options.TtsLowpassHz,
                TargetLufs = options.TargetLufs,
                Force = options.Force,
                Autoplay = options.Autoplay,
                Verbose = options.Verbose
            }, reporter);

            if (!result.Success && !string.IsNullOrWhiteSpace(result.Error))
            {
                return Task.FromResult(Fail(result.Error, result.ExitCode));
            }

            return Task.FromResult(result.ExitCode);
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
        Console.WriteLine($"  --tts-sample-rate <int>  Default {ExtractionDefaults.TtsSampleRate}");
        Console.WriteLine($"  --tts-highpass-hz <int>  Default {ExtractionDefaults.TtsHighpassHz}");
        Console.WriteLine($"  --tts-lowpass-hz <int>   Default {ExtractionDefaults.TtsLowpassHz}");
        Console.WriteLine($"  --target-lufs <int>      Default {ExtractionDefaults.TargetLufs}");
        Console.WriteLine();
        Console.WriteLine("NOTE:");
        Console.WriteLine("  ffmpeg is required on PATH (or use --ffmpeg-path).");
        Console.WriteLine("  If ffprobe is available, input duration guards are enforced.");
        Console.WriteLine();
        Console.WriteLine("EXAMPLES:");
        Console.WriteLine("  audio-extractor Lockdown.mp4");
        Console.WriteLine("  audio-extractor Lockdown.mp4 --start 00:01:00 --duration 00:00:20");
        Console.WriteLine("  audio-extractor Lockdown.mp4 --start 00:01:00 --end 00:01:20");
    }

    internal static Options ParseArgs(string[] args)
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
            TtsSampleRate = ExtractionDefaults.TtsSampleRate,
            TtsHighpassHz = ExtractionDefaults.TtsHighpassHz,
            TtsLowpassHz = ExtractionDefaults.TtsLowpassHz,
            TargetLufs = ExtractionDefaults.TargetLufs
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
                case "--autoplay":
                case "-Autoplay":
                    options.Autoplay = true;
                    i++;
                    break;
                case "--verbose":
                case "-Verbose":
                    options.Verbose = true;
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

    internal sealed class Options
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
        public bool Autoplay { get; set; }
        public bool Verbose { get; set; }
        public bool ShowHelp { get; set; }

        public static Options WithHelp() => new() { ShowHelp = true };
    }

    private static int Fail(string message, int code = 2)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ResetColor();
        return code;
    }

    private sealed class ConsoleExtractionReporter : IExtractionReporter
    {
        public void Info(string message)
        {
            Console.WriteLine(message);
        }

        public void Warn(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Error.WriteLine(message);
            Console.ResetColor();
        }

        public void Error(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(message);
            Console.ResetColor();
        }
    }
}
