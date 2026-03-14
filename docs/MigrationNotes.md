# Migration Notes

## Status

Audio Extractor is now Rust-first.

The active implementation is the Cargo workspace at the repo root. The Rust CLI and Rust GUI are the supported paths for current development and shipping work.

## Preserved Behaviour

The Rust implementation preserves the following behaviour from the legacy implementation:

- time parsing with `SS`, `MM:SS`, and `HH:MM:SS`
- fractional seconds in the seconds field
- TTS defaults of mono, 24 kHz, 80 Hz high-pass, 11 kHz low-pass, and `-16` LUFS
- canonical `_tts` and `_out` output naming
- no-clobber output naming unless overwrite is explicitly requested
- WAV PCM 16-bit output
- optional `ffprobe` duration guards

## Current Architecture

- `extractor-domain` owns parsing, validation, naming, and extraction plan construction
- `extractor-ffmpeg` owns binary discovery, probing, execution, and autoplay
- `extractor-cli` is the cross-platform command-line entry point
- `extractor-gui` is the `egui` desktop front end
- `extractor-platform` owns configuration paths and persisted settings

## Legacy Status

The following legacy components remain in the repository as archived references during the transition period:

- `.NET` CLI in `src/AudioExtractor`
- shared `.NET` extraction logic in `src/AudioExtractor.Core`
- WPF GUI in `src/AudioExtractor.Gui`
- PowerShell reference script at the repo root

They are no longer the primary implementation or default shipping path.

## Shipping Policy

Rust work ships against Cargo-based validation.

Current primary checks:

- `cargo check --workspace`
- `cargo test --workspace`

When slice-based sync shipping is requested, a completed numbered slice is a valid checkpoint and the current branch should be pushed to `origin` after a successful ship.