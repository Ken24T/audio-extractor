# Rust Refactor Review

## Scope Of Review

This review covers the current implementation across:

- CLI entry point and argument parsing
- Shared extraction logic and ffmpeg orchestration
- WPF GUI and settings persistence
- Tests
- Build and packaging metadata
- End-user documentation

## Current Architecture Summary

The application is split into three .NET projects plus tests:

- `src/AudioExtractor`: CLI entry point and manual argument parsing
- `src/AudioExtractor.Core`: extraction defaults, validation, naming, ffmpeg and ffprobe orchestration
- `src/AudioExtractor.Gui`: WPF desktop application using the shared core
- `tests/AudioExtractor.Tests`: unit tests for parsing and utility logic

At a high level, the current design already has one useful migration property: the extraction service is separate from the WPF UI. However, that separation is incomplete because the service still contains Windows-specific process and executable assumptions.

## Strengths Worth Preserving

### Shared extraction core exists already

The repo has already moved away from a script-only design. The extraction logic lives in a reusable service instead of being duplicated fully between CLI and GUI.

### Behaviour is narrow and well-bounded

The product scope is focused:

- parse user input
- validate extraction parameters
- derive output naming
- call `ffmpeg`
- optionally call `ffprobe`
- optionally open the result

This is a good fit for a Rust port.

### Existing tests capture some important invariants

Current tests cover:

- time parsing
- filename token generation
- argument parsing defaults and flag handling

That is not enough for a safe rewrite, but it is a useful start.

## Findings

### High: the so-called core layer is not actually cross-platform-ready

The current extraction service embeds Windows assumptions directly:

- `where ffmpeg` and `where ffprobe` are used for PATH detection
- `ffprobe.exe` is assumed when deriving sibling executables
- `ProcessWindowStyle.Hidden` is set on the ffmpeg process

Implication:

The current service cannot be ported mechanically. The Rust core must explicitly separate pure domain logic from operating-system-aware process discovery and launch behaviour.

### High: GUI defaults diverge from CLI and core behaviour

The GUI pre-populates output as `<basename>.wav`, while the core service auto-generates `_tts` or `_out` names with time tokens when output is omitted.

Implication:

The application currently does not have a single canonical output naming contract across entry points. The Rust rewrite must define one authoritative rule set and make both GUI and CLI consume it.

### High: validation rules are distributed across multiple layers

The CLI parses flags, the GUI validates numeric text fields, and the service validates extraction semantics. These layers overlap, but they do not share a single command model.

Implication:

The Rust implementation should define:

- one input command model
- one validation layer
- one naming layer
- one ffmpeg execution layer

UI and CLI should only translate user interaction into that model.

### Medium: autoplay behaviour is underspecified for Linux

The current implementation uses shell execution on a file path and assumes the operating system will open the file in a default application. That is fine on Windows, but the cross-platform behaviour must be defined explicitly for Linux.

Implication:

The migration needs a portability decision for autoplay:

- keep it on both platforms
- keep it only where a supported opener exists
- make it best-effort with clear user messaging

### Medium: settings persistence is currently Windows-centric in practice

The settings class uses `LocalApplicationData` and WPF virtual screen metrics. The app also uses Windows registry lookups for accent colour.

Implication:

Configuration, theme selection, and window-state persistence must be redesigned around cross-platform conventions, not ported directly.

### Medium: GUI architecture is tightly coupled to WPF event handlers

The GUI is organised as code-behind around XAML controls. Most user interaction logic is embedded in the window class.

Implication:

The Rust GUI should not imitate WPF code-behind. It should adopt a state-driven UI model where:

- the GUI owns presentation state
- the domain layer owns validation and execution rules
- background work is explicit

### Medium: test coverage is too shallow for a safe rewrite

The current tests mostly cover utilities and argument parsing. They do not comprehensively cover:

- extraction service behaviour
- ffmpeg argument generation
- PATH discovery
- no-clobber behaviour
- duration guard edge cases
- autoplay behaviour
- GUI-to-core contract

Implication:

The refactor should start by expanding the behavioural spec, not by writing Rust UI code.

### Low: documentation drift exists already

The documentation contains visible inconsistencies and formatting corruption in examples and option descriptions.

Implication:

The current docs are not sufficient as the source of truth for migration. Behaviour must be captured from code and tests, then documented cleanly during the port.

## Current Behaviour That Must Be Preserved

### Core extraction rules

- Input file is required
- `--end` and `--duration` are mutually exclusive
- `--end` or `--duration` requires `--start`
- `duration > 0`
- `end > start`
- optional ffprobe duration guards
- no-clobber naming unless forced

### Output formats and processing

- WAV PCM 16-bit output
- Default TTS path: mono, 24 kHz, high-pass 80 Hz, low-pass 11 kHz, loudnorm target -16 LUFS
- `--no-tts` path preserves format-related controls for channels and sample rate

### User-facing interaction

- explicit success or failure output
- verbose mode prints ffmpeg invocation
- GUI shows a simple progress state and log
- settings retain ffmpeg path and window placement

## Migration Implications

### What can be ported directly in concept

- time parsing rules
- naming rules, once unified
- extraction option model
- validation semantics
- ffmpeg filter construction

### What should be redesigned rather than ported literally

- CLI parsing implementation
- process discovery and launch behaviour
- GUI architecture
- settings storage conventions
- theming and accent-colour integration
- packaging and distribution

## Recommended First Principles For The Rewrite

1. Treat the behavioural contract as the thing to preserve, not the current .NET shape.
2. Move every Windows assumption to a platform adapter or remove it entirely.
3. Make domain validation independent of CLI or GUI.
4. Make the GUI consume the same command model as the CLI.
5. Build test coverage around generated ffmpeg plans before rebuilding the GUI.

## Summary Assessment

This project is a good candidate for a Rust refactor because the product scope is compact and the current business logic is centred on external process orchestration. The main risk is not algorithmic complexity. The real risk is behavioural drift caused by hidden Windows assumptions and duplicated entry-point logic. The refactor should therefore be organised around behaviour capture and architectural separation, not a line-by-line port.
