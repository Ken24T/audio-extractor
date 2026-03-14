# Rust Slice Status

## Purpose

This document tracks the planning and execution status of the Rust refactor slices.

## Status Legend

- `done`: slice artefact completed and reviewed at the planning level
- `next`: highest-priority slice to execute next
- `later`: planned but not yet started

## Current Status

| Slice | Title | Status | Output |
|---|---|---|---|
| 0.1 | CLI Contract Inventory | done | `docs/CurrentBehaviourMatrix.md` |
| 0.2 | Core Behaviour Inventory | done | `docs/CurrentBehaviourMatrix.md` |
| 0.3 | Divergence Audit | done | `docs/DivergenceAudit.md` |
| 1.1 | Workspace Skeleton | done | Root `Cargo.toml` and `crates/` workspace skeleton |
| 1.2 | Shared Types Baseline | done | Domain request, result, error, and settings types in `extractor-domain` |
| 1.3 | GUI Shell Skeleton | done | `extractor-gui` app shell on `eframe` with the main layout regions wired |
| 2.1 | Time Parsing Port | done | `extractor-domain` time parsing utility with parity tests |
| 2.2 | Output Naming Port | done | `extractor-domain` output naming and time token utilities with parity tests |
| 2.3 | Request Validation Port | done | Shared request validation in `extractor-domain` |
| 2.4 | Extraction Plan Model | done | Shared ffmpeg argument plan generation in `extractor-domain` |
| 3.1 | Binary Discovery | done | Cross-platform `ffmpeg` and `ffprobe` lookup in `extractor-ffmpeg` |
| 3.2 | Probe Integration | done | Optional `ffprobe` duration probing in `extractor-ffmpeg` |
| 3.3 | ffmpeg Plan Rendering | done | Rendered ffmpeg command output from the shared extraction plan |
| 3.4 | Extraction Execution | done | Real ffmpeg execution path in `extractor-ffmpeg` |
| 3.5 | Desktop Open Adapter | done | Best-effort autoplay for Windows and Linux |
| 4.1 | Base clap Parser | done | Clap-based CLI with legacy PowerShell alias normalisation |
| 4.2 | Domain Wiring | done | CLI request mapping into the shared Rust domain |
| 4.3 | Runtime Wiring | done | CLI execution wired through the Rust runtime crate |
| 4.4 | Parity Hardening | done | Rust workspace tests plus CLI compatibility coverage for key aliases |
| 5.1 | App State Model | done | `extractor-gui` state model for form fields, status, logs, and background work |
| 5.2 | File And Time Panels | done | GUI file selectors and time controls |
| 5.3 | Processing And TTS Panels | done | GUI processing and TTS controls |
| 5.4 | Run And Log Flow | done | Background execution, busy state, status, and log panel |
| 5.5 | Settings And Persistence | done | Cross-platform persisted `ffmpeg` path settings |
| 5.6 | GUI Parity Review | done | GUI now runs on the same shared Rust core and uses the canonical Rust behaviour |

## Current Recommendation

The Rust-first refactor is functionally complete enough to treat the remaining work as polish and release hardening rather than blocked migration slices.

Rationale:

- The shared Rust core, runtime adapter, CLI, GUI, settings persistence, build scripts, and end-user docs are now in place.
- Remaining improvements are best handled as follow-up hardening, packaging, and UX polish rather than as missing core migration slices.
