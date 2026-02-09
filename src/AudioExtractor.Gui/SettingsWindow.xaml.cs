using System.Windows;
using Microsoft.Win32;

namespace AudioExtractor.Gui;

public partial class SettingsWindow : Window
{
    public SettingsWindow(string? ffmpegPath)
    {
        InitializeComponent();
        FfmpegPathTextBox.Text = ffmpegPath ?? string.Empty;
    }

    public string? FfmpegPath => string.IsNullOrWhiteSpace(FfmpegPathTextBox.Text)
        ? null
        : FfmpegPathTextBox.Text.Trim();

    private void OnBrowseFfmpeg(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select ffmpeg.exe",
            Filter = "ffmpeg.exe|ffmpeg.exe|All files|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            FfmpegPathTextBox.Text = dialog.FileName;
        }
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
