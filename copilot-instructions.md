# Audio Extractor - Copilot Instructions

## Project Overview

Rust-first cross-platform audio extraction tool for Windows and Linux. The repo now centres on a Cargo workspace with shared domain logic, ffmpeg integration, a CLI, and an `egui` desktop GUI.

## Repository Structure

- `/Cargo.toml` - Rust workspace root
- `/crates/extractor-domain` - Shared parsing, validation, output naming, and extraction-plan logic
- `/crates/extractor-ffmpeg` - Binary discovery, probing, no-clobbering, execution, and autoplay integration
- `/crates/extractor-cli` - Cross-platform CLI entry point
- `/crates/extractor-gui` - Cross-platform `egui` desktop GUI
- `/crates/extractor-platform` - Config path and settings persistence helpers
- `/crates/extractor-test-support` - Shared Rust test fixtures
- `/docs/UserGuide.md` - End-user documentation
- `/docs/MigrationNotes.md` - Migration and legacy-status notes
- `/src/AudioExtractor*`, `/tests/AudioExtractor.Tests`, `/audio-extractor.ps1` - Archived legacy implementation references
- `/TCTBP Agent.md`, `/TCTBP.json` - Shipping workflow rules

## Development Commands

```bash
cargo check --workspace
cargo test --workspace

cargo run -p extractor-cli -- <inputFile> [options]
cargo run -p extractor-gui

cargo build --release -p extractor-cli -p extractor-gui
```

The normal ship gate for active work is the Rust workspace check plus relevant Rust tests.

## Key Dependencies

- `ffmpeg` on PATH, or an explicit `--ffmpeg-path`
- `ffprobe` is optional and enables input-duration guards
- `clap` for the CLI
- `egui` and `eframe` for the GUI
- `serde`, `toml`, and `directories` for persistence and configuration

## Product Behaviour

CLI and GUI should stay aligned on the same extraction rules:

- `inputFile` is required for CLI execution
- Supported time formats are `SS`, `MM:SS`, and `HH:MM:SS`
- `--end` and `--duration` are mutually exclusive
- `--end` or `--duration` requires `--start`
- Reject `end <= start` and `duration <= 0`
- Default output naming includes time tokens and `_tts` or `_out`
- Do not overwrite existing files unless `--force`; otherwise choose a non-clobber filename
- Default output is WAV PCM 16-bit
- TTS defaults are mono, 24 kHz, high-pass 80 Hz, low-pass 11 kHz, loudnorm target -16 LUFS
- `--no-tts` preserves original format and allows `--sample-rate` and `--channels`
- Validate ffmpeg availability before attempting extraction

## Implementation Guidance

- Keep shared extraction behaviour in `extractor-domain` and `extractor-ffmpeg` instead of duplicating it between CLI and GUI
- Keep the GUI thin over the shared Rust core
- Prefer guard clauses and structured errors for validation
- Preserve agreed behaviour from the migration docs before introducing product changes
- Minimise dependencies; do not add packages without a clear need
- Use Australian English spelling in user-facing text

## Versioning And Shipping

- The active shipped version now lives in `/Cargo.toml` under `workspace.package.version`
- Keep the version in sync with the SHIP tag created for full release shipments
- Follow the SHIP/TCTBP process in [TCTBP Agent.md](TCTBP Agent.md)
- Completed numbered slices remain valid ship checkpoints when the user asks for per-slice sync

## Documentation Expectations

- Review `README.md` and `docs/UserGuide.md` for user-visible features, GUI interaction changes, settings changes, packaging changes, and support-platform changes
- Keep `docs/MigrationNotes.md` aligned with major migration or legacy-retirement decisions
- Internal-only changes may skip docs updates, but record a short reason during SHIP or handoff
- Prefer small, accurate documentation updates over broad rewrites
