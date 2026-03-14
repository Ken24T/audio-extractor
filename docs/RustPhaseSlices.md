# Rust Phase Slices

## Purpose

This document breaks the larger migration phases into smaller execution slices that can be implemented, reviewed, and tested incrementally.

Each slice should be small enough to reason about in isolation and large enough to leave behind a coherent improvement.

## Slice Principles

- Favour vertical slices over giant horizontal rewrites where possible.
- Keep the domain layer ahead of the GUI layer.
- Do not let GUI work define business rules.
- Keep `egui` work thin and state-driven.
- Avoid mixing packaging work into core-behaviour slices.

## Phase 0 Slices: Behaviour Freeze And Spec Capture

### Slice 0.1: CLI Contract Inventory

- enumerate all flags and aliases from the current CLI
- document defaults and exit-code behaviour
- identify PowerShell-specific compatibility behaviour worth preserving or dropping

Output:

- CLI behaviour matrix

### Slice 0.2: Core Behaviour Inventory

- document validation rules
- document naming rules
- document ffprobe-dependent checks
- document autoplay expectations

Output:

- core behaviour reference

### Slice 0.3: Divergence Audit

- compare CLI behaviour, GUI behaviour, docs, and core service behaviour
- explicitly mark which layer becomes the source of truth in the Rust rewrite

Output:

- divergence list with decisions

## Phase 1 Slices: Rust Workspace Bootstrap

### Slice 1.1: Workspace Skeleton

- create Cargo workspace
- add crate directories and baseline manifests
- add formatting and lint conventions

Output:

- compiling empty workspace

### Slice 1.2: Shared Types Baseline

- define placeholder request, result, and error types
- establish crate dependency directions

Output:

- compile-checked architecture skeleton

### Slice 1.3: GUI Shell Skeleton

- add `extractor-gui` with `egui` and `eframe`
- create a placeholder app shell and main window layout regions

Output:

- app shell proves the chosen stack works on target platforms

## Phase 2 Slices: Domain Port

### Slice 2.1: Time Parsing Port

- port time parsing and validation
- add tests for current supported formats and invalid inputs

### Slice 2.2: Output Naming Port

- port canonical naming rules
- resolve current GUI versus core naming divergence
- add no-clobber naming tests

### Slice 2.3: Request Validation Port

- port option semantics into a validated request model
- add structured error types

### Slice 2.4: Extraction Plan Model

- define plan types that describe extraction operations without executing them

## Phase 3 Slices: ffmpeg And ffprobe Integration

### Slice 3.1: Binary Discovery

- implement PATH and explicit-path lookup without Windows-only assumptions

### Slice 3.2: Probe Integration

- implement optional ffprobe duration probing
- return structured outcomes rather than stringly-typed failures

### Slice 3.3: ffmpeg Plan Rendering

- convert validated domain plans into concrete ffmpeg arg lists
- add verbose-render tests

### Slice 3.4: Extraction Execution

- run ffmpeg plans and surface success and failure cleanly

### Slice 3.5: Desktop Open Adapter

- implement best-effort autoplay behind a platform boundary

## Phase 4 Slices: CLI Parity

### Slice 4.1: Base clap Parser

- implement argument shape and help output

### Slice 4.2: Domain Wiring

- map CLI args into validated domain requests

### Slice 4.3: Runtime Wiring

- connect CLI to ffmpeg execution and structured reporting

### Slice 4.4: Parity Hardening

- confirm defaults, aliases, and exit codes against the agreed spec

## Phase 5 Slices: egui GUI MVP

### Slice 5.1: App State Model

- define UI state containers
- separate persisted state from transient form state

### Slice 5.2: File And Time Panels

- implement input, output, and timing controls
- add canonical output-name preview behaviour

### Slice 5.3: Processing And TTS Panels

- implement processing toggles and numeric controls
- reflect domain defaults clearly in the UI

### Slice 5.4: Run And Log Flow

- implement run action, busy state, status text, and scrollable logs

### Slice 5.5: Settings And Persistence

- implement ffmpeg path settings
- implement cross-platform config persistence
- restore last-known window or UI preferences where supported

### Slice 5.6: GUI Parity Review

- compare the Rust GUI against agreed MVP behaviour rather than current WPF visuals

## Phase 6 Slices: Packaging And Distribution

### Slice 6.1: CLI Artifacts

- produce repeatable CLI builds for Windows and Linux

### Slice 6.2: GUI Artifacts

- produce repeatable GUI builds for Windows and Linux

### Slice 6.3: Runtime Dependency Guidance

- document how `ffmpeg` and `ffprobe` are located or configured on each platform

## Phase 7 Slices: Docs And Migration

### Slice 7.1: README Rewrite

- change the repo narrative from Windows .NET to cross-platform Rust

### Slice 7.2: User Guide Rewrite

- produce Windows and Linux run guidance

### Slice 7.3: Migration Notes

- document what changed from the .NET implementation

## Phase 8 Slices: Legacy Retirement

### Slice 8.1: Deprecation Decision

- decide whether the .NET implementation is archived or removed

### Slice 8.2: Repo Cleanup

- remove or relocate obsolete files, docs, and workflows

## Recommended First Implementation Sequence

1. Slice 0.1
2. Slice 0.2
3. Slice 0.3
4. Slice 1.1
5. Slice 1.2
6. Slice 2.1
7. Slice 2.2
8. Slice 2.3
9. Slice 2.4
10. Slice 3.1
11. Slice 3.2
12. Slice 3.3
13. Slice 3.4
14. Slice 4.1
15. Slice 4.2
16. Slice 4.3
17. Slice 4.4
18. Slice 1.3
19. Slice 5.1
20. Slice 5.2
21. Slice 5.3
22. Slice 5.4
23. Slice 5.5
24. Slice 5.6

This order keeps the domain and runtime stable before meaningful GUI work begins, while still validating the `egui` toolchain early enough to avoid late surprises.