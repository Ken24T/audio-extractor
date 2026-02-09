# Audio Extractor - User Guide

## Overview

Audio Extractor is a Windows CLI and GUI tool that extracts and processes audio from media files using ffmpeg. It's optimized for creating high-quality reference audio samples for Qwen3-TTS (text-to-speech) applications.

## Requirements

- Windows operating system
- ffmpeg installed and available on your PATH (or specify path with `--ffmpeg-path`)
- ffprobe (optional, for duration validation)

## Installation

1. Download `audio-extractor.exe` to a folder on your system
2. Ensure ffmpeg is installed:
   - Download from [ffmpeg.org](https://ffmpeg.org/download.html)
   - Add ffmpeg to your system PATH, or use `--ffmpeg-path` to specify location

### GUI Installation

To build a single-file GUI executable locally:

```powershell
dotnet publish .\src\AudioExtractor.Gui -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "E:\AudioExtractor-GUI"
```

Run the GUI:

```powershell
E:\AudioExtractor-GUI\AudioExtractor.Gui.exe
```

## Quick Start

Extract audio from a video file with TTS-optimized settings:

```powershell
audio-extractor.exe video.mp4
```

This creates a WAV file with:

- Mono audio
- 24 kHz sample rate
- High-pass and low-pass filtering (80 Hz - 11 kHz)
- Loudness normalization (-16 LUFS)

### GUI Quick Start

Launch the GUI and select an input file, then click **Run**. Use **Edit > Settings** to point to `ffmpeg.exe` if it's not on PATH.

## Basic Usage

```text
audio-extractor <inputFile> [options]
```

### Common Examples


**Extract a specific time range:**

```powershell
audio-extractor.exe podcast.mp4 --start 00:05:30 --duration 00:00:20

Creates a 20-second clip starting at 5 minutes 30 seconds.

**Extract between two timestamps:**

**Extract between two timestamps:**

Extracts audio from 15:00 to 15:45 (45 seconds).

**Custom output filename:**

Extracts audio from 15:00 to 15:45 (45 seconds).

**Custom output filename:**

```powershell
audio-extractor.exe song.mp3 --output my-sample.wav
```

Skips TTS-specific processing and keeps original audio characteristics.

**Open output file automatically:**

audio-extractor.exe video.mp4 --no-tts

Extracts audio and opens it in your default audio player.

## Time Format

All time values support three formats:

- `SS` - Seconds only (e.g., `90`)
- `MM:SS` - Minutes and seconds (e.g., `01:30`)
- `HH:MM:SS` - Hours, minutes, and seconds (e.g., `00:01:30`)

Fractional seconds are supported:

- `3.25` - 3.25 seconds
- `01:30.5` - 1 minute 30.5 seconds

## Command-Line Options

### Time Control

| Option              | Description                                              | Example                |
|---------------------|----------------------------------------------------------|------------------------|
| `--start <time>`    | Start extraction at specified time                       | `--start 00:02:30`     |
| `--end <time>`      | End extraction at specified time (requires `--start`)    | `--end 00:03:00`       |
| `--duration <time>` | Extract for specified duration (requires `--start`)      | `--duration 00:00:20`  |

**Note:** Use either `--end` OR `--duration`, not both.

### Output Control

| Option            | Description                                              | Example               |
|-------------------|----------------------------------------------------------|-----------------------|
| `--output <file>` | Specify output filename                                  | `--output sample.wav` |
| `--force`         | Overwrite existing output file                           | `--force`             |
| `--autoplay`      | Open output file in default app after extraction         | `--autoplay`          |

### Audio Processing

| Option                | Description                                             | Default  |
|-----------------------|---------------------------------------------------------|----------|
| `--no-tts`            | Disable TTS optimization (preserve original format)     | Disabled |
| `--sample-rate <int>` | Sample rate (only with `--no-tts`)                      | -        |
| `--channels <1\|2>`   | Audio channels (only with `--no-tts`)                   | -        |

### TTS Optimization (default mode)

| Option                    | Description             | Default |
|---------------------------|-------------------------|---------|  
| `--tts-sample-rate <int>` | Target sample rate      | 24000   |
| `--tts-highpass-hz <int>` | High-pass filter cutoff | 80      |
| `--tts-lowpass-hz <int>`  | Low-pass filter cutoff  | 11000   |
| `--target-lufs <int>`     | Target loudness level   | -16     |

### System

| Option                 | Description                        | Example                               |
|------------------------|------------------------------------|------------------------------------- -|
| `--ffmpeg-path <path>` | Path to ffmpeg.exe                 | `--ffmpeg-path C:\\Tools\\ffmpeg.exe` |
| `--verbose`            | Show ffmpeg command being executed | `--verbose`                           |


If you don't specify `--output`, the tool generates a descriptive filename:

**Pattern:** `<basename>_<mode>_<time-tags>.wav`

- `<mode>` is either `_tts` or `_out`
- Time tags include start (`s`), end (`e`), and duration (`d`)

**Examples:**

- `podcast.mp4` → `podcast_tts.wav`
- `podcast.mp4 --start 00:05:00` → `podcast_tts_s00-05-00.wav`
- `podcast.mp4 --start 00:05:00 --duration 00:00:30` → `podcast_tts_s00-05-00_d00-00-30.wav`

## Use Cases


### Creating TTS Reference Audio

Extract clean speech samples for voice cloning or TTS training:

```powershell
audio-extractor.exe interview.mp4 --start 00:12:15 --duration 00:00:10
```

The default settings optimize for speech:

- Removes low-frequency rumble (high-pass at 80 Hz)
- Removes high-frequency noise (low-pass at 11 kHz)
- Normalizes loudness for consistent volume

### Extracting Music Segments

Preserve full audio quality for music:

```powershell
audio-extractor.exe concert.mkv --start 01:23:45 --end 01:27:30 --no-tts --sample-rate 48000 --channels 2
```

### Quick Audio Preview

Extract and immediately play a segment:

```powershell
audio-extractor.exe video.mp4 --start 00:10:00 --duration 00:00:05 --autoplay
```

### Batch Processing

Create a PowerShell script to extract multiple segments:

```powershell
$segments = @(
    @{Start="00:05:00"; Duration="00:00:15"; Output="sample1.wav"},
    @{Start="00:12:30"; Duration="00:00:20"; Output="sample2.wav"},
    @{Start="00:18:45"; Duration="00:00:10"; Output="sample3.wav"}

)

foreach ($seg in $segments) {
    audio-extractor.exe input.mp4 --start $seg.Start --duration $seg.Duration --output $seg.Output
}
```

## Duration Guards

If ffprobe is available, the tool validates that:

- Start time doesn't exceed input duration
- End time doesn't exceed input duration
- Start + duration doesn't exceed input duration

This prevents creating empty or truncated output files.

## File Protection

By default, the tool won't overwrite existing files. If the output file exists, it automatically adds a numeric suffix:

- `sample.wav` exists → creates `sample_001.wav`
- `sample_001.wav` exists → creates `sample_002.wav`

Use `--force` to override this behavior and overwrite existing files.

## Tips

1. **Always specify start time for extractions:** Without `--start`, the entire file is processed, which may take longer.

2. **Use `--verbose` when troubleshooting:** See the exact ffmpeg command being run.

3. **Test with short durations first:** Extract 5-10 seconds to verify settings before processing longer segments.

4. **Check audio levels:** If output is too quiet or loud, adjust `--target-lufs` (lower = louder, e.g., `-18` is louder than `-16`).

5. **For very quiet source audio:** Try `--target-lufs -12` for more aggressive normalization.

6. **Keep reference samples short:** For TTS training, 5-15 second clips are typically ideal.

## Troubleshooting

### "ffmpeg not in PATH"

Install ffmpeg and add it to your system PATH, or use `--ffmpeg-path`:

```powershell
audio-extractor.exe video.mp4 --ffmpeg-path "C:\Tools\ffmpeg\bin\ffmpeg.exe"
```

### "Start time exceeds input duration"

Check your input file duration with ffprobe:

```powershell
ffprobe -i video.mp4
```

Ensure your `--start`, `--end`, or `--duration` values are valid.

### Empty or silent output

1. Check source audio: `ffprobe -i input.mp4`
2. Try `--verbose` to see ffmpeg output
3. If using `--no-tts`, ensure you're not filtering too aggressively

### Output file size is large

WAV files are uncompressed. A 10-second mono 24kHz WAV is approximately 480 KB. This is expected for lossless audio.

## Getting Help


View built-in help:

```powershell
audio-extractor.exe --help

```

## Examples by Scenario

### Voice Acting Sample

```powershell
audio-extractor.exe audiobook.m4a --start 00:15:30 --duration 00:00:12
```

### Podcast Clip

```powershell
audio-extractor.exe podcast.mp3 --start 00:23:15 --end 00:23:45 --output funny-moment.wav
```

### Meeting Recording Snippet

```powershell
audio-extractor.exe meeting.mp4 --start 01:12:00 --duration 00:01:30 --autoplay
```

### High-Quality Music Export

```powershell
audio-extractor.exe album.flac --start 00:02:15 --end 00:06:30 --no-tts --sample-rate 96000 --channels 2 --output track.wav
```

### Multiple Extractions with Consistent Settings

```powershell
audio-extractor.exe source.mp4 --start 00:05:00 --duration 00:00:10 --tts-sample-rate 22050 --target-lufs -18
audio-extractor.exe source.mp4 --start 00:12:30 --duration 00:00:10 --tts-sample-rate 22050 --target-lufs -18
audio-extractor.exe source.mp4 --start 00:18:45 --duration 00:00:10 --tts-sample-rate 22050 --target-lufs -18
```
