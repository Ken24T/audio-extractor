# Audio Extractor

Windows CLI and GUI app for reliable audio extraction using ffmpeg.

## Features

- WAV PCM 16-bit output
- Mono, 24 kHz defaults (Qwen3-friendly)
- High-pass + low-pass filtering
- Loudness normalisation
- Auto output naming with time tokens
- Output protection (no overwrite unless `--force`)
- GUI adapts to your Windows accent colour (with static fallback)

## Requirements

- Windows
- .NET 8 SDK
- ffmpeg available on PATH (or provide `--ffmpeg-path`)

## Build

```powershell
cd .\src\AudioExtractor

dotnet restore

dotnet build -c Release
```

## Run (from source)

```powershell
dotnet run --project .\src\AudioExtractor -- <inputFile> [options]
```

## Run GUI (from source)

```powershell
dotnet run --project .\src\AudioExtractor.Gui
```

## Usage

```text
audio-extractor <inputFile> [options]

Options:
  --start <time>         HH:MM:SS | MM:SS | SS
  --end <time>           Requires --start
  --duration <time>      Requires --start

  --output <file>        Output filename (auto if omitted)
  --no-tts               Preserve original format
  --force                Overwrite output file
  --ffmpeg-path <path>   Path to ffmpeg.exe

  --sample-rate <int>    Only in --no-tts mode
  --channels <1|2>       Only in --no-tts mode

  --tts-sample-rate <int>  Default 24000
  --tts-highpass-hz <int>  Default 80
  --tts-lowpass-hz <int>   Default 11000
  --target-lufs <int>      Default -16
```

## Examples

```powershell
# Full file
.\audio-extractor.exe .\Lockdown.mp4

# Start + duration
.\audio-extractor.exe .\Lockdown.mp4 --start 00:01:00 --duration 00:00:20

# Start + end
.\audio-extractor.exe .\Lockdown.mp4 --start 00:01:00 --end 00:01:20
```

## Notes

- The original PowerShell script remains at the repo root for reference.
- Publish a single-file executable with:

```powershell
cd .\src\AudioExtractor

dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
```

- Publish the GUI as a single-file executable with:

```powershell
dotnet publish .\src\AudioExtractor.Gui -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "E:\AudioExtractor-GUI"
```
