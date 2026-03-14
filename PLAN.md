# Rust Cross-Platform Refactor Plan

## Goal

Refactor Audio Extractor from its current .NET and WPF Windows-first implementation into a Rust-first application that runs on Windows and Linux, while preserving the current extraction behaviour and improving architectural separation, portability, testability, and packaging.

This planning set is intentionally code-free. It defines the target architecture, migration phases, risks, delivery order, and acceptance criteria before implementation begins.

## Recommended Direction

- Keep `ffmpeg` and optional `ffprobe` as external runtime dependencies rather than re-implementing audio processing in Rust.
- Replace the current mixed CLI and WPF architecture with a Rust workspace containing separate crates for domain logic, ffmpeg integration, CLI, GUI, and integration tests.
- Treat the current .NET implementation as the behavioural reference, but not as the architectural template.
- Build the Rust GUI as a cross-platform desktop app rather than trying to preserve WPF concepts directly.

## Selected Rust Stack

- Language and toolchain: stable Rust 2024 edition if available at project start, otherwise 2021 edition
- Workspace layout: Cargo workspace with multiple crates
- CLI: `clap`
- GUI: `egui` with `eframe`
- Serialization: `serde`
- Config storage: `toml` plus `directories`
- Error handling: `thiserror` and `anyhow`
- Process execution: `std::process::Command` first, add async only if needed later
- Testing: `cargo test`, snapshot tests where useful, integration tests for CLI and ffmpeg argument generation

## Why `egui` / `eframe`

The current GUI is a utility-style form with file pickers, numeric inputs, checkboxes, a settings dialog, and a log panel. It does not depend on native widgets for its product value. `egui` is the lowest-risk cross-platform Rust GUI option for this shape of application because it offers:

- Mature Windows and Linux desktop support
- A fast path to a single-codebase utility interface
- Straightforward custom styling without re-creating WPF-like theming systems
- Easier packaging than a browser-based shell

Alternatives should only be revisited if there is a strong requirement for native-widget look and feel, advanced accessibility constraints, or a much richer desktop interaction model.

Decision status:

- Selected for the planning baseline and implementation roadmap.
- Alternatives such as `iced` and `Slint` are no longer the default planning assumption.
- Reopening the decision should require a concrete blocker discovered during implementation, not preference drift.

## Delivery Strategy

The migration should not be approached as a single rewrite step. It should be executed in controlled phases:

1. Freeze and document current behaviour precisely.
2. Design the Rust workspace and behavioural contracts.
3. Port pure domain logic first.
4. Port ffmpeg and ffprobe orchestration second.
5. Deliver CLI parity before GUI parity.
6. Build a cross-platform GUI on top of the validated Rust core.
7. Add packaging, distribution, and migration documentation.
8. Retire the .NET implementation only after parity is demonstrated.

## Planning Documents

- `docs/RustRefactorReview.md`: current-state review, findings, migration implications
- `docs/RustTargetArchitecture.md`: recommended Rust architecture and design decisions
- `docs/RustExecutionPlan.md`: phased implementation roadmap, risk register, and acceptance criteria
- `docs/RustPhaseSlices.md`: implementation slices within each phase for incremental delivery

## Success Criteria

- Windows and Linux CLI builds from the same Rust workspace
- Windows and Linux desktop GUI builds from the same Rust workspace
- Feature parity for core extraction flows, including TTS mode, no-TTS mode, naming, validation, and ffprobe-based duration guards
- No Windows-only assumptions in the domain or ffmpeg integration layers
- Behavioural contract captured by tests rather than only by current UI code and docs
- Packaging and runtime guidance updated for both operating systems

## Non-Goals For Phase 1

- Replacing `ffmpeg` with a native Rust audio processing stack
- Mobile support
- macOS support
- Major product redesign unrelated to portability
- Background media indexing, queueing, or library-management features

## Immediate Recommendation

Start the implementation effort by building the Rust core and CLI first. The current codebase contains Windows assumptions in both GUI and non-GUI layers, so forcing the GUI migration first would hide portability issues and delay a stable behavioural baseline.

Once the core and CLI are stable, implement the egui GUI as a thin stateful shell over the shared Rust services rather than rebuilding WPF concepts one-for-one.
