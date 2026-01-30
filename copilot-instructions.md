# Audio Extractor – Copilot Instructions

## Project Overview

Windows console app (C# / .NET 8) that wraps **ffmpeg** to extract audio with Qwen3-friendly defaults. It replaces the legacy `audio-extractor.ps1` script while keeping feature parity.

## Repository Structure

- `/src/AudioExtractor` – Console application source
- `/README.md` – Usage and build instructions
- `audio-extractor.ps1` – Reference script (legacy)
- `TCTBP Agent.md`, `TCTBP.json` – Shipping workflow rules

## Build & Run

```powershell
dotnet restore
dotnet build -c Release

dotnet run --project .\src\AudioExtractor -- <inputFile> [options]
```

Publish single-file executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
```

## Dependencies

- `System.CommandLine` for CLI parsing
- **External:** `ffmpeg` must be installed and available on PATH (or specify `--ffmpeg-path`)

## Required CLI Behaviour (Parity with the script)

- `inputFile` is required
- Time formats: `SS`, `MM:SS`, `HH:MM:SS`
- `--end` and `--duration` are mutually exclusive
- `--end` or `--duration` requires `--start`
- Reject `end <= start` and `duration <= 0`
- Default output naming includes time tokens (`s`, `e`, `d`) and `_tts` or `_out`
- No overwrite unless `--force`; otherwise pick a non-clobber filename
- Output is WAV PCM 16-bit
- TTS defaults: mono, 24 kHz, highpass 80 Hz, lowpass 11 kHz, loudnorm target -16 LUFS
- `--no-tts` preserves format and allows `--sample-rate` / `--channels`

## Coding Standards

- Nullable enabled (`<Nullable>enable</Nullable>`)
- Guard clauses for argument validation
- Minimal dependencies; avoid adding packages without clear benefit
- Windows-first behaviour (PowerShell examples are fine)
- Use Australian English spelling in user-facing text

## Error Handling

- Friendly error messages to stderr
- Non-zero exit codes on failure
- Validate ffmpeg presence before execution

## Shipping Workflow

Follow the SHIP/TCTBP process in [TCTBP Agent.md](TCTBP Agent.md).
