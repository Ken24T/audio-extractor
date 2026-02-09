using System.Windows;
using Microsoft.Win32;
using AudioExtractor.Core;

namespace AudioExtractor.Gui;

public partial class MainWindow : Window
{
    private readonly AudioExtractionService _service = new();

    public MainWindow()
    {
        InitializeComponent();
        TtsSampleRateTextBox.Text = ExtractionDefaults.TtsSampleRate.ToString();
        TtsHighpassTextBox.Text = ExtractionDefaults.TtsHighpassHz.ToString();
        TtsLowpassTextBox.Text = ExtractionDefaults.TtsLowpassHz.ToString();
        TargetLufsTextBox.Text = ExtractionDefaults.TargetLufs.ToString();
    }

    private void OnBrowseInput(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select input media",
            Filter = "Media files|*.wav;*.mp3;*.mp4;*.m4a;*.flac;*.aac;*.ogg;*.mov;*.mkv|All files|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            InputTextBox.Text = dialog.FileName;
        }
    }

    private void OnBrowseOutput(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Select output file",
            Filter = "Wave files|*.wav|All files|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            OutputTextBox.Text = dialog.FileName;
        }
    }

    private void OnBrowseFfmpeg(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select ffmpeg.exe",
            Filter = "ffmpeg.exe|ffmpeg.exe|All files|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            FfmpegTextBox.Text = dialog.FileName;
        }
    }

    private async void OnRun(object sender, RoutedEventArgs e)
    {
        ClearLog();

        if (string.IsNullOrWhiteSpace(InputTextBox.Text))
        {
            MessageBox.Show(this, "Input file is required.", "Audio Extractor", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryReadOptionalInt(SampleRateTextBox.Text, "Sample Rate", out var sampleRate))
        {
            return;
        }

        if (!TryReadOptionalInt(ChannelsTextBox.Text, "Channels", out var channels))
        {
            return;
        }

        if (!TryReadRequiredInt(TtsSampleRateTextBox.Text, "TTS Sample Rate", out var ttsSampleRate))
        {
            return;
        }

        if (!TryReadRequiredInt(TtsHighpassTextBox.Text, "TTS Highpass Hz", out var ttsHighpass))
        {
            return;
        }

        if (!TryReadRequiredInt(TtsLowpassTextBox.Text, "TTS Lowpass Hz", out var ttsLowpass))
        {
            return;
        }

        if (!TryReadRequiredInt(TargetLufsTextBox.Text, "Target LUFS", out var targetLufs))
        {
            return;
        }

        RunButton.IsEnabled = false;
        var reporter = new UiReporter(this);
        var options = new ExtractionOptions
        {
            InputFile = InputTextBox.Text.Trim(),
            Output = NormalizeOptional(OutputTextBox.Text),
            Start = NormalizeOptional(StartTextBox.Text),
            End = NormalizeOptional(EndTextBox.Text),
            Duration = NormalizeOptional(DurationTextBox.Text),
            SampleRate = sampleRate,
            Channels = channels,
            FfmpegPath = NormalizeOptional(FfmpegTextBox.Text),
            NoTts = NoTtsCheckBox.IsChecked == true,
            TtsSampleRate = ttsSampleRate,
            TtsHighpassHz = ttsHighpass,
            TtsLowpassHz = ttsLowpass,
            TargetLufs = targetLufs,
            Force = ForceCheckBox.IsChecked == true,
            Autoplay = AutoplayCheckBox.IsChecked == true,
            Verbose = VerboseCheckBox.IsChecked == true
        };

        var result = await Task.Run(() => _service.Run(options, reporter));

        if (!result.Success)
        {
            MessageBox.Show(this, result.Error ?? "Extraction failed.", "Audio Extractor", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        else
        {
            MessageBox.Show(this, "Extraction complete.", "Audio Extractor", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        RunButton.IsEnabled = true;
    }

    private void OnClearLog(object sender, RoutedEventArgs e)
    {
        ClearLog();
    }

    private void ClearLog()
    {
        LogTextBox.Clear();
    }

    private string? NormalizeOptional(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private bool TryReadOptionalInt(string? text, string label, out int? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!int.TryParse(text, out var parsed))
        {
            MessageBox.Show(this, $"{label} must be a number.", "Audio Extractor", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        value = parsed;
        return true;
    }

    private bool TryReadRequiredInt(string? text, string label, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text) || !int.TryParse(text, out value))
        {
            MessageBox.Show(this, $"{label} must be a number.", "Audio Extractor", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    private sealed class UiReporter : IExtractionReporter
    {
        private readonly MainWindow _window;

        public UiReporter(MainWindow window)
        {
            _window = window;
        }

        public void Info(string message)
        {
            _window.Dispatcher.Invoke(() => _window.LogTextBox.AppendText(message + Environment.NewLine));
        }

        public void Warn(string message)
        {
            Info(message);
        }

        public void Error(string message)
        {
            Info(message);
        }
    }
}
