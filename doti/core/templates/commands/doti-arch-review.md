# doti-arch-review

Purpose: review the architecture implications of scaffold-dotnet changes AND validate that the two architecture engines — ArchUnitNET and Sentrux — are configured to measure the right things and stay mutually consistent.

This skill is seeded with the specific ArchUnitNET and Sentrux items to check so the reviewer does not re-derive them from scratch each run. The architecture and Sentrux gates are command-backed; this skill's cross-engine consistency review is an advisory judgment (no single command validates that the two configs agree yet) — label that review advisory and do not weaken the implemented gates.

## How to run

1. Read `.doti/agent-context.md` and the active spec/plan/tasks.
2. Identify whether the change touches projects, folders, namespaces, layers, attributes, handlers/services/validators/options, or public contracts.
3. Walk the ArchUnitNET checklist, the Sentrux checklist, and the cross-engine consistency checklist below.
4. Report which checks are command-backed vs advisory; never imply a planned command is implemented.

## ArchUnitNET checklist (config: `rules/architecture.json`)

- `rules/architecture.json` is the single config source; tests are config-driven. Reject rule weakening done inline in test code instead of in config.
- All nine rule families exist as visible test groups. The structural families carry negative fixtures (proving the rule fails when violated; `cycle` is positive-only); the capability-confinement, output-confinement, and CLI surface-confinement families carry non-vacuity assertions (proving they are enforced against loaded types, not vacuously true):
  1. **Namespace dependency** — dependency direction is correct. Contracts (`Hx.Tooling.Contracts`) depend on nothing internal; core may depend on contracts; CLI may depend on core + contracts; **core must not depend on CLI**. Generated `dotnet-cli`: `<Solution>` (core) must not depend on `<Solution>.Cli`.
  2. **Class dependency** — forbidden class/interface directions; DTO/contract types stay independent of process, file-system, Git, and CLI types.
  3. **Inheritance naming** — assignable classes (command handlers, services, validators, options) follow naming conventions.
  4. **Class namespace containment** — name-patterned types live in the expected namespace (e.g., `*Command` in the CLI, `Hygiene*`/`Gitleaks*` under their runner-core namespaces).
  5. **Attribute access** — JSON/serialization (and future binding) attributes live on contract/config DTOs, not on CLI command code or process adapters.
  6. **Cycle** — namespace/module slices are acyclic.
  7. **Security architecture (capability confinement)** — the domain/library layer must not depend on dangerous capabilities (process execution, networking, dynamic code generation); those belong in the CLI or dedicated adapters. The capability assemblies are loaded so the rule is enforced, not vacuously true. Complements the code-level analyzer security rules (CA3xxx/CA5xxx) enabled in `Directory.Build.props`.
  8. **Output confinement (agent-first)** — only the `Agent` host writes to the console; command logic returns the `CliResult` envelope (JSON-first), so output stays one machine-consumable chokepoint. A non-vacuity assertion proves the `Agent` actually reaches `System.Console`.
  9. **CLI surface confinement (Channel Independence / thin adapter)** — types in a `.Cli` namespace carry no business-logic roles (`*Service`/`*Repository`/`*Validator`/`*Calculator`/`*Engine`/`*Manager`/`*Scanner`/`*Provider`); those live in the domain library, so the CLI stays a thin channel adapter and the core is reusable from any channel. A non-vacuity assertion proves a `*Service` resides in the library, not the CLI.
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

Run `/doti-implement` to implement the tasks.
