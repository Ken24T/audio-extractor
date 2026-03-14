$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

cargo build --release -p extractor-cli -p extractor-gui

Write-Host "Built release binaries in $repoRoot\target\release"