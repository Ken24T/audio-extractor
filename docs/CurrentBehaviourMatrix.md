# Current Behaviour Matrix

## Purpose

This document captures the current observable behaviour of the application across the CLI, shared extraction core, GUI, and legacy PowerShell script.

It is the output of Slice 0.1 and Slice 0.2.

## Scope

Sources reviewed:

- `src/AudioExtractor/Program.cs`
- `src/AudioExtractor.Core/AudioExtractionUtils.cs`
- `src/AudioExtractor.Core/ExtractionModels.cs`
- `src/AudioExtractor.Gui/MainWindow.xaml.cs`
- `audio-extractor.ps1`
- `tests/AudioExtractor.Tests/ProgramTests.cs`
- `README.md`
- `docs/UserGuide.md`

## Contract Summary

### Runtime dependencies

| Capability | Current behaviour |
|---|---|
| `ffmpeg` | Required at runtime, either on PATH or by explicit path |
| `ffprobe` | Optional; enables duration guard checks when available |
| CLI host | .NET 8 |
| GUI host | .NET 8 WPF on Windows |

### Entry points

| Entry point | Status | Notes |
|---|---|---|
| CLI | Active | Manual argument parsing in `Program.cs` |
| Shared service | Active | Behaviour authority for validation, naming, and ffmpeg invocation |
| WPF GUI | Active | Thin UI shell over the shared service, with some divergent defaults |
| PowerShell script | Legacy reference | Similar but not identical behaviour |

## CLI Option Matrix

### Help and invocation

| Input | Current behaviour | Exit code |
|---|---|---|
| No arguments | Show help | `1` |
| `--help` | Show help | `0` |
| `-h` | Show help | `0` |
| `/?` | Show help | `0` |
| `-?` | Show help | `0` |

### Supported flags and aliases

| Canonical flag | Alias support | Current behaviour |
|---|---|---|
| `--output <file>` | `-o`, `-Output` | Sets output path |
| `--start <time>` | `-Start` | Sets start time |
| `--end <time>` | `-End` | Sets end time |
| `--duration <time>` | `-Duration` | Sets duration |
| `--sample-rate <int>` | `-SampleRate` | Only intended for `--no-tts` mode |
| `--channels <int>` | `-Channels` | Only intended for `--no-tts` mode |
| `--ffmpeg-path <path>` | `-FfmpegPath` | Explicit ffmpeg binary path |
| `--no-tts` | `-NoTTS` | Disables TTS processing profile |
| `--force` | `-Force` | Allows overwrite |
| `--autoplay` | `-Autoplay` | Opens output in default app after success |
| `--verbose` | `-Verbose` | Prints ffmpeg invocation details |
| `--tts-sample-rate <int>` | None | TTS sample rate override |
| `--tts-highpass-hz <int>` | None | TTS high-pass override |
| `--tts-lowpass-hz <int>` | None | TTS low-pass override |
| `--target-lufs <int>` | None | TTS loudness target override |

### Defaults

| Setting | Current default |
|---|---|
| TTS sample rate | `24000` |
| TTS high-pass | `80` |
| TTS low-pass | `11000` |
| Target LUFS | `-16` |
| TTS mode | Enabled by default |
| Force overwrite | Disabled by default |
| Autoplay | Disabled by default in CLI |
| Verbose | Disabled by default |

## Validation Matrix

### Input validation

| Rule | Current behaviour | Exit code |
|---|---|---|
| Missing input file | CLI shows help before calling service | `1` |
| Non-existent input path | Error: `Input file not found: <path>` | `2` |
| Invalid channel count | Error: `Channels must be 1 or 2.` | `2` |
| `end` and `duration` both set | Error: `Use either --end OR --duration (not both).` | `2` |
| `end` or `duration` without `start` | Error: `Start time required when using --end/--duration.` | `2` |
| `duration <= 0` | Error: `Duration must be > 0` | `2` |
| `end <= start` | Error includes both values | `2` |

### Time format parsing

| Format | Current .NET behaviour |
|---|---|
| `SS` | Supported |
| `MM:SS` | Supported |
| `HH:MM:SS` | Supported |
| Fractional seconds in seconds field | Supported, for example `00:00:03.25` |
| More than 3 colon segments | Invalid |
| Non-numeric parts outside fractional seconds | Invalid |

### Duration guards with ffprobe

| Condition | Current behaviour |
|---|---|
| `ffprobe` available and start exceeds media duration | Error |
| `ffprobe` available and end exceeds media duration | Error |
| `ffprobe` available and start + duration exceeds media duration | Error |
| `ffprobe` missing or probe fails | Duration guards are skipped silently |

## Output Naming Matrix

### Shared service behaviour

If `Output` is omitted:

- mode suffix is `_tts` when TTS mode is active
- mode suffix is `_out` when `NoTts` is active
- time tags are appended as available:
  - `s<time>` for start
  - `e<time>` for end
  - `d<time>` for duration
- extension is always `.wav`

Examples:

| Scenario | Shared service output pattern |
|---|---|
| TTS, no timing | `<basename>_tts.wav` |
| No TTS, no timing | `<basename>_out.wav` |
| TTS with start | `<basename>_tts_sHH-MM-SS.wav` |
| TTS with start and duration | `<basename>_tts_sHH-MM-SS_dHH-MM-SS.wav` |

### No-clobber behaviour

When overwrite is not forced:

- if the target path does not exist, use it as-is
- if it exists, generate `_001`, `_002`, and so on
- after exhausting numeric attempts, fall back to a timestamp suffix

## ffmpeg Execution Matrix

### Common behaviour

| Behaviour | Current implementation |
|---|---|
| Input media | Passed via `-i <input>` |
| Audio extraction | Always includes `-vn` |
| Output codec | Always `pcm_s16le` |
| Output extension | Always `.wav` |
| Overwrite mode | `-y` when forced, `-n` otherwise |
| Base logging | `-hide_banner -loglevel warning` |

### Time selection behaviour

| Input combination | Current .NET ffmpeg behaviour |
|---|---|
| Start only | Uses `-ss <start>` |
| Start + duration | Uses `-ss <start>` and `-t <duration>` |
| Start + end | Uses `-ss <start>` and computed `-t <end - start>` |

### TTS mode behaviour

Current filter chain:

`highpass -> lowpass -> aresample -> loudnorm`

Current additional args:

- `-af <filter>`
- `-ac 1`
- `-ar <tts_sample_rate>`

### No-TTS mode behaviour

- no TTS filter chain
- optional `-ar <sample_rate>` if provided
- optional `-ac <channels>` if provided

## Logging And Messaging Matrix

### CLI

| Event | Current behaviour |
|---|---|
| Validation/runtime error | Red stderr output |
| Non-fatal warning | Yellow stderr output |
| Informational progress | stdout |
| Verbose mode | Prints `Running ffmpeg:` and the rendered command |
| Existing output path | Prints `Output exists -> <newPath>` |
| Success | Prints `Done -> <output>` |

### GUI

| Event | Current behaviour |
|---|---|
| Missing input | Warning message box |
| Invalid numeric field | Warning message box |
| Extraction failure | Error message box and status update |
| Informational progress | Appended to log textbox |
| Success | Appends `Extraction complete.` to log and updates status |

## GUI Behaviour Matrix

### Defaults and initial UI state

| GUI element | Current behaviour |
|---|---|
| TTS numeric fields | Pre-populated from extraction defaults |
| Autoplay checkbox | Checked by default |
| Output path on input selection | Defaults to `<basename>.wav` |
| ffmpeg path | Loaded from settings |
| Window placement | Restored from settings if valid |

### GUI-to-service mapping

The GUI ultimately calls the same extraction service as the CLI, but not all defaults originate from the shared service. The main divergence is output naming.

## Legacy PowerShell Matrix

### Preserved concepts

- same overall product scope
- similar flag names
- same `_tts` and `_out` naming concept
- same no-clobber pattern
- same general TTS defaults

### Behaviour differences from current .NET implementation

| Area | PowerShell | Current .NET |
|---|---|---|
| Fractional seconds | Not supported | Supported |
| End handling | Uses `-to <end>` with `-ss` | Calculates duration and uses `-t <end - start>` |
| PATH lookup | `Get-Command` | Windows `where` command |
| ffmpeg failure exit code | `10` | surfaces as generic failure with exit code `2` |
| CLI help coverage | Simpler | Includes more options |

## Exit Code Matrix

| Scenario | Current CLI exit code |
|---|---|
| Explicit help | `0` |
| No arguments | `1` |
| Missing input file in service path | `1` |
| Validation failure | `2` |
| Unknown option | `2` |
| Invalid integer option | `2` |
| ffmpeg runtime failure | `2` |
| Success | `0` |

## Behavioural Constraints For The Rust Port

The following are currently stable enough to preserve unless explicitly changed:

- time parsing rules, including fractional seconds
- TTS defaults
- no-clobber naming
- WAV PCM 16-bit output
- optional ffprobe duration guards
- explicit verbose ffmpeg command output

The following are currently inconsistent and should be resolved rather than copied blindly:

- GUI default output naming
- CLI help and docs coverage for all supported flags
- Windows-only process and PATH discovery assumptions
- exact autoplay expectations across platforms
