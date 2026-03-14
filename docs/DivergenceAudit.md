# Divergence Audit

## Purpose

This document records where current behaviour differs across the CLI, shared service, WPF GUI, legacy PowerShell script, and documentation.

It is the output of Slice 0.3.

## Source-Of-Truth Rule For The Migration

For the Rust port, the source-of-truth order is:

1. Shared extraction behaviour in the current .NET service, where that behaviour is internally coherent and tested
2. Current CLI behaviour for user-visible command-line semantics not contradicted by the service
3. Explicit migration decisions in this document where the current implementation diverges or is platform-bound
4. Existing docs only after they are checked against code

The current WPF UI and the legacy PowerShell script are reference inputs, not the default behavioural authority.

## Divergence List

### 1. Output naming differs between GUI and shared service

Observed behaviour:

- The shared service auto-generates names like `_tts` or `_out` with time tokens when output is omitted.
- The GUI pre-fills output as `<basename>.wav` when an input file is chosen.

Impact:

- CLI and GUI do not expose the same default naming contract.

Rust decision:

- Preserve the shared-service naming rules as the canonical behaviour.
- The Rust GUI should preview the canonical generated name instead of inventing a simpler default.

### 2. Legacy script and current .NET implementation disagree on end-time handling

Observed behaviour:

- The PowerShell script uses `-to <end>` when an end time is supplied.
- The .NET implementation converts end time into duration and uses `-t <end - start>` together with `-ss`.

Impact:

- Although the user-facing concept is similar, the runtime ffmpeg invocation is not identical.

Rust decision:

- Preserve the current .NET behaviour as the canonical baseline.
- Do not copy the legacy PowerShell `-to` behaviour unless a concrete regression is discovered.

### 3. Fractional seconds support differs between legacy script and current .NET implementation

Observed behaviour:

- The current .NET parser supports fractional seconds in the seconds field.
- The legacy PowerShell parser accepts only integer segments.

Impact:

- The current product is more capable than the legacy script.

Rust decision:

- Preserve fractional seconds support.

### 4. ffmpeg and ffprobe discovery is Windows-only in the current shared service

Observed behaviour:

- The current service uses `where` for PATH detection.
- It assumes `ffprobe.exe` when deriving a sibling executable from a provided ffmpeg path.

Impact:

- The so-called shared core is not cross-platform-ready.

Rust decision:

- Preserve the user-facing capability, not the Windows-specific mechanism.
- Replace these assumptions with platform-neutral discovery logic.

### 5. Autoplay defaults differ between CLI and GUI

Observed behaviour:

- CLI default autoplay is off.
- GUI default autoplay checkbox is on.

Impact:

- Cross-entry-point experience differs.

Rust decision:

- Keep the divergence for MVP if necessary, but document it explicitly.
- Reassess during GUI implementation whether both entry points should converge on one default.

### 6. Help output, implementation, and docs are out of sync

Observed behaviour:

- The CLI implementation supports `--autoplay` and `--verbose`.
- Help text does not currently list those options.
- README and UserGuide include additional drift and formatting issues.

Impact:

- User-facing documentation is not a reliable authority.

Rust decision:

- Treat code and explicit planning artefacts as authoritative until docs are rebuilt.
- The Rust CLI help and documentation must be generated from one consistent contract.

### 7. ffmpeg failure handling differs between legacy script and current CLI

Observed behaviour:

- The PowerShell script exits with code `10` on ffmpeg failure.
- The current .NET implementation catches runtime exceptions and returns a generic failure exit code `2`.

Impact:

- There is no stable historical exit-code contract across implementations.

Rust decision:

- Preserve the current .NET CLI behaviour unless a clearer exit-code scheme is intentionally designed later.

### 8. GUI validation is partly duplicated outside the shared service

Observed behaviour:

- GUI validates numeric textbox input and missing input path itself.
- Shared service validates extraction semantics afterward.
- CLI performs argument-shape parsing and then hands off to the service.

Impact:

- Validation responsibilities are split and duplicated.

Rust decision:

- Centralise behavioural validation in the Rust domain layer.
- Keep GUI and CLI validation limited to input-shape and presentation concerns.

### 9. GUI theming is Windows-specific and not product-critical

Observed behaviour:

- The GUI reads Windows accent colour from the registry and WPF system parameters.
- The visual shell uses WPF-specific styling and layout concepts.

Impact:

- This is cosmetic logic with no portable behaviour guarantee.

Rust decision:

- Do not preserve Windows accent integration as a baseline requirement.
- A simple cross-platform theme should be the MVP default.

## Preserve, Adapt, Drop Matrix

| Behaviour area | Decision | Reason |
|---|---|---|
| Time parsing including fractional seconds | Preserve | Current implementation is coherent and tested |
| TTS defaults and filter concept | Preserve | Core product behaviour |
| No-clobber output naming | Preserve | User-visible behaviour and tested utility logic |
| GUI default output of `<basename>.wav` | Drop | Conflicts with shared-service naming authority |
| Windows `where` lookup | Drop | Platform-specific implementation detail |
| Windows registry accent detection | Drop for MVP | Cosmetic and non-portable |
| Legacy script `-to` end handling | Drop | Conflicts with current service baseline |
| Legacy ffmpeg failure exit code `10` | Drop | Not reflected in current active CLI |
| CLI/GUI shared extraction semantics | Preserve and centralise | Required for Rust architecture |

## Explicit Migration Decisions

### Decision 1

The Rust implementation should model the shared extraction behaviour as the canonical domain contract, not the WPF GUI defaults.

### Decision 2

The Rust GUI should be a thin shell over the domain and ffmpeg crates. It should not become the place where behavioural rules live.

### Decision 3

The migration should prefer preserving current active .NET behaviour over preserving legacy PowerShell behaviour when the two conflict.

### Decision 4

Documentation must be rebuilt from the explicit behaviour matrix and migration decisions, not copied from current docs verbatim.

## Open Items Remaining For Later Phases

These are not blockers for Slice 0 completion, but they still require later product decisions:

- whether CLI and GUI autoplay defaults should converge
- whether Linux autoplay should be best-effort or explicitly disabled unless configured
- whether window placement persistence should be preserved in the first Rust GUI release or simplified
- whether accent-colour support returns after GUI parity is achieved
