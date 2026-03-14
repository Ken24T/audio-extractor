# Rust Execution Plan

## Phase Overview

The refactor should be executed in eight phases with explicit gates between them.

The GUI technology decision is now locked to `egui` plus `eframe`, so the plan below assumes that stack throughout Phase 5 and later documentation work.

## Phase 0: Behaviour Freeze And Spec Capture

### Objective

Create a trustworthy behavioural specification from the current implementation before any porting begins.

### Work

- Catalogue all existing CLI options and defaults
- Record the actual output naming rules from the core service
- Record no-clobber behaviour
- Record duration guard behaviour with and without ffprobe
- Record autoplay behaviour and current expectations
- Record GUI settings that must persist across sessions
- Identify current docs drift and correct the spec documents if needed

### Deliverables

- behaviour matrix
- known divergence list between CLI, GUI, docs, and core
- migration acceptance checklist

### Exit Criteria

- the team agrees on the canonical behaviour to preserve
- unresolved ambiguities are documented as explicit design decisions

## Phase 1: Rust Workspace Bootstrap

### Objective

Create the empty Rust workspace and crate boundaries without implementing the product.

### Work

- create Cargo workspace
- add core crates and dependency baseline
- define lint, format, and test commands
- define CI shape for Windows and Linux builds
- add initial package metadata
- add the GUI crate with `egui` and `eframe` dependencies, even if it remains mostly skeletal in this phase

### Deliverables

- Rust workspace skeleton
- contributor commands for build, test, and lint
- initial CI matrix

### Exit Criteria

- workspace builds on Windows and Linux
- empty crates compile cleanly under CI

## Phase 2: Domain Port

### Objective

Port pure logic into `extractor-domain` with no OS dependencies.

### Work

- port time parsing
- port output naming
- port default values
- port extraction request validation
- define structured domain errors
- define extraction plan model

### Deliverables

- pure Rust domain crate
- unit tests covering current behaviour and edge cases

### Exit Criteria

- domain tests pass on Windows and Linux
- no platform-specific code exists in the domain crate

## Phase 3: ffmpeg And ffprobe Integration Port

### Objective

Build the runtime adapter that turns validated plans into real ffmpeg and ffprobe calls.

### Work

- design cross-platform binary discovery
- implement explicit binary override support
- implement optional ffprobe duration probing
- implement command rendering for verbose logs
- implement no-clobber file path flow
- implement autoplay adapter boundary

### Deliverables

- `extractor-ffmpeg` crate
- integration tests with fakes or controlled fixtures

### Exit Criteria

- generated args match agreed behaviour
- binary discovery works on Windows and Linux
- runtime failures are surfaced with structured messages

## Phase 4: CLI Parity Release Candidate

### Objective

Deliver a Rust CLI that can stand in for the current .NET CLI.

### Work

- implement `clap` parser
- map parsed args into domain requests
- wire domain and ffmpeg crates
- implement help output and exit codes
- verify PowerShell and shell usage examples for both Windows and Linux

### Deliverables

- runnable Rust CLI
- CLI parity checklist
- updated CLI documentation draft

### Exit Criteria

- CLI parity confirmed for supported flags and default behaviour
- CLI smoke tests pass on Windows and Linux

## Phase 5: GUI MVP On Rust Core

### Objective

Build a cross-platform `egui` desktop GUI on top of the validated Rust core.

### Work

- implement app state model
- define a stable `egui` layout structure for the main extraction workflow
- implement input and output selectors
- implement time controls
- implement processing and TTS controls
- implement run workflow with background execution
- implement log panel and status feedback
- implement settings storage and restore
- implement file dialog integration suitable for Windows and Linux

### Deliverables

- Windows and Linux GUI MVP
- GUI parity checklist
- `egui` view map showing how current WPF workflow maps into Rust panels and state

### Exit Criteria

- GUI can execute the same extraction scenarios as the CLI
- settings persist correctly on both platforms
- GUI remains responsive during extraction
- GUI code remains thin over the shared domain and ffmpeg crates

## Phase 6: Packaging And Distribution

### Objective

Prepare repeatable delivery for both supported operating systems.

### Work

- define release artifact layout
- build Windows distribution package
- build Linux distribution package
- document runtime dependency handling for ffmpeg and ffprobe
- decide whether ffmpeg is bundled, discovered, or both

### Deliverables

- release packaging scripts or CI jobs
- install and run instructions

### Exit Criteria

- release artifacts are reproducible
- packaging docs are accurate for both platforms

## Phase 7: Documentation And Migration Release

### Objective

Transition the repo and users from .NET-first to Rust-first.

### Work

- rewrite README around the Rust architecture
- update user guide for Windows and Linux
- document migration from the existing .NET binaries
- document support policy and known limitations

### Deliverables

- updated README
- updated user guide
- migration notes

### Exit Criteria

- docs match the shipped binaries
- users can install and run on both operating systems from docs alone

## Phase 8: Legacy Retirement

### Objective

Remove or archive the old stack once Rust parity is proven.

### Work

- decide whether to archive or delete the .NET and PowerShell implementation
- remove obsolete build and packaging instructions
- remove Windows-only implementation details from user-facing docs

### Deliverables

- deprecation notice or archive plan
- cleaned repo structure

### Exit Criteria

- no active docs or CI assume the old implementation remains primary

## Risk Register

### Risk: hidden behavioural drift between GUI and CLI

Impact:

- users see different defaults after migration

Mitigation:

- define one canonical domain model first
- build parity tests against agreed scenarios

### Risk: Linux desktop integration is underspecified

Impact:

- autoplay, file picker behaviour, and packaging become inconsistent

Mitigation:

- lock OS-level behaviour decisions before GUI implementation
- keep Linux MVP conservative and explicit

### Risk: GUI toolkit churn or limitations

Impact:

- implementation slows or requires rework

Mitigation:

- keep GUI MVP simple
- avoid speculative platform polish in the first delivery
- do not revisit the toolkit choice unless a concrete blocker is documented

### Risk: ffmpeg discovery remains fragile

Impact:

- support burden rises on both platforms

Mitigation:

- support both PATH and explicit binary path
- add diagnostics that tell the user exactly what executable path was attempted

### Risk: test coverage stays too shallow

Impact:

- behavioural regressions are found late in manual testing

Mitigation:

- invest in plan and argument-generation tests early
- add integration tests before GUI work scales up

### Risk: packaging expands scope too early

Impact:

- platform release work blocks core parity

Mitigation:

- deliver raw artifacts first
- add polished packaging only after CLI and GUI parity

## Acceptance Criteria By Capability

### CLI

- parses supported flags correctly
- preserves defaults and validation rules
- prints actionable errors
- runs on Windows and Linux

### Core extraction

- generates correct ffmpeg arguments
- preserves no-clobber behaviour
- preserves TTS defaults
- preserves no-TTS overrides
- handles optional ffprobe duration validation

### GUI

- allows full extraction configuration without shell usage
- displays logs and status
- persists user preferences
- runs on Windows and Linux from the same codebase

### Packaging and docs

- both platforms have documented install and run paths
- users understand ffmpeg runtime expectations
- repo docs no longer describe the product as Windows-only

## Recommended Initial Milestone Breakdown

1. Behaviour freeze and divergence audit
2. Rust workspace and domain crate
3. ffmpeg integration and CLI parity
4. cross-platform `egui` GUI MVP
5. packaging and docs
6. legacy retirement decision

## Phase Slice Policy

Each phase should be delivered in small slices that satisfy all of these rules:

- one slice has one primary objective
- a slice should be reviewable without reading the whole migration
- a slice should leave the repo in a coherent state
- later slices may depend on earlier slices, but should avoid hidden coupling

Detailed slice definitions are maintained in `docs/RustPhaseSlices.md`.


## Recommendation On Branching During Implementation

When implementation begins, create separate feature branches per phase or per vertical slice rather than one long-lived rewrite branch. The current branch should remain the planning branch and the source of truth for migration scope and sequencing.
