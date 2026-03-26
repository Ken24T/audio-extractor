# Audio Extractor — TCTBP Cheatsheet

Short operator reference for the Audio Extractor workflows.

Use this file for the quick view.
Use [TCTBP Agent.md](TCTBP%20Agent.md) for the full workflow rules and guard rails.

## Core Rule

- No code is ever lost while syncing local and remote state.
- Do not use destructive shortcuts as part of normal workflow execution.
- If a workflow hits divergence, ambiguity, failed verification, or stale release state, it should stop rather than guess.

## Repo Gates

- Format check: `n/a`
- Test: `cargo test --workspace`
- Lint: `n/a`
- Normal build gate: `cargo check --workspace`
- Release/package build: `cargo build --release -p extractor-cli -p extractor-gui`

## Version And Tags

- Version source: `Cargo.toml` field `workspace.package.version`
- Tag format: `X.Y.Z`

## Triggers

### `ship` / `ship please` / `shipping` / `prepare release`

Formal source release workflow.

- preflights repo state
- runs verification gates
- assesses docs impact
- bumps version when required
- commits, tags, and pushes

### `publish` / `publish please`

Safely publish the current clean branch to `origin` without release semantics.

- no version bump
- no tag creation
- no handover metadata update

### `handover` / `handover please`

Safely checkpoint and publish the current work branch, then refresh `tctbp/handover-state` so another machine can resume deterministically.

### `resume` / `resume please`

Safely restore the intended work branch at the start of a session by consulting handover metadata first.

### `deploy` / `deploy please`

Run an explicit current-platform release artefact build.

Repo-specific target:

- `current-platform-artifacts`
  - build: `cargo build --release -p extractor-cli -p extractor-gui`
  - helper: `./scripts/build-rust-artifacts.sh`
  - validate: confirm release output exists in `target/release`

### `status` / `status please`

Read-only operator snapshot of branch state, sync status, tags, version source, and recommended next steps.

### `abort`

Inspect and recover from a partially completed SHIP, sync, or deploy workflow.

### `branch <new-branch-name>`

Close out current work cleanly and start the next branch.

- asks for explicit confirmation before merging a non-default branch back into `master`
- requires the source branch to be published before the transition continues

## Docs Impact Reminder

Repo-specific docs commonly reviewed:

- `README.md`
- `docs/UserGuide.md`
- `docs/MigrationNotes.md`
- `PLAN.md`
- `Cargo.toml`
- `scripts/build-rust-artifacts.sh`
- `scripts/build-rust-artifacts.ps1`
- `.github/TCTBP Agent.md`
- `.github/TCTBP Cheatsheet.md`
- `.github/copilot-instructions.md`

## Quick Choice

- Need a release version or tag: use `ship`
- Need to sync a clean branch without release or metadata side effects: use `publish`
- Need to stop on one machine and resume on another safely: use `handover`
- Need to restore the last handed-over branch before starting work: use `resume`
- Need current-platform release artefacts built: use `deploy`
- Need a quick repo state check: use `status`
- Need to recover from a partial workflow state: use `abort`
- Need to start the next branch: use `branch <new-branch-name>`