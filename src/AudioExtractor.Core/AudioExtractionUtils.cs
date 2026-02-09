using System.Globalization;

namespace AudioExtractor.Core;

public static class AudioExtractionUtils
{
    public static double? ConvertToSeconds(string? timeText)
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

        double seconds;
        int minutes = 0;
        int hours = 0;

        if (parts.Length == 1)
        {
            seconds = ParseSecondsPart(parts[0], timeText);
        }
        else if (parts.Length == 2)
        {
            minutes = ParseIntPart(parts[0], timeText);
            seconds = ParseSecondsPart(parts[1], timeText);
        }
        else
        {
            hours = ParseIntPart(parts[0], timeText);
            minutes = ParseIntPart(parts[1], timeText);
            seconds = ParseSecondsPart(parts[2], timeText);
        }

        return (hours * 3600) + (minutes * 60) + seconds;
    }

    public static string BuildAutoOutputName(
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

    public static string ToFileTimeToken(string timeText)
    {
        return timeText.Trim().Replace(":", "-").Replace(" ", string.Empty);
    }

    private static int ParseIntPart(string part, string originalText)
    {
        if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new ArgumentException($"Invalid time format: {originalText}");
        }

        return value;
    }

    private static double ParseSecondsPart(string part, string originalText)
    {
        if (!double.TryParse(part, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            throw new ArgumentException($"Invalid time format: {originalText}");
        }

        return value;
    }
}
