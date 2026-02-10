using System;
using System.IO;
using System.Text.Json;

namespace AudioExtractor.Gui;

/// <summary>
/// Persists user preferences to %LOCALAPPDATA%\AudioExtractor\settings.json.
/// All I/O is wrapped in try/catch — settings must never crash the app.
/// </summary>
public sealed class UserSettings
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudioExtractor");

    private static readonly string SettingsPath =
        Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    // Window placement (normal-state bounds, even when maximised)
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public string? WindowState { get; set; }

    // Application preferences
    public string? FfmpegPath { get; set; }

    /// <summary>
    /// Loads settings from disk. Returns a default instance on any failure.
    /// </summary>
    public static UserSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new UserSettings();

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<UserSettings>(json, JsonOptions) ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
    }

    /// <summary>
    /// Writes current settings to disk. Creates the directory if needed.
    /// </summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Settings are cosmetic — never crash.
        }
    }

    /// <summary>
    /// Returns true if window placement values have been saved previously.
    /// </summary>
    public bool HasWindowPlacement =>
        WindowLeft.HasValue && WindowTop.HasValue &&
        WindowWidth.HasValue && WindowHeight.HasValue;

    /// <summary>
    /// Checks whether the saved window rectangle is at least partially visible
    /// within the virtual screen (bounding box of all monitors).
    /// </summary>
    public bool IsWindowPositionValid()
    {
        if (!HasWindowPlacement)
            return false;

        const double margin = 50; // at least 50 DIP of the window must be on-screen

        double vsLeft = System.Windows.SystemParameters.VirtualScreenLeft;
        double vsTop = System.Windows.SystemParameters.VirtualScreenTop;
        double vsRight = vsLeft + System.Windows.SystemParameters.VirtualScreenWidth;
        double vsBottom = vsTop + System.Windows.SystemParameters.VirtualScreenHeight;

        double winRight = WindowLeft!.Value + WindowWidth!.Value;
        double winBottom = WindowTop!.Value + WindowHeight!.Value;

        // Window must overlap the virtual screen by at least `margin` pixels
        bool horizontalOk = WindowLeft.Value < vsRight - margin && winRight > vsLeft + margin;
        bool verticalOk = WindowTop.Value < vsBottom - margin && winBottom > vsTop + margin;

        return horizontalOk && verticalOk;
    }
}
