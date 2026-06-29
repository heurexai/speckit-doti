# 022 — Doti repo version lifecycle

## Goal

An operator (or agent) managing one or many repos that have Doti installed can, at any time, **see what Doti version a repo is on**, **safely update a repo's tool-owned assets to the installed tool's version with explicit before→after feedback**, **scan a directory tree for every Doti repo and its version**, and **batch-update them all** — each surface rendered in full detail for both humans (rich + plain) and agents (the `CliResult` JSON envelope).

Updating is **non-destructive by construction**: it touches only **tool-owned managed assets** (never operator-owned content like the constitution), it **detects and preserves operator-customized managed assets** (reporting them instead of clobbering), and it applies every change in an **isolated git worktree** that is reviewed before it merges back into the repo.

This closes a real, observed gap: running `hx doti install` then `hx doti payload check` on a repo reports `upgraded-existing-doti-repo; installed=9, preserved=1` and `parity passed for 93 managed file(s)` — but **never states the version the repo is now on**, and `hx version --repo` actively *mis-reports* it (see FR-003).

## User Scenarios & Testing

**Priority Mode** — **code / generated-code** is dominant (four new user-facing CLI commands + one behavior fix, ordered into independently testable value slices). The **workflow/tooling** discipline is a hard constraint on every slice: each command is fail-closed, git-backed where it mutates, and emits the deterministic `CliResult` JSON proof. Read-only visibility (US1, US2) is sequenced before mutation (US3, US4) so the smallest, lowest-risk value lands first.

### User Story 1 — Know a repo's Doti version (Priority: P1)

An operator points a command at a repo and immediately learns which Doti version it is on and whether that is current relative to their installed tool — one command, human or JSON.

- **Why this priority:** Foundational, smallest slice, and the direct answer to the observed gap ("confirm the version"). All other stories reuse its version-read + relation logic.
- **Independent Test:** Run `check-version` against `speckit-nomos` (stamped `0.13.3`); confirm it reports `0.13.3` + relation `current` against an installed `0.13.3` tool in both human and JSON, and that `hx version --repo` no longer reports `newer` for that same clean repo.
- **Acceptance Scenarios:**
  1. **Given** a repo whose `.doti/payload.json` says `payloadVersion: 0.13.3` and an installed tool at `0.13.3`, **When** `hx doti check-version --repo <path>` runs, **Then** it states repo version `0.13.3`, tool version `0.13.3`, relation `current` — human line + JSON `data`.
  2. **Given** the same repo, **When** `hx version --repo <path>` runs, **Then** `targetRelation` reads `current`/`match` (from `payload.json`), not `newer`.
  3. **Given** a non-Doti directory, **When** `check-version` runs, **Then** it fails closed, distinguishing "not a Doti repo" from "Doti repo, version unknown".

### User Story 2 — Scan a tree for every Doti repo and its version (Priority: P2)

An operator points a command at a root directory and gets a table of every Doti repo beneath it with each repo's version and whether it is current.

- **Why this priority:** Fleet visibility; read-only and safe to run anywhere; it is also the preview surface the batch update builds on.
- **Independent Test:** Build a root with two Doti repos at different versions + one non-Doti directory; run `hx doti scan --root <dir>`; confirm exactly the two Doti repos appear with correct versions + relations (table for humans, array for JSON), and the non-Doti directory does not.
- **Acceptance Scenarios:**
  1. **Given** a root with N Doti repos at assorted versions, **When** `hx doti scan --root <dir>` runs, **Then** it lists all N with path, repo version, tool version, and relation — a formatted table + an N-entry JSON array.
  2. **Given** a root with no Doti repos, **When** `scan` runs, **Then** it reports an explicit empty success (zero rows), not an error.
  3. **Given** a discovered repo that is unreadable or has a malformed `payload.json`, **When** `scan` runs, **Then** that repo is listed with relation `unknown` + a reason and the scan still completes.

### User Story 3 — Safely update one repo, preserving customizations (Priority: P3)

An operator updates an existing Doti repo's tool-owned assets to the installed tool's version. The command tells them the before→after version, **leaves operator-owned content and any customized managed asset untouched while reporting the customizations**, applies the change in a git worktree it can preview, and merges back only on acceptance.

- **Why this priority:** Directly removes the "install didn't tell me the version" pain *and* the deeper risk that an update silently clobbers an operator's edits. It carries the customization-detection + worktree machinery the batch update reuses.
- **Independent Test:** Take a repo stamped older with one managed template hand-edited; run `hx doti update --repo <path>` with a newer tool; confirm it reports before→after, reports the edited template as customized-and-kept, leaves the constitution untouched, and (without `--force`) does not overwrite the edited template — and that `--force` does overwrite it.
- **Acceptance Scenarios:**
  1. **Given** a Doti repo stamped `0.13.2`, an installed tool at `0.13.5`, and no customizations, **When** `hx doti update --repo <path>` runs, **Then** tool-owned assets reach `0.13.5`, operator-owned content (e.g. `.doti/memory/constitution.md`) is untouched, and the output states `0.13.2 → 0.13.5` (human + JSON).
  2. **Given** a repo where a managed template has been edited (beyond whitespace), **When** `update` runs without `--force`, **Then** that template is **not** overwritten, it is reported as customized (path + "kept"), other non-customized assets still update, and the result makes the customization visible.
  3. **Given** the same customized repo, **When** `update --force` runs, **Then** the customized template is overwritten with the new payload and reported as force-updated.
  4. **Given** any update, **When** it runs, **Then** the change is staged in a git worktree and `--dry-run` shows the would-be diff without merging; on a normal run the reviewed change merges back into the repo.
  5. **Given** git is unavailable, **When** `update` runs, **Then** it fails hard (git is required).
  6. **Given** a repo already at the tool's version, **When** `update` runs, **Then** it reports `already current at <version>` and changes nothing.
  7. **Given** a directory that is not yet a Doti repo, **When** `update` runs, **Then** it fails closed and directs the operator to `hx doti install`.

### User Story 4 — Batch-update every Doti repo under a root (Priority: P4)

An operator updates all Doti repos under a directory tree to the installed tool's version in one command, with the same customization-safety and worktree mechanics per repo, plus a per-repo and summary report.

- **Why this priority:** Highest-leverage convenience, built on US3 (update) applied across US2 (scan); it carries the most mutation risk, so it ranks last and is fail-soft.
- **Independent Test:** With a root of mixed-version Doti repos (one customized, one outdated, one current), run `hx doti update-all --root <dir>`; confirm the outdated one updates, the current one is skipped, the customized one is reported-and-kept, and the summary reports updated / already-current / customized-skipped / failed counts.
- **Acceptance Scenarios:**
  1. **Given** a root with a mix of outdated, current, and customized Doti repos, **When** `hx doti update-all --root <dir>` runs, **Then** each outdated repo updates (worktree-applied, customizations preserved + reported), current ones are skipped, and the output reports per-repo before→after + an updated / already-current / customized-skipped / failed summary (human + JSON).
  2. **Given** `--dry-run`, **When** `update-all` runs, **Then** it previews per-repo what would change and mutates nothing.
  3. **Given** one repo fails to update, **When** `update-all` runs, **Then** the remaining repos still update, the failed repo is recorded with a reason, and the overall outcome reflects the failure (fail-soft, never abort-on-first).

### Edge Cases

- A `.doti/` directory with no `payload.json` (partial/older install) → "Doti repo, version unknown", distinct from "not a Doti repo".
- A repo whose `payloadVersion` is *newer* than the tool (relation `ahead`) → reported, never silently downgraded; `update` reports the ahead state rather than rolling back.
- A repo with **no managed-asset hash baseline** (older install) → `update` still proceeds but warns that per-asset customization detection is unavailable for that repo, and writes the baseline so detection works next time.
- A managed asset that differs from the baseline **only by whitespace/EOL** → treated as **not** customized (whitespace-insensitive hashing); it updates normally.
- Operator-owned content (constitution) → never a managed template; never overwritten, even with `--force`.
- Nested Doti repos under a root → each `.doti/payload.json` is one entry; discovery does not descend into a found repo's `.git`/vendored internals.
- Symlink loops / permission-denied directories during `scan`/`update-all` → skipped with a reason; the operation completes.
- A worktree left over from an interrupted prior run → detected and cleaned/reused, never silently corrupting the repo.

## Scope

**Included:** the four commands (`check-version`, `update`, `scan`, `update-all`); the `hx version --repo` `targetRelation` source fix (FR-003); whitespace-insensitive **customization detection** for tool-owned managed assets, with preserve-and-report default + `--force` override; **git-worktree-based mutation** with `--dry-run` preview and merge-back-on-acceptance (git is a hard prerequisite); a single shared version-relation model and a single shared customization-hash; full human (rich + plain) **and** JSON rendering on every surface; fail-closed/fail-soft semantics and stable diagnostic codes.

**Explicitly excluded:** updating the installed `hx` **tool** itself (that stays the tool channel — `dotnet tool update -g Heurex.SpeckitDoti` / Microsoft Store — and the existing `/doti-upgrade` utility; `update`/`update-all` reconcile repos to whatever tool is installed); modifying operator-owned content (constitution and any non-managed file); release/publish behavior; remote/networked discovery (local filesystem under `--root` only); GUI.

## Functional Requirements

- `FR-001`: `hx doti check-version --repo <path>` MUST report the repo's Doti version from `.doti/payload.json` (`payloadVersion` + `toolVersion`), as a human line and in the JSON `data`. `[US1]`
- `FR-002`: `check-version` MUST classify the repo's relation to the installed tool — `current` / `outdated` / `ahead` / `unknown` — and surface the installed-tool version alongside the repo version. `[US1]`
- `FR-003`: `hx version --repo` MUST compute `targetRelation` from `.doti/payload.json`, falling back to `.doti/scaffold-version.json` only when `payload.json` is absent — so a clean Doti-adopted repo whose payload equals the tool reports `current`/`match`, not `newer`. `[US1]`
- `FR-004`: `check-version` MUST fail closed with distinct diagnostics for "not a Doti repo" vs "Doti repo, version unknown". `[US1]`
- `FR-005`: `hx doti scan --root <dir>` MUST recursively discover every Doti-enabled repo under the root (those carrying `.doti/payload.json`) and report, per repo: path, repo version, tool version, relation. `[US2]`
- `FR-006`: `scan` MUST render a formatted table for humans and a machine array in JSON; zero Doti repos is an explicit empty success, not an error. `[US2]`
- `FR-007`: `scan` MUST be strictly read-only and MUST complete past an unreadable/malformed repo, recording it with relation `unknown` + a reason. `[US2]`
- `FR-008`: `hx doti update --repo <path>` MUST reconcile a repo's **tool-owned managed assets** to the installed tool's payload and state the **before→after** version (`<old> → <new>`), or `already current at <version>`. `[US3]`
- `FR-009`: `update` MUST never modify **operator-owned content** (e.g. `.doti/memory/constitution.md`) — even with `--force` — and MUST report it as preserved. `[US3]`
- `FR-010`: `update` MUST detect a **customized managed asset** by comparing its current content to the managed-asset baseline using **whitespace/EOL-insensitive** content hashing; by default it MUST NOT overwrite a customized managed asset, and MUST report each one (path + "kept"). `[US3]`
- `FR-011`: `update` MUST provide `--force` to overwrite customized managed assets with the new payload, reporting which were force-updated. `[US3]`
- `FR-012`: For a repo lacking a managed-asset hash baseline (older install), `update` MUST still proceed, MUST warn that per-asset customization detection is unavailable for that repo, and MUST write the baseline so detection works on the next update. `[US3]`
- `FR-013`: `update` (and `update-all`) MUST apply changes in an isolated **git worktree**, make the change reviewable, and merge it back into the repo only on acceptance; `--dry-run` MUST show the would-be diff and NOT merge back / mutate the repo. `[US3][US4]`
- `FR-014`: `update` and `update-all` MUST require git; if git is unavailable they MUST fail hard (git is a hard prerequisite). `[US3][US4]`
- `FR-015`: `update` MUST fail closed when `--repo` is not already a Doti repo, directing the operator to `hx doti install`. `[US3]`
- `FR-016`: `hx doti update-all --root <dir>` MUST update every Doti-enabled repo found under the root using the same customization-detection + worktree mechanics per repo, reporting per-repo before→after plus a summary of updated / already-current / customized-skipped / failed counts. `[US4]`
- `FR-017`: `update-all` MUST be fail-soft: a failure updating one repo MUST NOT abort the batch; each repo's outcome (+ reason on failure) is recorded and the overall result reflects any failure. `[US4]`
- `FR-018`: `update-all` MUST support `--dry-run` previewing per-repo changes without mutating. `[US4]`
- `FR-019`: All four commands MUST render in BOTH detailed human output (rich + plain via the shared kernel renderer; `--plain-help` / `HX_HELP_MODE=plain` / `NO_COLOR` honored) and the `CliResult` JSON envelope, with stable `<PREFIX><NNNN>` diagnostic codes and the version/relation/customization data in `data`. `[US1][US2][US3][US4]`
- `FR-020`: The version-relation computation (`current`/`outdated`/`ahead`/`unknown`), the managed-asset customization hash, and the repo-version read MUST be **single-sourced** — one definition each, used by `check-version`, `scan`, `update`, `update-all`, and `hx version --repo` — so behavior cannot diverge between surfaces. `[US1][US2][US3][US4]`

## Success Criteria

- `SC-001`: From any Doti repo, the repo's version + its relation to the installed tool are obtainable in one command, human and JSON.
- `SC-002`: After `hx doti update`, the output states the before→after version (or `already current at <v>`) — confirming the landed version with no second command, directly closing the observed gap.
- `SC-003`: `hx version --repo` reports `current`/`match` (not `newer`) for a Doti-adopted repo whose `payloadVersion` equals the tool — verified on `speckit-nomos`.
- `SC-004`: `hx doti scan --root <dir>` lists every Doti repo under the root with version + relation (N repos → N rows / N JSON entries); zero → explicit empty success.
- `SC-005`: `hx doti update` leaves a hand-edited managed asset unchanged and reports it as customized (no `--force`), and overwrites it with `--force` — operator-owned content (constitution) is unchanged in both cases.
- `SC-006`: Every `update`/`update-all` mutation is applied via a git worktree and is previewable with `--dry-run`; with git absent the command fails hard.
- `SC-007`: `hx doti update-all --root <dir>` brings all outdated repos current and reports updated / already-current / customized-skipped / failed counts; a single repo failure does not stop the others.
- `SC-008`: Every command's JSON envelope validates against `schemas/cli-envelope.schema.json` and carries the version/relation/customization data in `data`.

## Key Entities

- **Repo Doti version stamp** — `.doti/payload.json`: `payloadVersion` + `toolVersion`. The authoritative "what version is this repo on" source.
- **Version relation** — a repo's `payloadVersion` vs the installed tool: `current` / `outdated` / `ahead` / `unknown`.
- **Managed-asset customization status** — per tool-owned asset, whether its current content (whitespace/EOL-insensitive hash) matches the managed-asset baseline (`unmodified`) or diverges (`customized`); operator-owned content is out of this set entirely.
- **Update plan / result** — per repo: before/after version, the set of assets that updated, were kept-as-customized, or were force-updated, the worktree used, and (on failure) the reason.

## Deterministic Surfaces

- **New (planned — advisory until implemented):** `hx doti check-version --repo <path> --json`; `hx doti update --repo <path> [--force] [--dry-run] --json`; `hx doti scan --root <dir> --json`; `hx doti update-all --root <dir> [--force] [--dry-run] --json`.
- **Modified:** `hx version --repo` — `targetRelation` sourced from `.doti/payload.json` (FR-003).
- **Reads:** `.doti/payload.json` (`payloadVersion`, `toolVersion`); `.doti/managed-assets.json` (the canonical managed-asset hash baseline); `.doti/scaffold-version.json` (fallback only).
- **Reuses:** the existing `hx doti install` installer + the canonical managed-asset hash baseline + the modified-asset categories already surfaced by `hx version --repo` (`modifiedWorkflowTemplates` / `modifiedSkillGeneratedInstructions`); the `CliResult` envelope + kernel renderer; `schemas/cli-envelope.schema.json`; `errorcodes/registry.json` (+ `shipped.json`); git (worktrees).

## Architecture Impact

- New logic — version relation, Doti-repo discovery, managed-asset customization hashing, and the worktree-based apply/merge — belongs in a `*.Core` (the Doti command core, e.g. `Hx.Doti.Core` / `Hx.Runner.Core`), single-sourced (FR-020) and consumed by the `hx version` path too. CLI surfaces stay thin (parse → delegate → render); the `cliSurfaceConfinement` / `cliDelegation` ArchUnit families must continue to hold.
- The customization hash MUST reuse the repo's existing canonical (whitespace/EOL-insensitive) managed-asset hashing rather than introduce a second hashing scheme.
- New diagnostic codes appended to `errorcodes/registry.json` (+ `shipped.json`), respecting the append-only stability gate.
- Docs to update so they describe only implemented behavior: `.doti/agent-context.md`, `README.md`, `CHANGELOG.md`, and rendered help/`describe`. `CONTRIBUTING.md` also gains the `feature→dev` (squash) / `dev→main` (merge) branch-flow note set up alongside this cycle.

## Sentrux And Hygiene Impact

- New production code in `*.Core` is measured by Sentrux — keep discovery / relation / hashing / worktree units modular and within function-size limits; an additive, well-structured change is expected to need no baseline change.
- Hygiene: `scan` / `update-all` walk operator-provided paths and worktrees go in temp/`.git` space — output must not leak unexpected local paths beyond what the operator supplied; no secrets introduced.

## Assumptions

- **"Latest doti" = the installed tool's payload version.** `update`/`update-all` reconcile repos to whatever `hx` is installed; updating the *tool* stays the channel's job (`dotnet tool update` / Store / `/doti-upgrade`). *(Confirmed at clarify.)*
- **A "Doti-enabled repo" is a directory containing `.doti/payload.json`.** `scan`/`update-all` discover by that marker; they do not descend into a found repo's `.git`/vendored internals.
- **"Tool-owned managed asset" vs "operator-owned content":** managed assets are the rendered/installed `.doti` payload the tool owns; operator-owned content (the constitution, and anything the install classifies as preserved/operator-authored) is never updated. The boundary reuses the existing install classification + the managed-asset baseline.
- **`update` is `install` made customization-aware and worktree-isolated** — it reuses the install asset set, hook-arming, and constitution-preservation, adding before→after reporting, per-asset customization detection, `--force`, and the worktree apply/merge.
- Version comparison is semantic-version ordering; equal payload and tool version ⇒ `current`.

## Acceptance

- **Command-backed today:** none of the four commands exist yet — every surface here is **advisory/planned** until `/07-doti-implement`. The facts they build on are command-backed: `.doti/payload.json` carries `payloadVersion`/`toolVersion` (verified on `speckit-nomos`), `.doti/managed-assets.json` is the canonical hash baseline, `hx version --repo` already surfaces modified-asset categories from it (and emits the mis-sourced `targetRelation` this cycle fixes), and `hx doti install` performs the asset reconcile.
- **Advisory until implemented:** the `CliResult` JSON proofs, the relation/customization classification, the worktree apply/merge, and the scan/update-all reporting are validated by `/07` tests + `gate run`; treat them as planned, not proven, until then.

## Clarifications

### 2026-06-29

- Q: Does "update to the latest doti" update the tool, or only repos? → A: **Only repos.** `update`/`update-all` reconcile a repo's tool-owned managed assets (templates etc.) to the installed tool; operator-configured content (the constitution) is left alone. Tool updates stay the channel / `/doti-upgrade`.
- Q: How is an operator-customized managed asset handled on update? → A: **Detect it via whitespace-insensitive content hashing, do NOT modify it, and report it as customized to the operator.** Provide `--force` to apply the new managed asset over a customization. If an older repo has no baseline to detect against, that is acceptable for now, but going forward the feature must exist (write the baseline so detection works next time).
- Q: Safety for batch/single mutation? → A: **Provide `--dry-run`, and by default apply the changes in a git worktree, then merge back only once the dry-run is reviewed and accepted.** Git is a hard requirement — fail hard if git is unavailable.
