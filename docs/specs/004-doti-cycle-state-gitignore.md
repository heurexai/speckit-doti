# Spec: Doti Cycle State Gitignore

> WHAT and WHY only. This fixes a v0.7.1 regression where updated or custom-installed repos can receive Doti cycle commands without ignoring the Doti cycle-state file those commands write.

## Goal

Generated, installed, and updated repos must ignore the same Doti runtime state paths that the Doti cycle engine writes and reads. In particular, `.doti/cycle-state.json` and `.doti/gate-proof.json` must be ignored in target repos so a successful `doti cycle stamp --stage specify` does not immediately make its own proof stale by creating an untracked state file.

This matters because `CycleStateStore` writes `.doti/cycle-state.json`, docs describe that file as gitignored, and `doti cycle commit` depends on the same path. If a target repo only ignores a product-specific path such as `.nomos/cycle-state.json`, the first stamp changes the working diff and the next stage can fail with stale prerequisites.

## Scope

Included behavior:

- Keep the Doti cycle-state path as `.doti/cycle-state.json`.
- Keep the Doti gate-proof path as `.doti/gate-proof.json`.
- Ensure `doti install` creates or updates root `.gitignore` with those Doti runtime-state entries.
- Ensure `hx update` creates or updates root `.gitignore` with those Doti runtime-state entries for existing repos.
- Preserve existing `.gitignore` content, including product-specific ignores such as `.nomos/cycle-state.json`.
- Report `.gitignore` as a planned/changed update path when update needs to add the entries.
- Keep generated dotnet-cli repos covered by the template `.gitignore`.

Excluded behavior:

- Moving cycle state to `.nomos/cycle-state.json`.
- Removing or rewriting product-specific ignore entries.
- Treating `.doti/cycle-state.json` or `.doti/gate-proof.json` as managed assets to copy from payloads.

## Functional Requirements

- `FR-001`: `CycleStateStore` MUST continue to read and write `.doti/cycle-state.json`.
- `FR-002`: `GateProofStore` MUST continue to read and write `.doti/gate-proof.json`.
- `FR-003`: A generated repo MUST ignore `.doti/cycle-state.json`.
- `FR-004`: A generated repo MUST ignore `.doti/gate-proof.json`.
- `FR-005`: `doti install` MUST add missing `.doti/cycle-state.json` and `.doti/gate-proof.json` entries to the target repo root `.gitignore`.
- `FR-006`: `hx update` MUST add missing `.doti/cycle-state.json` and `.doti/gate-proof.json` entries to the target repo root `.gitignore`.
- `FR-007`: The gitignore update MUST append missing entries without deleting existing content.
- `FR-008`: The gitignore update MUST be idempotent.
- `FR-009`: `hx update` MUST include `.gitignore` in planned and changed paths when it adds missing Doti runtime-state entries.
- `FR-010`: `hx update` MUST respect the dirty planned-path blocker if `.gitignore` is dirty and update would need to modify it.

## Success Criteria

- `SC-001`: A repo whose `.gitignore` only contains `.nomos/cycle-state.json` receives `.doti/cycle-state.json` and `.doti/gate-proof.json` through `hx update`.
- `SC-002`: `doti install` into a repo with no `.gitignore` creates one with both Doti runtime-state entries.
- `SC-003`: `doti install` into a repo with existing `.gitignore` preserves existing lines and appends missing Doti runtime-state entries.
- `SC-004`: In a Git repo with the corrected `.gitignore`, `doti cycle stamp --stage specify --feature 001-example --repo . --json` can be followed by `doti cycle stamp --stage clarify --feature 001-example --repo . --json` without the first stamp becoming stale solely because `.doti/cycle-state.json` was written.
- `SC-005`: `doti cycle status` and `doti cycle commit` continue to use the same `.doti/cycle-state.json` path that `stamp` writes.

## Clarifications

### 2026-06-24

- No operator question was needed. The bug report identified the intended path and the mismatch: code and docs use `.doti/cycle-state.json`, while an updated repo ignored only `.nomos/cycle-state.json`.
