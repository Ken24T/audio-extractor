#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_root"

cargo build --release -p extractor-cli -p extractor-gui

echo "Built release binaries in $repo_root/target/release"