# doti-arch-review

Purpose: review the architecture implications of scaffold-dotnet changes AND validate that the two architecture engines — ArchUnitNET and Sentrux — are configured to measure the right things and stay mutually consistent.

This skill is seeded with the specific ArchUnitNET and Sentrux items to check so the reviewer does not re-derive them from scratch each run. The architecture and Sentrux gates are command-backed; this skill's cross-engine consistency review is an advisory judgment (no single command validates that the two configs agree yet) — label that review advisory and do not weaken the implemented gates.

## How to run

1. Read `.doti/agent-context.md` and the active spec/plan/tasks.
2. Identify whether the change touches projects, folders, namespaces, layers, attributes, handlers/services/validators/options, or public contracts.
3. Walk the ArchUnitNET checklist, the Sentrux checklist, and the cross-engine consistency checklist below.
4. Report which checks are command-backed vs advisory; never imply a planned command is implemented.

## ArchUnitNET checklist (config: `rules/architecture.json`)

- `rules/architecture.json` is the single config source for ArchUnitNET family ids, and `architecture test --json` must report the same ids/count. Reject guidance that claims families the command-backed proof does not report.
- In speckit-doti, the ArchUnitNET families currently dog-food Channel Independence:
  1. **`cliSurfaceConfinement`** — types in a `.Cli` namespace carry no business-logic roles (`*Service`/`*Repository`/`*Validator`/`*Calculator`/`*Engine`/`*Manager`/`*Scanner`/`*Provider`); those live in the domain/core library, so the CLI stays a thin channel adapter.
  2. **`cliDelegation`** — command-dispatch types delegate to a `*.Core` library rather than containing the logic themselves, so the core is drivable from any channel.
- If a change adds more ArchUnitNET families, update `rules/architecture.json`, the ArchUnitNET tests, `architecture test --json` proof expectations, and this guidance in the same change.
- The README states ArchUnitNET rules are default gates, not examples.
- The shared architecture loader loads the generated library + CLI assemblies once per run.
- Any new project/namespace/layer/attribute/handler/service/validator/options class is reflected in `rules/architecture.json`.

## Sentrux checklist (config: `.sentrux/rules.toml`, policy: `rules/sentrux.json`, baseline: `.sentrux/baseline.json`)

- `.sentrux/rules.toml` `[[layers]]` point at folders that actually exist and use the intended `order` (lower order = lower-level): scaffold repo contracts(0) → core(1) → cli(2); generated `dotnet-cli` core(0) → cli(1).
- `[[boundaries]]` forbid the wrong-direction edges (core must not depend on cli; contracts depend on nothing internal) and match the ArchUnitNET namespace direction.
- `[constraints]` are meaningful, not placeholders: `max_cycles = 0`, plus `max_cc` / `max_fn_lines` / `no_god_files` tuned to real intent (not so loose they never fire, nor so strict they block legitimate structure).
- `rules/sentrux.json` (runner policy) and the rendered `.sentrux/rules.toml` are consistent — the native config is derived from the policy, not hand-diverged.
- Baseline lifecycle: `.sentrux/baseline.json` is created only at first smoke (`gate --save`), normal gates `gate`-compare, and the gate fails closed when the baseline is missing, stale, or produced by a non-fork/wrong-version Sentrux. No silent re-baselining.
- Required tool features are verified: `sentrux check --include-untracked`, `sentrux gate --save`, the `Heurex fork` version stamp, and the C# grammar provisioned for offline determinism (`SENTRUX_SKIP_GRAMMAR_DOWNLOAD` + vendored grammars).
- Scope is right: `check --include-untracked` covers untracked/new/staged files; template content under `scaffold/templates/` is measured appropriately; the quality signal is computed on the intended graph (vendored binaries/excluded dirs do not skew it).

## Cross-engine consistency

- ArchUnitNET (assembly/namespace assertions) and Sentrux (whole-graph quality + path layers/boundaries) must encode the **same** intended architecture. A dependency forbidden in one must not be allowed in the other.
- When the project graph changes (new project, folder, namespace, layer), update `rules/architecture.json` and `.sentrux/rules.toml` in the same change.
- "Measuring the right things" means: layers/paths resolve to real folders, rules reflect the real intended constraints, and both engines agree on direction and cycle policy.

## Codification note

This checklist is intentionally codified in the skill so the reviewer does not spend context rediscovering it. It should later become a command-backed validator (for example a runner check that validates `rules/architecture.json` and `.sentrux/rules.toml` against the actual project graph and for mutual consistency), composed with `architecture test` and `sentrux verify`/`check` (both implemented). Until that composed validator exists, this review is advisory.

## Command availability

`architecture test`, `sentrux verify`/`check`, `gate run`, and hygiene are command-backed and implemented. The advisory part is this skill's cross-engine consistency review — no single command validates that the ArchUnitNET and Sentrux configs agree yet, so label that review advisory.

## Next

Run `/07-doti-implement` to implement the tasks.
