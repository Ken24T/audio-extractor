# Audio Extractor

Cross-platform Rust CLI and desktop GUI for reliable audio extraction with `ffmpeg`.

## Current Status

The Rust workspace at the repo root is now the primary implementation for Windows and Linux.

The older `.NET` CLI, shared core, WPF GUI, and PowerShell script remain in the repository as archived behavioural references during the migration, but they are no longer the primary build or shipping path.

## Features

- WAV PCM 16-bit output
- Default TTS-oriented processing: mono, 24 kHz, 80 Hz high-pass, 11 kHz low-pass, `-16` LUFS target
- Preserve-input mode with optional sample-rate and channel overrides
- Time parsing for `SS`, `MM:SS`, and `HH:MM:SS`, including fractional seconds in the seconds field
- Canonical auto output naming with `_tts` and `_out` suffixes plus time tokens
- No-clobber output naming unless `--force` is used
- Optional `ffprobe` duration guards
- Cross-platform desktop GUI built with `egui` and `eframe`

## Requirements

- Rust toolchain with Cargo
- `ffmpeg` available on `PATH`, or an explicit binary path supplied in the CLI or GUI settings
- `ffprobe` is optional and enables input-duration validation

## Repository Layout

- `Cargo.toml` - Rust workspace root
- `crates/extractor-domain` - shared parsing, validation, naming, and plan building
- `crates/extractor-ffmpeg` - runtime integration, binary discovery, probing, execution, autoplay
- `crates/extractor-cli` - cross-platform CLI
- `crates/extractor-gui` - `egui` desktop GUI
- `crates/extractor-platform` - config-path and settings persistence helpers
- `docs/UserGuide.md` - end-user guide
- `docs/MigrationNotes.md` - migration summary and legacy status

## Development Commands

```bash
cargo check --workspace
cargo test --workspace
```

Run the CLI from source:

```bash
cargo run -p extractor-cli -- <inputFile> [options]
```

Run the GUI from source:

```bash
cargo run -p extractor-gui
```

Build release binaries for the current platform:

```bash
cargo build --release -p extractor-cli -p extractor-gui
```

Helper scripts are provided in `scripts/` for repeatable release builds.

## CLI Usage

```text
audio-extractor <inputFile> [options]

Options:
  --start <time>           HH:MM:SS | MM:SS | SS
  --end <time>             Requires --start
  --duration <time>        Requires --start
  --output <file>          Output filename (auto if omitted)
  --no-tts                 Preserve original format
  --force                  Overwrite output file
  --autoplay               Open output in the default app after success
  --verbose                Print the rendered ffmpeg command
  --ffmpeg-path <path>     Explicit path to ffmpeg
  --sample-rate <int>      Only in --no-tts mode
  --channels <1|2>         Only in --no-tts mode
  --tts-sample-rate <int>  Default 24000
  --tts-highpass-hz <int>  Default 80
  --tts-lowpass-hz <int>   Default 11000
  --target-lufs <int>      Default -16
```

PowerShell-style aliases from the legacy CLI are still accepted, for example `-Output`, `-Start`, `-NoTTS`, and `-Verbose`.

## Examples

```bash
# Full file with default TTS profile
cargo run -p extractor-cli -- Lockdown.mp4

# Start + duration
cargo run -p extractor-cli -- Lockdown.mp4 --start 00:01:00 --duration 00:00:20

# Preserve-input mode with stereo output
cargo run -p extractor-cli -- concert.mkv --start 01:23:45 --end 01:27:30 --no-tts --sample-rate 48000 --channels 2
```

## Packaging

Use the release build helpers for repeatable current-platform builds:

- `scripts/build-rust-artifacts.sh`
- `scripts/build-rust-artifacts.ps1`

These produce release binaries for the CLI and GUI on the platform where they are run.

## Migration Notes

The repo now ships and validates Rust slice work with Cargo commands. See `docs/MigrationNotes.md` for the current migration summary, behavioural preservation notes, and the archived status of the legacy implementation.
