# Audio Extractor - Copilot Instructions

## Project Overview

Windows-first audio extraction tool built on .NET 8. The repo contains a CLI, a shared core library, and a WPF GUI that all wrap ffmpeg for reliable audio extraction with Qwen3-friendly defaults.

## Repository Structure

- `/src/AudioExtractor` - CLI entry point and argument parsing
- `/src/AudioExtractor.Core` - Shared extraction logic, validation, and ffmpeg integration
- `/src/AudioExtractor.Gui` - WPF desktop GUI and persisted user settings
- `/tests/AudioExtractor.Tests` - Unit tests for CLI parsing and shared behaviour
- `/docs/UserGuide.md` - End-user documentation
- `/README.md` - Build, run, and publish overview
- `/audio-extractor.ps1` - Legacy PowerShell reference implementation
- `/TCTBP Agent.md`, `/TCTBP.json` - Shipping workflow rules

## Development Commands

```powershell
dotnet restore audio-extractor.sln
dotnet build audio-extractor.sln -c Release
dotnet test audio-extractor.sln --verbosity minimal

dotnet run --project .\src\AudioExtractor -- <inputFile> [options]
dotnet run --project .\src\AudioExtractor.Gui

dotnet publish .\src\AudioExtractor\AudioExtractor.csproj -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
dotnet publish .\src\AudioExtractor.Gui\AudioExtractor.Gui.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

The normal ship gate is the solution build plus tests. Publish commands are release-build paths and should be used only for packaging or deployment work.

## Key Dependencies

- `ffmpeg` on PATH, or an explicit `--ffmpeg-path`
- `ffprobe` is optional and enables input-duration guards
- `Extended.Wpf.Toolkit` for GUI controls

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

- Nullable is enabled; keep null handling explicit
- Prefer guard clauses for argument and settings validation
- Keep shared extraction behaviour in `AudioExtractor.Core` instead of duplicating logic between CLI and GUI
- Minimise dependencies; do not add packages without a clear need
- Keep Windows-first behaviour and PowerShell examples unless the change requires broader platform support
- Use Australian English spelling in user-facing text

## Versioning And Shipping

- The shipped version currently lives in `src/AudioExtractor/AudioExtractor.csproj`
- Keep the version in sync with the SHIP tag created for that release
- Follow the SHIP/TCTBP process in [TCTBP Agent.md](TCTBP Agent.md)

## Documentation Expectations

- Review `README.md` and `docs/UserGuide.md` for user-visible features, GUI interaction changes, settings changes, and packaging changes
- Internal-only changes may skip docs updates, but record a short reason during SHIP or handoff
- Prefer small, accurate documentation updates over broad rewrites
