# Audio Extractor - User Guide

## Overview

Audio Extractor is a Rust-based CLI and desktop GUI that extracts and processes audio with `ffmpeg` on Windows and Linux. The default profile is tuned for TTS-oriented output, while preserve-input mode keeps the source characteristics closer to the original audio.

## Requirements

- `ffmpeg` installed and available on `PATH`, or configured explicitly
- `ffprobe` is optional and enables input-duration validation
- For local builds: Rust and Cargo

## Installation

### Use Prebuilt Binaries

If you already have a packaged CLI or GUI build, place it somewhere convenient and ensure `ffmpeg` is available either on `PATH` or by explicit configuration.

### Build From Source

Build the release binaries for your current platform:

```bash
cargo build --release -p extractor-cli -p extractor-gui
```

The resulting binaries are in `target/release/`.

## Quick Start

### CLI

Extract audio from a media file with the default TTS profile:

```bash
audio-extractor video.mp4
```

This produces a WAV file with:

- mono output
- 24 kHz sample rate
- 80 Hz high-pass filtering
- 11 kHz low-pass filtering
- `-16` LUFS loudness target

### GUI

Launch the GUI, select an input file, review or change the output path, then click `Run`.

If `ffmpeg` is not on `PATH`, enter the path to the `ffmpeg` binary in the GUI settings field.

## Time Format

All time values support:

- `SS`
- `MM:SS`
- `HH:MM:SS`

Fractional seconds are supported in the seconds field:

- `3.25`
- `01:30.5`
- `00:00:10.123`

## CLI Usage

```text
audio-extractor <inputFile> [options]
```

### Time Control

| Option | Description | Example |
|---|---|---|
| `--start <time>` | Start extraction at the specified time | `--start 00:02:30` |
| `--end <time>` | End extraction at the specified time, requires `--start` | `--end 00:03:00` |
| `--duration <time>` | Extract for a specific duration, requires `--start` | `--duration 00:00:20` |

Rules:

- use either `--end` or `--duration`, not both
- `--end` and `--duration` require `--start`
- `end` must be after `start`
- `duration` must be greater than zero

### Output Control

| Option | Description |
|---|---|
| `--output <file>` | Write to a specific output path |
| `--force` | Overwrite an existing output path |
| `--autoplay` | Open the output file in the default app after success |
| `--verbose` | Print the rendered `ffmpeg` command |

### Audio Processing

| Option | Description | Default |
|---|---|---|
| `--no-tts` | Disable the TTS processing profile | Off |
| `--sample-rate <int>` | Sample rate override in preserve-input mode | Unset |
| `--channels <1|2>` | Channel override in preserve-input mode | Unset |

### TTS Defaults

| Option | Description | Default |
|---|---|---|
| `--tts-sample-rate <int>` | Target sample rate | `24000` |
| `--tts-highpass-hz <int>` | High-pass cutoff | `80` |
| `--tts-lowpass-hz <int>` | Low-pass cutoff | `11000` |
| `--target-lufs <int>` | Loudness target | `-16` |

### System

| Option | Description |
|---|---|
| `--ffmpeg-path <path>` | Explicit path to the `ffmpeg` binary |

Legacy PowerShell-style aliases such as `-Output`, `-Start`, `-NoTTS`, `-Force`, and `-Verbose` are still accepted.

## Output Naming

If you do not specify `--output`, Audio Extractor generates a canonical WAV filename based on the input name, processing mode, and time fields.

Pattern:

```text
<basename>_<mode>_<time-tags>.wav
```

Where:

- `<mode>` is `_tts` for the default profile
- `<mode>` is `_out` for preserve-input mode
- `s`, `e`, and `d` time tags are appended for start, end, and duration

Examples:

- `podcast.mp4` -> `podcast_tts.wav`
- `podcast.mp4 --start 00:05:00` -> `podcast_tts_s00-05-00.wav`
- `podcast.mp4 --start 00:05:00 --duration 00:00:30` -> `podcast_tts_s00-05-00_d00-00-30.wav`

## File Protection

By default, existing output files are not overwritten.

If the target filename already exists, Audio Extractor creates a numbered alternative such as:

- `sample.wav`
- `sample_001.wav`
- `sample_002.wav`

Use `--force` to overwrite the original target path.

## Duration Guards

If `ffprobe` is available, Audio Extractor validates that:

- the start time does not exceed the input duration
- the end time does not exceed the input duration
- `start + duration` does not exceed the input duration

If `ffprobe` is not available, extraction still runs, but those duration guards are skipped.

## GUI Workflow

The Rust desktop GUI provides:

- input and output file selection
- free-form time fields for start, end, and duration
- preserve-input and TTS controls
- a settings field for `ffmpeg` path persistence
- log output and status feedback during extraction

The GUI uses the same Rust domain and runtime crates as the CLI. Behavioural rules live in the shared core, not only in the UI.

## Common Examples

Extract a 20-second speech clip:

```bash
audio-extractor podcast.mp4 --start 00:05:30 --duration 00:00:20
```

Extract between two timestamps:

```bash
audio-extractor recording.mp4 --start 00:15:00 --end 00:15:45
```

Preserve full audio characteristics for music:

```bash
audio-extractor concert.mkv --start 01:23:45 --end 01:27:30 --no-tts --sample-rate 48000 --channels 2
```

Extract and open immediately after success:

```bash
audio-extractor video.mp4 --start 00:10:00 --duration 00:00:05 --autoplay
```

## Troubleshooting

### `ffmpeg not in PATH`

Install `ffmpeg` and ensure it is on `PATH`, or provide the binary path explicitly.

CLI example:

```bash
audio-extractor video.mp4 --ffmpeg-path /path/to/ffmpeg
```

### `Start time exceeds input duration`

Check the input duration with `ffprobe` and verify your `--start`, `--end`, and `--duration` values.

### Output is empty or silent

1. Verify the source file actually contains audio.
2. Run with `--verbose` to inspect the rendered `ffmpeg` command.
3. If using preserve-input mode, verify the selected sample-rate and channel overrides are appropriate.

### Output file is large

The tool writes uncompressed WAV output. Larger output files are expected.

## Help

View CLI help:

```bash
audio-extractor --help
```
```

### Multiple Extractions with Consistent Settings

```powershell
audio-extractor.exe source.mp4 --start 00:05:00 --duration 00:00:10 --tts-sample-rate 22050 --target-lufs -18
audio-extractor.exe source.mp4 --start 00:12:30 --duration 00:00:10 --tts-sample-rate 22050 --target-lufs -18
audio-extractor.exe source.mp4 --start 00:18:45 --duration 00:00:10 --tts-sample-rate 22050 --target-lufs -18
```
