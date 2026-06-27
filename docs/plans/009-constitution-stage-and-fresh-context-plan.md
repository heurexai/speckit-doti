# 009 — Constitution Stage and Always-Fresh Context — Plan

## Summary

Add a project **constitution** as a first-class, always-fresh context. Concretely: a new unnumbered `doti-constitution` skill authors a **§1/§2** constitution (§1 = inherited doti invariants *cited*, §2 = the only operator-fillable declarations + an auto-filled project-name title); a command-backed `hx doti constitution` emits the current §2 and is **codified** into the plan + arch-review context steps so the agent always reviews against fresh §2 (delivery code-enforced, **evaluation agent-judged**); the §1/§2 template ships to `hx new` repos with the generated repo's constitution **template-initialized** (not copied from this repo's); and the advisory drift finder is **.NET-tuned** (member chunking, code-aware instruction, recalibrated thresholds) and documented as an on-demand, code↔docs-only agent tool. The technical approach reuses the established single-sourced-skill + thin-CLI→`*.Core` + managed-asset-preservation patterns; the only genuinely new build is the .NET calibration for WI-5.

## Technical Context

- **Stack/patterns:** Doti-prose assets are single-sourced in `.doti/core/**`, rendered by `DotiRenderer`/`SkillMarkdownRenderer`, gated by `doti render-skills --check` + `doti payload check`. CLI commands are thin (`RunnerCommands.*` → a `*.Core` type → `CliResult`), enforced by the `cliSurfaceConfinement`/`cliDelegation` ArchUnit families. The semantic stack (`Hx.Embedding.Core` zero-Hx-deps; `Hx.Semantic.Core`) is firewalled from Gate/Cycle by the 008 `FR-020` ArchUnit rule.
- **Verified mechanic (load-bearing for WI-3):** the payload glob (`Hx.Scaffold.Cli.csproj:93`) ships `.doti/memory/constitution.md` (this repo's, excluding only cycle-state/gate-proof/sentrux-log/templates), and `DotiInstaller.StaticDotiSubdirectories` reconciles `memory/` — so a generated repo today inherits **this repo's** constitution (verified: `.doti/memory/constitution.md` ≡ `.doti/core/memory/constitution.md`). The design must exclude it from the payload and **initialize from the template** instead.
- **Constitution location:** `.doti/memory/constitution.md` is a Doti asset → its read/emit/init logic belongs in `Hx.Doti.Core` (with `DotiRenderer`/`DotiInstaller`), not `Hx.Cycle.Core`.
- No `[NEEDS CLARIFICATION]` — all 16 FRs are decided (clarify sessions in the spec).

## Constitution Check (gate)

Verdict against `.doti/memory/constitution.md` — the **9** principles (the plan-template's list is stale at 7; refreshing it is a WI-4 side-fix). Evaluated before design:

- **Deterministic Ownership / Bootstrap Honesty** — PASS. §2 is **agent-evaluated advisory** and is never reported as gate proof (FR-012); the deterministic gate is unchanged; the finder is advisory/never-gating. Honesty preserved: no false "enforced" claim.
- **Template Boundary** — PASS. The constitution *template* is a Doti asset; the scaffold CLI owns *initialization* (fill name, materialize) — the boundary the constitution sits across.
- **Public Hygiene** — PASS. No machine paths (project name auto-derived, FR-015; the template carries no operator-local content).
- **Cross-Platform** — PASS. The member chunker is .NET; no shell runners.
- **Codified Cycle** — PASS. The constitution is a **project artifact, not a per-cycle stamp** (FR-003); the *delivery* of §2 is codified (FR-007/008) without making the constitution a fail-closed gate.
- **Channel Independence** — PASS. New behavior lands in `*.Core` types (`ConstitutionService`, `ConstitutionInitializer`, `ProjectNameResolver`, the chunker); CLI deltas are wiring-only.
- **Engineering Discipline / Operator Decisions** — PASS.

Re-evaluate after design: no violation introduced (Complexity Tracking empty).

## Research (resolve unknowns)

**R1 — §2 chunking for the .NET-tuned finder (WI-5).**
- **Decision:** a lightweight, dependency-free **member chunker** in `Hx.Embedding.Core` (split `.cs` on type/member declaration boundaries by brace-depth + declaration heuristics) producing one chunk per type/member.
- **Rationale:** `Hx.Embedding.Core` has **zero Hx deps** (008 FR-040) and must stay portable; Roslyn (`Microsoft.CodeAnalysis`) is a heavy dep for a recall-favouring advisory finder that only needs approximate member boundaries.
- **Alternatives rejected:** Roslyn (accurate but a large dependency in the zero-dep embedding lib — disproportionate for advisory chunking; revisit only if calibration shows member boundaries are too noisy); reusing Sentrux's tree-sitter grammars (they drive the Sentrux binary, not a C#-callable API).

**R2 — Code-aware embedding instruction.**
- **Decision:** use the Qwen3 instruction prefix (today unused on the symmetric drift path, `Qwen3Embedder.cs:79`) with a code/.NET-oriented instruction for drift embeddings; BGE-M3 (no instruction) keeps its symmetric form.
- **Rationale:** the instruction is the engine's documented lever for domain steering; it costs nothing and is reversible.
- **Alternatives rejected:** a second fine-tuned model (out of scope; the engines are pinned/hash-verified).

**R3 — .NET calibration gold set (the real new artifact).**
- **Decision:** assemble a small **.NET code↔doc gold set** (labelled drift / no-drift pairs — e.g. renamed method vs stale XML-doc summary) and recalibrate `Thresholds.Default` per engine, recorded in `docs/plans/hx-semantic-calibration.md` (the existing calibration doc).
- **Rationale:** `Thresholds.cs` itself flags the current values as domain-sensitive ("recalibrate per domain"); a .NET gold set is the only way to set honest thresholds.
- **Alternatives rejected:** reusing the Wikipedia/PAWS/STS-B thresholds (wrong domain — the whole point of WI-5).

**R4 — Codified injection mechanism (WI-2).**
- **Decision:** `hx doti review-context` **composes** the constitution §2 into its output (the runner wires `ReviewContextProjector` + `ConstitutionService`), so arch-review (which already runs review-context) gets §2 automatically; the plan skill invokes `hx doti constitution` directly as its codified Constitution-Check step.
- **Rationale:** strongest codification available in the markdown-skill model — the agent can't obtain arch-review context without §2; composition lives in the **runner** (CLI layer), keeping `Hx.Cycle.Core ↛ Hx.Doti.Core` clean.
- **Alternatives rejected:** a read-the-file step (clarify-rejected, not codified); embedding the constitution logic inside `Hx.Cycle.Core` (would create a Cycle→Doti core edge).

**R5 — Generated-repo constitution initialization (WI-3).**
- **Decision:** **exclude** `.doti/memory/constitution.md` from the payload glob (like `cycle-state.json`); the installer **initializes** the target's constitution from `.doti/core/templates/constitution-template.md` with the name filled (FR-015) when absent, and **preserves** an operator-edited one (existing managed-asset preservation).
- **Rationale:** a generated repo must get *its own* constitution, not speckit-doti's (the verified current bug); mirrors the per-repo-artifact pattern already used for cycle state.
- **Alternatives rejected:** shipping this repo's constitution as the default (the bug); a `[PROJECT_NAME]` placeholder (clarify-rejected — auto-derived).

## Design

**Selection rule applied:** simplest correct + modular, reusing the single-sourced-skill, thin-CLI→`*.Core`, and managed-asset patterns. New `*.Core` types, one concern each; CLI wiring-only.

**WI-1 — `doti-constitution` skill (Doti-prose).** Add the entry to `.doti/core/skills.json` (unnumbered, beside `doti-amend`/`doti-drift-fix`) + a `.doti/core/templates/commands/doti-constitution.md` template; re-render. Same proven path as the 008 utility skills.

**WI-2 — fresh §2 injection.**
- `Hx.Doti.Core.ConstitutionService` (NEW, single responsibility): `Read(repoRoot)` → the constitution + a parsed **§2** projection + an `Exists` flag; emits absence cleanly (no throw — surface-and-proceed).
- `hx doti constitution` (NEW runner command `RunnerCommands.Doti.Constitution.cs` + factory wiring) → delegates to `ConstitutionService`, returns `CliResult` (Ok with §2 + absence note).
- `RunnerCommands.Doti.ReviewContext` composes `ConstitutionService` so review-context's output carries §2 (codified for arch-review). The plan template invokes `hx doti constitution` as its Constitution-Check step.

**WI-3 — template + this repo + scaffold init.**
- Restructure `.doti/core/templates/constitution-template.md` → auto-filled title + §1 (cited invariants) + §2 (placeholders only). Restructure `.doti/core/memory/constitution.md` (+ the `.doti/memory/` copy) → 9 §1 principles + explicit §2 (no placeholders).
- `Hx.Doti.Core.ProjectNameResolver` (NEW): `--name` → solution (`.slnx`/`.sln`) name → repo dir name. `Hx.Doti.Core.ConstitutionInitializer` (NEW): initialize-from-template (name-filled) if absent, preserve if present. Payload glob excludes `.doti/memory/constitution.md`; `hx new` + `doti install` call the initializer.

**WI-4 — arch-review consumes §2 (Doti-prose).** Edit `doti-arch-review.md` to evaluate the change against the codified §2 (tech-stack/coding-style) and cite §2 in findings (agent-evaluated). Side-fix: refresh `plan-template.md`'s stale 7-principle list to cite §1/§2 by reference.

**WI-5 — .NET-tuned finder + tool docs.** `Hx.Embedding.Core`: the member chunker + the code-aware Qwen3 instruction. `Hx.Semantic.Core.DriftCandidateService`: chunk `.cs` by member (was whole-file). `Thresholds`: recalibrated per R3. Document `hx doti drift-candidates` as an on-demand advisory tool (agent-context + skills) with match-type guidance (FR-014), code↔docs-only.

**Architecture delta.**
- New `*.Core` types: `Hx.Doti.Core` — `ConstitutionService`, `ConstitutionInitializer`, `ProjectNameResolver`; `Hx.Embedding.Core` — a `CSharpMemberChunker` (`internal`); no new project.
- ArchUnit: `cliSurfaceConfinement`/`cliDelegation` already cover the new command (RunnerCommands → `*.Core`); the BL-5 suffixes (`*Resolver`/`*Service`) are already confined. The 008 `Gate/Cycle ↛ Semantic` rule MUST still hold (WI-5 touches only Embedding/Semantic). No `rules/architecture.json` family change needed; no `.sentrux/rules.toml` layer change (constitution + templates are Sentrux-excluded prose, 008 FR-029).
- The only *behavioral* boundary to watch: review-context's composition of `ConstitutionService` happens in the **runner**, not in `Hx.Cycle.Core` — preserving the no-Cycle→Doti-core edge.

## CLI surface & error contract

New command **`hx doti constitution`** (also surfaced through `hx` via the runner-doti composition):
- **Error codes:** none new on the happy/absent paths (absence is a surfaced Ok note, FR-016). A malformed/unreadable constitution reuses `Validation_Failed`. Register only if a genuinely new diagnostic appears at build.
- **Exit class:** Success (present or absent); Validation (malformed).
- **`describe` entry:** `doti constitution` (+ `--repo`, `--json`, optional `--section §2|full`).
- **Envelope:** `CliResult`, JSON-first; the data carries the §2 content + `exists` flag.
- **Channel boundary:** delegates to `Hx.Doti.Core.ConstitutionService`; CLI is parse→delegate→render (cliDelegation).

## Command Availability

| Area | Command | Status |
| --- | --- | --- |
| Constitution emit | `hx doti constitution --repo . --json` | **planned (WI-2)** — advisory until built |
| Review context (+§2) | `hx doti review-context --repo . --json` | implemented (007/008); **§2 composition planned (WI-2)** |
| Drift finder (.NET-tuned) | `hx doti drift-candidates --repo . --json` | implemented (008); **.NET tuning planned (WI-5)**, advisory/never-gating |
| Render / parity | `doti render-skills --check`, `doti payload check` | implemented — gate the new skill + template |
| Gate | `hx gate run --profile normal --json` | implemented |

No planned gate is downgraded; §2 evaluation is advisory by design (FR-012), not a new gate step.

## Complexity Tracking

(Empty — the Constitution Check surfaced no violation.)

## Risks

- **WI-5 calibration (highest):** an honest .NET threshold needs a real labelled gold set; without it, tuning is guesswork. Mitigation: R3's gold set is a first-class task; if member-chunking proves too noisy, Roslyn is the fallback (R1).
- **Generated-repo constitution regression:** excluding `.doti/memory/constitution.md` from the payload must not break this repo's own constitution (it stays on disk, just unshipped) or the install of *other* `memory/` assets — covered by `doti payload check` + a generated-repo install test (SC-002/006).
- **Codified-injection drift:** review-context composing `ConstitutionService` must not create a `Cycle→Doti` core edge — the composition is in the runner; an ArchUnit assertion should pin it.
- **Scope:** 5 WIs in one cycle (operator's decision); the constitution work (WI1–4) and the ML tuning (WI5) are independent — tasks should phase them so WI5's calibration can't block WI1–4.
- **Plan-template staleness:** the 7-vs-9 principle drift is itself a small correctness risk the feature fixes (WI-4 side-fix).
