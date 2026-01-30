<#
audio-extractor.ps1
Reliable audio extraction for Qwen3-TTS-style reference audio using ffmpeg.

DEFAULT:
  - WAV PCM 16-bit
  - Mono
  - 24 kHz
  - High-pass + Low-pass
  - Loudness normalisation
  - Output protection (no overwrite unless -Force)

Auto output naming:
  Adds Start/End/Duration to filename

Sanity checks:
  Validates time format
  Rejects End <= Start
  Rejects Duration <= 0
#>

param(
    [Parameter(Position = 0)]
    [string]$InFile,

    [string]$Output,
    [string]$Start,
    [string]$End,
    [string]$Duration,

    [int]$SampleRate,
    [ValidateSet(1,2)]
    [int]$Channels,

    [string]$FfmpegPath,

    [switch]$NoTTS,

    [int]$TtsSampleRate = 24000,
    [int]$TtsHighpassHz = 80,
    [int]$TtsLowpassHz  = 11000,
    [int]$TargetLUFS    = -16,

    [switch]$Force,
    [switch]$Help
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# -----------------------------
# Friendly CLI error
# -----------------------------
function Fail {
    param([string]$Message, [int]$Code = 2)
    Write-Host $Message -ForegroundColor Red
    exit $Code
}

# -----------------------------
# Help
# -----------------------------
function Show-Help {
@"
audio-extractor.ps1 - Extract audio using ffmpeg

DEFAULT: Qwen3-friendly WAV (mono, 24kHz, speech filtered, loudnorm)

USAGE:
  .\audio-extractor.ps1 <inputFile> [options]

OPTIONS:
  -Start <time>         HH:MM:SS | MM:SS | SS
  -End <time>           Requires -Start
  -Duration <time>      Requires -Start

  -Output <file>         Output filename (auto if omitted)
  -NoTTS                 Preserve original format
  -Force                 Overwrite output file
  -FfmpegPath <path>     Path to ffmpeg.exe

EXAMPLES:
  .\audio-extractor.ps1 Lockdown.mp4
  .\audio-extractor.ps1 Lockdown.mp4 -Start 00:01:00 -Duration 00:00:20
  .\audio-extractor.ps1 Lockdown.mp4 -Start 00:01:00 -End 00:01:20
"@
}

# -----------------------------
# Time parser
# -----------------------------
function Convert-ToSeconds {
    param([string]$TimeText)

    if ([string]::IsNullOrWhiteSpace($TimeText)) { return $null }

    $parts = $TimeText.Trim().Split(":")
    if ($parts.Count -gt 3) {
        Fail "Invalid time format: $TimeText (use SS, MM:SS, or HH:MM:SS)"
    }

    $nums = $parts | ForEach-Object {
        if ($_ -notmatch '^\d+$') { Fail "Invalid time format: $TimeText" }
        [int]$_
    }

    while ($nums.Count -lt 3) { $nums = ,0 + $nums }
    $h,$m,$s = $nums
    return ($h * 3600) + ($m * 60) + $s
}

# -----------------------------
# Filename helpers
# -----------------------------
function To-FileTimeToken {
    param([string]$TimeText)
    if (!$TimeText) { return "" }
    return ($TimeText.Trim() -replace ":", "-" -replace "\s+", "")
}

function Build-AutoOutputName {
    param($InputPath,$IsNoTTS,$Start,$End,$Duration)

    $base = [IO.Path]::GetFileNameWithoutExtension($InputPath)
    $dir  = [IO.Path]::GetDirectoryName($InputPath)
    if (!$dir) { $dir = "." }

    $mode = $IsNoTTS ? "_out" : "_tts"
    $tags = @()

    if ($Start)    { $tags += "s$(To-FileTimeToken $Start)" }
    if ($End)      { $tags += "e$(To-FileTimeToken $End)" }
    if ($Duration) { $tags += "d$(To-FileTimeToken $Duration)" }

    $tag = ($tags.Count -gt 0) ? "_" + ($tags -join "_") : ""
    return Join-Path $dir "$base$mode$tag.wav"
}

function Get-NonClobberPath {
    param($Path)
    if (!(Test-Path $Path)) { return $Path }

    $dir  = [IO.Path]::GetDirectoryName($Path)
    $base = [IO.Path]::GetFileNameWithoutExtension($Path)
    $ext  = [IO.Path]::GetExtension($Path)

    for ($i=1; $i -le 9999; $i++) {
        $p = Join-Path $dir ("{0}_{1:D3}{2}" -f $base,$i,$ext)
        if (!(Test-Path $p)) { return $p }
    }

    return Join-Path $dir ("{0}_{1}{2}" -f $base,(Get-Date -f yyyyMMdd_HHmmss),$ext)
}

# -----------------------------
# ffmpeg lookup
# -----------------------------
function Resolve-FfmpegPath {
    param($MaybePath)
    if ($MaybePath) {
        if (!(Test-Path $MaybePath)) { Fail "ffmpeg not found: $MaybePath" }
        return (Resolve-Path $MaybePath).Path
    }

    $cmd = Get-Command ffmpeg -ErrorAction SilentlyContinue
    if (!$cmd) { Fail "ffmpeg not in PATH. Install or use -FfmpegPath" }
    return $cmd.Source
}

# -----------------------------
# Help handling
# -----------------------------
$gnuHelp = @("--help","-h","/?","-?")
if ($Help -or ($InFile -and ($gnuHelp -contains $InFile))) {
    Show-Help
    exit 0
}

if (!$InFile) {
    Show-Help
    exit 1
}

# -----------------------------
# Validate input file
# -----------------------------
if (!(Test-Path $InFile)) {
    Fail "Input file not found: $InFile"
}

# -----------------------------
# Validate time args
# -----------------------------
if ($End -and $Duration) { Fail "Use either -End OR -Duration (not both)." }
if (($End -or $Duration) -and !$Start) { Fail "Start time required when using End/Duration." }

$startSec    = Convert-ToSeconds $Start
$endSec      = Convert-ToSeconds $End
$durationSec = Convert-ToSeconds $Duration

if ($durationSec -ne $null -and $durationSec -le 0) {
    Fail "Duration must be > 0"
}
if ($startSec -ne $null -and $endSec -ne $null -and $endSec -le $startSec) {
    Fail "End time ($End) must be AFTER Start time ($Start)."
}

# -----------------------------
# Output filename
# -----------------------------
if (!$Output) {
    $Output = Build-AutoOutputName $InFile $NoTTS $Start $End $Duration
}

if (!$Force) {
    $new = Get-NonClobberPath $Output
    if ($new -ne $Output) {
        Write-Host "Output exists -> $new" -ForegroundColor Yellow
        $Output = $new
    }
}

$ffmpeg = Resolve-FfmpegPath $FfmpegPath

# -----------------------------
# Run ffmpeg wrapper
# -----------------------------
function Run-Ffmpeg {
    param([string[]]$Args)
    Write-Host "Running ffmpeg:" -ForegroundColor Cyan
    Write-Host "`"$ffmpeg`" $($Args -join ' ')" -ForegroundColor DarkCyan
    & $ffmpeg @Args
    if ($LASTEXITCODE -ne 0) { Fail "ffmpeg failed ($LASTEXITCODE)" 10 }
}

# -----------------------------
# Build ffmpeg args
# -----------------------------
$baseArgs = @("-hide_banner","-loglevel","warning")
if ($Start) { $baseArgs += @("-ss",$Start) }
$baseArgs += @("-i",$InFile)
if ($Duration) { $baseArgs += @("-t",$Duration) }
if ($End)      { $baseArgs += @("-to",$End) }

$wavArgs = @("-vn","-c:a","pcm_s16le")

$outArgs = @()
if ($Force) { $outArgs += "-y" } else { $outArgs += "-n" }
$outArgs += $Output

# -----------------------------
# NoTTS mode
# -----------------------------
if ($NoTTS) {
    if ($SampleRate) { $wavArgs += @("-ar",$SampleRate) }
    if ($Channels)   { $wavArgs += @("-ac",$Channels) }

    Run-Ffmpeg ($baseArgs + $wavArgs + $outArgs)
    Write-Host "Done -> $Output" -ForegroundColor Green
    exit 0
}

# -----------------------------
# Qwen3 TTS default mode
# -----------------------------
$filter = "highpass=f=$TtsHighpassHz,lowpass=f=$TtsLowpassHz,aresample=$TtsSampleRate,loudnorm=I=$TargetLUFS:TP=-1.5:LRA=11"

$ttsArgs = $baseArgs + $wavArgs + @(
    "-af",$filter,
    "-ac","1",
    "-ar",$TtsSampleRate
) + $outArgs

Run-Ffmpeg $ttsArgs
Write-Host "Done -> $Output" -ForegroundColor Green
