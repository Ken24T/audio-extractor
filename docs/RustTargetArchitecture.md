# Rust Target Architecture

## Architecture Goals

- One Rust workspace for Windows and Linux
- One canonical domain model for extraction behaviour
- Zero Windows-specific logic in the domain layer
- CLI and GUI both consume the same application services
- All operating-system differences isolated behind adapters
- Packaging and runtime guidance explicit per platform

## Locked Decisions

- GUI stack: `egui` plus `eframe`
- CLI stack: `clap`
- Config: `toml` via `serde` with config-path resolution via `directories`
- Runtime media backend: external `ffmpeg` and optional `ffprobe`

These decisions are now the baseline for the implementation plan. Any change should be treated as an exception requiring a specific technical reason.

## Recommended Workspace Layout

```text
audio-extractor/
  Cargo.toml
  crates/
    extractor-domain/
    extractor-ffmpeg/
    extractor-cli/
    extractor-gui/
    extractor-platform/
    extractor-test-support/
```

## Crate Responsibilities

### `extractor-domain`

Pure logic only:

- extraction option structs
- validated command model
- time parsing
- output naming
- default values
- error types for invalid user input
- extraction plan generation independent of process launching

This crate must not:

- spawn processes
- inspect PATH
- read config files
- open desktop files
- know whether it is called by the CLI or GUI

### `extractor-ffmpeg`

Runtime integration layer:

- locate `ffmpeg` and optional `ffprobe`
- convert validated commands into concrete process invocations
- run probe operations
- run extraction operations
- surface structured failures

Recommended internal split:

- binary discovery
- probe service
- extraction executor
- command rendering for logs and tests

### `extractor-platform`

Small OS adapter layer:

- open output file in default app
- derive config directory location
- optional accent-colour support if retained
- OS-specific process lookup helpers where needed

The purpose of this crate is to keep platform branches out of the domain and ffmpeg crates.

### `extractor-cli`

Command-line app:

- parse arguments with `clap`
- map arguments into domain commands
- report validation and runtime errors cleanly
- print verbose command details when requested

The CLI should not own validation semantics beyond argument-shape parsing.

### `extractor-gui`

Cross-platform desktop app using `egui` and `eframe`:

- file picking
- editable extraction form
- log output
- settings editor
- execution progress state
- recent values and preferences persistence

The GUI should manage presentation state only. Extraction semantics must come from shared crates.

Recommended internal modules:

- `app_state`: persistent and transient UI state
- `panels`: file, timing, processing, settings, and log panels
- `actions`: user intents mapped to domain requests
- `tasks`: background extraction execution and result handoff
- `theme`: app colour tokens and visual rules
- `platform_ui`: file dialogs and OS-facing UI helpers that do not belong in core logic

### `extractor-test-support`

Shared fixtures and helpers for:

- golden tests
- temporary directory scenarios
- fake ffmpeg and ffprobe executors
- cross-platform path cases

## Data Flow

```text
CLI / GUI
  -> request model
  -> extractor-domain validation
  -> extraction plan
  -> extractor-ffmpeg execution
  -> structured result
  -> CLI / GUI presentation
```

## Canonical Domain Types

Recommended conceptual types:

- `RawExtractionRequest`
- `ValidatedExtractionRequest`
- `TimeSelection`
- `OutputMode`
- `TtsProfile`
- `OutputStrategy`
- `ExtractionPlan`
- `ProbeResult`
- `ExtractionOutcome`

The critical design point is to distinguish unvalidated user input from validated execution-ready commands.

## Validation Model

Validation should be centralised in the domain layer and return structured errors such as:

- missing input path
- invalid time format
- missing `start` when `end` or `duration` is present
- mutually exclusive `end` and `duration`
- invalid channel count
- invalid range relationships

This creates one authoritative behaviour source for both entry points.

## Output Naming Strategy

Define one naming policy and make both GUI and CLI use it.

Recommended rule:

- preserve current `_tts` and `_out` conventions
- preserve time-token suffixes
- preserve no-clobber behaviour unless `force` is set
- let the GUI preview the canonical name rather than invent its own default

## Process Execution Strategy

The executor should generate a structured ffmpeg invocation before running it. That plan should be testable without actually executing ffmpeg.

Recommended approach:

- domain generates an `ExtractionPlan`
- ffmpeg crate converts it to concrete process args
- logging uses the rendered command
- tests validate plan shape and args separately from runtime process execution

## Platform Strategy

### Windows

- support PATH lookup
- support explicit `ffmpeg` path selection
- provide packaged GUI binary
- retain autoplay if supported cleanly

### Linux

- support PATH lookup
- support explicit binary path override
- store config under XDG-conformant directories
- package GUI and CLI independently or together depending on release strategy

### Cross-platform rules

- do not assume `.exe`
- do not assume `where`
- do not assume registry access
- do not assume a single desktop environment for file opening

## Configuration Strategy

Recommended config file:

- format: TOML
- location:
  - Windows: user config directory via `directories`
  - Linux: XDG config directory via `directories`

Suggested config contents:

- `ffmpeg_path`
- `window_bounds`
- `window_state`
- recent extraction preferences
- optional theme preference

## GUI Strategy

The Rust GUI should aim for behavioural parity, not WPF visual parity.

Recommended MVP surface:

- input and output path controls
- time selection controls
- processing mode toggles
- TTS settings controls
- run button
- log panel
- settings panel or modal
- status indicator

Recommended egui layout strategy:

- left-to-right or top-to-bottom sections instead of a literal WPF group-box port
- a single primary window for MVP
- a settings modal or collapsible settings panel instead of a separate heavyweight window abstraction
- explicit disabled states and inline validation summaries
- a scrollable log pane backed by a simple in-memory event list

Deferred until after parity:

- accent-colour integration
- richer animations
- advanced multi-job support

What should not be carried over directly from WPF:

- code-behind style event coupling
- Windows registry-based accent lookup as a baseline requirement
- WPF-specific window-placement semantics
- assumptions that every input control maps to a distinct widget abstraction

## Packaging Strategy

Produce separate deliverables for:

- CLI only
- GUI only
- optional combined distribution bundle

Windows packaging candidates:

- zip first
- installer later if needed

Linux packaging candidates:

- tarball first
- AppImage or distro packages later if needed

## Remaining Decisions To Lock During Implementation

1. Rust edition
2. Autoplay policy on Linux
3. Packaging targets for first public Rust release
4. Whether accent-colour support is retained, simplified, or removed in the first cross-platform GUI release
