# 029 — Scaffold & doti setup configuration (`--config` / interactive / `config show`)

## Summary

`hx new` parameterizes only two tokens (`--name`, `--company`) and `hx doti install` four flags; everything else a new repo needs is hand-edited across a dozen files or known only as tribal post-scaffold steps (the inventory in [`docs/configuration.md`](../configuration.md) and [`docs/design/doti-setup-config-inventory.md`](../design/doti-setup-config-inventory.md) records ~48 configurables and 30 manual steps). This feature lets the operator supply the configuration **once** — as a JSON file (`--config`, agent-facing) or an interactive wizard (`--interactive`, human-facing) — projects it into the generated repo, persists it to a tracked `.doti/setup.json`, and surfaces the effective configuration with **default-vs-custom provenance** via `hx doti config show`. Scope is the **operator-configurable subset** only; derived, auto-set, and payload-fixed values stay out.

## Motivation

The configuration surface is split across three disconnected places — a few CLI flags, a dozen tracked config files, and machine/GitHub-side state in no file at all — so standing up a releasable repo is a guessing game even for an experienced operator. There is no single input that captures intent and no way to *see* what was set versus defaulted. This feature gives both paths one schema and one visibility surface, eliminating the file-level manual steps `hx` can safely own while honestly documenting the GitHub/nuget-side steps it cannot.

## Priority Mode

**Tooling / CLI change → fail-closed safety + deterministic proof before ergonomics.** The agent-facing deterministic path (the provenance-tracked config model + `--config` + `config show`) is the MVP; the human wizard and the setup automation layer on top. Slices are independently testable user-value increments.

## Prioritized work items

- **US1 (P1, MVP) — Agent-facing config + visibility.** The provenance-tracked config model, `hx new --config <json>`, persistence to `.doti/setup.json`, and `hx doti config show [--json]`. *Independent test:* `hx new --config x.json` yields a repo whose metadata matches `x.json`, and `hx doti config show --json` echoes `{value, source, default}` per key. Delivers value on its own (agents get a single deterministic input + audit).
- **US2 (P2) — Human interactive wizard.** `hx new --interactive` grouped prose Q&A that writes the same `.doti/setup.json` then runs the `--config` codepath, with an end-of-setup Review display. *Independent test:* a scripted answer set produces the same repo + `setup.json` as the equivalent `--config` file.
- **US3 (P3) — Project-file projections.** Eliminate the file-level manual steps `hx` can safely own from config: `GitVersion.yml` next-version, `<Description>`/`<RepositoryUrl>`/`<PackageLicenseExpression>`/`<Authors>`, and constitution §2. *Independent test:* each value lands in its target file; an omitted value keeps the template default.
- **US4 (P4) — Setup checklist (operator-intent).** A printed checklist naming every remaining setup step `hx` does not perform — both the operator-only steps (NuGet OIDC policy, repo secret, Environment, branch protection, tag push) and the git/CI steps deferred to 030 (baseline commit, `dev`/`main`, `.github/workflows` emission, DCO hook). *Documentation only; the git/CI automation itself is out of scope this cycle (C1 → deferred to 030).*

## Scope

**In:** the operator-configurable subset — identity (`name`, `company`, `output`, `description`, `authors`, `repositoryUrl`, `license`), versioning (`nextVersion`), release local output (`directory`/`environmentVariable`/`enabled`), publish intent (NuGet `owner`/`repo`/`workflow`/`environment`/`target`), `agents`, and constitution §2; both input paths (`--config`, `--interactive`) on **both `hx new` and `hx doti install`** (install consumes the applicable subset); `hx doti config show` with provenance; the `.doti/setup.json` persisted intent; the US3 project-file projections.

**Out:** Sentrux configuration (baseline is auto-set by the first-smoke run; layers/namespaces auto-render from name/layout); all derived values (Copyright, PackageId, namespaces, `release.json` paths, executable names); payload-fixed config (gate-profile maps, `prerequisites.json`, schemaVersions, §1 invariants); **executing** any operator-only GitHub/nuget-side step; adding a third agent flavor (needs code, not config).

## Functional requirements

- **FR-001 — Provenance-tracked config model. [US1]** The system MUST resolve configuration into an object where every operator-configurable key carries `value`, `source` (∈ `default` | `config-file` | `interactive` | `flag` | `derived`), and the `default` it would otherwise take. This model MUST be the single source consumed by both input paths and the show surface (no second resolution path).
- **FR-002 — `--config <path>` agent input. [US1]** `hx new --config <doti-setup.json>` and `hx doti install --config <doti-setup.json>` MUST read and schema-validate the file (fail closed on `schemaVersion` ≠ 1, unknown fields, or invalid values), resolve it against defaults (recording source per FR-001), and project each **applicable** field into its target — `hx new`: template tokens, `.doti/release.json`, `GitVersion.yml`, `Directory.Build.props`/`.csproj` metadata, constitution §2; `hx doti install`: the doti-layer + release/publish/versioning subset, never regenerating existing projects, reporting new-only fields as ignored. Explicit flags (`--name`/`--company`/`--output`/`--agents`) MUST override the matching config field.
- **FR-003 — Persist intent to `.doti/setup.json`. [US1]** The operator-supplied intent (not the derived/default fill) MUST be written to a tracked `.doti/setup.json` so re-runs (`hx doti install`, upgrades) read the same intent. The projection MUST be idempotent: re-running over the produced repo yields no spurious diffs and preserves operator edits under the existing managed-asset reconciliation (`.new` sidecar / `--force`).
- **FR-004 — `hx doti config show [--json]` with provenance. [US1]** The system MUST resolve the effective configuration from `.doti/setup.json` (the persisted operator intent) overlaid on documented defaults — persisted-only, not a live repo-file diff (C4) — and render either JSON (`{ key: { value, source, default } }`, grouped) or a human table (grouped by what each setting drives, a default-vs-custom column, and a `N custom · M default` footer). The command MUST be non-mutating.
- **FR-005 — `--interactive` human wizard. [US2]** `hx new --interactive` (and `hx doti install --interactive`, asking only the install-applicable subset) MUST ask grouped prose questions (identity, agents, versioning, publish, constitution §2, …), each mapping 1:1 to a config key, as a **lighter setup flow** — prompt + default + validation + a one-line "what it affects" — NOT the Operator-Question Protocol (which stays reserved for blocking cycle decisions; C2). It MUST support conditional branches (publish = yes → NuGet sub-questions), write the resolved `.doti/setup.json` then run the `--config` codepath (identical projection), refuse when `--config` is also passed, and end with a Review step that displays the effective config (FR-004 human render) for confirmation before any file is written or committed.
- **FR-006 — Project-file projections. [US3]** From the config, `hx new` MUST set `GitVersion.yml` `next-version` (from `versioning.nextVersion`), `<Description>`/`<RepositoryUrl>`/`<PackageLicenseExpression>`/`<Authors>` (from `identity.*`), and the constitution §2 sections (from `constitution.*`, written verbatim into `.doti/memory/constitution.md`, never clobbering an already-authored §2). Gate-rule localization (`.sentrux` layers, `rules/architecture.json`, the ArchUnit test code) is NOT config-driven — the template's `sourceName` substitution already localizes it in lockstep (C5).
- **FR-007 — Setup steps surfaced as a checklist, never executed. [US4]** The config MAY carry NuGet publish parameters and branch-protection intent; `hx` MUST NOT execute the nuget.org OIDC policy, the `NUGET_USER` secret, the GitHub Environment, branch protection, or the `v*` tag push. It MUST print an operator checklist naming exactly those steps **plus** the git/CI steps deferred to 030 (baseline commit, `dev`/`main`, `.github/workflows` emission, DCO hook). The git/CI **automation** is out of scope this cycle (C1 → 030).
- **FR-008 — Existing flags + callers survive. [US1]** `--name`/`--company`/`--output`/`--agents`/`--profile`/`--force`/`--json` MUST keep their current behavior and become the high-priority overrides above `--config`. A `hx new` invocation with neither `--config` nor `--interactive` MUST behave exactly as today (no breaking change).
- **FR-009 — Schema-validated rejection of non-config. [US1]** The schema MUST reject values that are not configurable (e.g. an agent flavor outside `{claude, codex}`, a `schemaVersion` ≠ 1) with a clear validation error naming the offending field, rather than silently accepting or ignoring it. A validation failure MUST leave no partial repo.
- **FR-010 — Host commands + library placement. [US1]** `--config`/`--interactive` are wired onto **both `hx new` and `hx doti install`** (C3); `hx doti config show` is standalone (any repo with a `.doti/setup.json`). One schema, each field carrying an applies-to scope: `install` consumes the doti-layer + release/publish/versioning subset and uses `identity.name` as the integration name only (never regenerating an existing repo's projects); new-only fields are reported as ignored on the `install` path. The config model + resolver MUST live in a `*.Core` library (git-free, unit-testable); the CLI MUST stay parse → delegate → render.

## Success criteria

- **SC-001** — `hx new --config sample.json` produces a repo whose package metadata (name, company, description, repository URL, license, version seed) matches `sample.json`; a field omitted from `sample.json` takes its documented default.
- **SC-002** — `hx doti config show --json` returns, for every operator-configurable key, `{ value, source, default }`; an operator-supplied value reads `source != default`, an omitted value reads `source == default` and `value == default`.
- **SC-003** — `hx doti config show` (human) groups keys by what they drive, marks each default vs custom, and prints a custom/default count.
- **SC-004** — A scripted `hx new --interactive` answer set produces the same repo + `.doti/setup.json` as the equivalent `--config` file; passing both `--config` and `--interactive` fails with a clear error.
- **SC-005** — Re-running `hx new --config` / `hx doti install` over the produced repo is idempotent (no spurious diffs) and preserves operator edits.
- **SC-006** — A config with an invalid value (e.g. `agents: ["gemini"]`, `schemaVersion: 2`) fails closed with a validation error naming the offending field, leaving no partial repo.
- **SC-007** — `hx new` with neither `--config` nor `--interactive` behaves identically to today (regression locked).
- **SC-008** — The operator-only steps (NuGet policy/secret/Environment, branch protection, tag push) are never executed by `hx`; they appear in a printed checklist instead.

## Clarifications

### Session 2026-06-30

- **C1 (scope) → Defer git/CI automation to 030.** This cycle scopes to config-capture (`--config`/`--interactive`), `config show`, the project-file projections (GitVersion seed, `.csproj` metadata, constitution §2), and the operator-intent **checklist**. The git/CI **setup automation** — baseline sanctioned commit, `dev`/`main` creation, `.github/workflows/{release,ci,dco}.yml` emission, the DCO `prepare-commit-msg` hook — is **deferred to a follow-up feature (030)**. US4 is reduced to the checklist only.
- **C2 (UX) → Lighter setup wizard.** `--interactive` is a lighter flow (prompt + default + validation + a one-line "what it affects"), NOT the Operator-Question Protocol, which stays reserved for blocking cycle decisions.
- **C3 (scope) → Both `hx new` and `hx doti install` now.** `--config`/`--interactive` are wired onto both this cycle. One schema; each field carries an **applies-to** scope. `hx new` consumes the full subset (project generation + doti layer). `hx doti install` consumes the **doti-layer + release/publish/versioning subset** (agents, tier, constitution §2, `release.json` fields, publish intent, version seed, local-output) and uses `identity.name` only as the integration/display name — it **never regenerates or renames** an existing repo's projects. New-only fields (e.g. `output`, project scaffolding) are inapplicable to `install` and **reported as ignored**, not silently dropped. `hx doti config show` is standalone (any repo with a `.doti/setup.json`).
- **C4 (UX/scope) → Persisted-only.** `config show` reads `.doti/setup.json` + documented defaults (the intent view, satisfying R2); a `--live` repo-file diff is deferred (config-drift stays with `/08` + `payload check`).
- **C5 (scope/technical) → Leave gate rules to template substitution.** Verified: the template's `sourceName` (`HxScaffoldSample`) is substituted across `.sentrux/rules.toml`, `rules/architecture.json`, and the ArchUnit test code, so `hx new` already localizes the gate rules and the architecture↔test coupling in lockstep. No config-driven gate-rule rendering this cycle.

## Deterministic surfaces

- `hx new --config <path>` / `hx new --interactive` — *planned, absent today (advisory).*
- `hx doti config show [--json]` — *planned, absent today (advisory).*
- `.doti/setup.json` — the persisted operator intent (tracked) — *planned.*
- The `CliResult` JSON envelope for `config show` (the `{value, source, default}` per-key object) — the machine contract agents read.

## Architecture / Sentrux / hygiene impact

- New config model + resolver as a single-responsibility `*.Core` type (candidate: `Hx.Scaffold.Core` for the model, with the resolver pure/git-free); the CLI surfaces stay thin (parse → delegate → render) per the `cliSurfaceConfinement`/`cliDelegation` ArchUnit families.
- `config show` is a read-only runner command; no gate is downgraded, no enforced→advisory change.
- Schema validation is fail-closed (consistent with `release.json`/`hx.config.json` `schemaVersion` guards).
- No new external dependency anticipated (JSON via the existing serializer); keep new methods within the Sentrux function-size limit.

## Assumptions

- The feature targets the **operator subset** confirmed in `docs/design/doti-setup-config-inventory.md` §9; Sentrux/derived/payload-fixed values are out by design (operator-confirmed: the smoke run sets the Sentrux baseline; nothing operator-facing).
- `.doti/setup.json` is the config home (tracked, repo-owned), mirroring the other `.doti/*.json` manifests.
- The README Configuration section + `docs/configuration.md` already exist (committed this session) and become this feature's documentation baseline; their "How" column updates from "edit `.csproj`" to "`--config`/wizard" as the projections land.
- Default values are exactly those documented in `docs/configuration.md` (code-verified).

## Out of scope

- Executing operator-only GitHub/nuget-side steps (NuGet OIDC policy, repo secret, Environment, branch protection, tag push) — documented as intent + checklist only.
- **Git/CI setup automation** (baseline commit, `dev`/`main` creation, `.github/workflows` emission, DCO `prepare-commit-msg` hook) — deferred to a follow-up feature (030); surfaced as a checklist only this cycle (C1).
- Sentrux baseline/layers and any derived or payload-fixed value.
- A third agent flavor (requires a render template + code).
- Multi-template selection beyond the existing `dotnet-cli` profile.

## Design direction (for `/04-doti-arch-review`)

- Resolve config in two stages — layer inputs (flag ▸ `--config`/interactive ▸ derived ▸ default) recording each key's winning `source`, then project the resolved-with-provenance object into target files — so provenance is first-class, not reconstructed (R2).
- Keep the wizard a thin front-end that emits `.doti/setup.json` and re-enters the `--config` path, so the two inputs are provably 1:1 (one projection codepath, tested once).
- Place the model/resolver in `*.Core`; map config keys → target files through a single declarative projection table (so a new key is one table row, not scattered edits) — the same single-source-of-truth discipline 028 applied to action affordances.
- `config show` renders from the resolved model; the human table reuses the grouping in `docs/configuration.md`.
