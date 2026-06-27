# 009 — Constitution Stage and Always-Fresh Context

## Goal

speckit-doti dropped the **constitution** that the original Spec Kit puts at the very front of the workflow: the project's non-negotiable principles, declared tech stack, and coding style. Today there is no `doti-constitution` skill, the workflow starts at `specify`, and our `constitution-template.md` is a thin prose list — not the structured, fillable template Spec Kit ships (`docs/specs` evidence: `.doti/core/skills.json` has no `constitution` entry; `.doti/core/workflows/doti/workflow.yml` starts at `specify`; `.doti/core/templates/constitution-template.md` is an 8-line bullet list vs Spec Kit's placeholder-driven `[PROJECT_NAME]`/`[PRINCIPLE_N]`/governance/version template at `D:\temp\spec-kit\templates\constitution-template.md`).

The user-facing outcome: a project (this repo **and** every `hx new`-generated repo) can author a real constitution as the first workflow step, and that constitution is **re-injected fresh** into `/03-doti-plan` and `/06-doti-arch-review` — so the agent reviews against the *current* declared principles, tech stack, and coding style, not a stale path reference or generic lenses. This matters because the plan template already gates on `.doti/memory/constitution.md` (`plan-template.md:13-15`) but nothing keeps that content in the agent's working context, and the arch-review template references the constitution **nowhere** (`doti-arch-review.md` grep: 0 hits) — so project-specific conventions silently never reach the design review.

## User Scenarios & Testing

**Priority Mode — workflow / tooling (dominant), with docs/Doti-prose and generated-code exceptions.** The headline deliverable is a new workflow stage + a fail-closed/deterministic freshness guarantee, so the order is **safety + deterministic proof before ergonomics**. Exceptions: the constitution template + arch-review template edits are Doti-prose (truth-first); shipping the template into `hx new` is a generated-code/scaffold concern.

### Work Item 1 — The constitution stage exists and authors a real constitution (Priority: P1)

A project lead runs the **unnumbered** `doti-constitution` skill — a utility skill alongside `doti-bug`/`converge`/`doti-amend`/`doti-drift-fix`, run ahead of and independent of the numbered `/01`–`/09` cycle — and authors `.doti/memory/constitution.md` from the **§1/§2** template: §1 cites the inherited doti invariants (read-only reference), and the lead fills only **§2** — `[DOMAIN_PRINCIPLES]`, `[TECH_STACK]`, `[CODING_STYLE]`, `[SECURITY_COMPLIANCE]`, `[PERFORMANCE]`. The constitution is a **project-level artifact** authored once and amended occasionally — it is NOT a numbered cycle stage and is NOT stamped per feature cycle.

- **Why this priority:** it is the missing foundation everything else consumes; without the skill there is no authored §2 to inject or enforce.
- **Independent Test:** on a repo with no constitution, run the skill; verify `.doti/memory/constitution.md` exists, §2 declares the tech stack + coding style, §1 is present as a fixed reference, and there are **no unfilled `[PLACEHOLDER]` tokens**.
- **Acceptance Scenarios:**
  1. **Given** a repo with the thin legacy constitution, **When** the skill runs, **Then** it produces a §1/§2 constitution (inherited invariants cited + filled project declarations) without losing this repo's existing §1 principles.
  2. **Given** an existing constitution, **When** an amendment is made, **Then** the change is captured by the doti cycle + git (no hand-maintained SemVer doc-version line).

### Work Item 2 — The constitution is re-injected fresh at plan and arch-review (Priority: P2)

When `/03-doti-plan` or `/06-doti-arch-review` runs, the **current** §2 is delivered into the working context as the **output of the stage's command-backed context step** — codified (enforced by code, not convention; folded into the command the stage already runs), so an amendment made minutes earlier is reflected immediately and the agent cannot silently skip it. The *delivery* is codified; the *evaluation* of the change against §2 stays agent-judged.

- **Why this priority:** "always fresh" is the load-bearing correctness guarantee the user asked for; a stale or skipped constitution makes the Constitution Check and the arch-review review against the wrong rules.
- **Independent Test:** amend the constitution, then run the plan/arch-review context surface; assert the emitted constitution is byte-identical to the on-disk `.doti/memory/constitution.md`.
- **Acceptance Scenarios:**
  1. **Given** an amended constitution, **When** `/03-doti-plan` runs, **Then** the Constitution Check evaluates against the amended content (not a cached/older version).
  2. **Given** no constitution exists, **When** plan/arch-review run, **Then** the surface reports the absence explicitly (fail-closed / clear signal), never silently proceeding as if a constitution were satisfied.

### Work Item 3 — The structured template ships to scaffolded repos; this repo keeps its real constitution (Priority: P3)

`hx new` generates a repo that carries the **§1/§2** template plus the `doti-constitution` skill, and initializes the generated repo's `.doti/memory/constitution.md` (§1 cited, §2 placeholders to fill). speckit-doti's own constitution is **restructured in place** — its 9 principles become §1 and an explicit **§2** (.NET 10 stack, pure-core/thin-CLI + analyzer/Sentrux coding style) is added — and remains a **real, filled** constitution, never reverted to placeholders.

- **Why this priority:** the scaffold is the product; a generated repo must inherit the same §1/§2 capability, but this repo's authored constitution is truth that must not regress to placeholders.
- **Independent Test:** `hx new` a repo; assert it contains the §1/§2 template + the `doti-constitution` skill + an initialized constitution; separately assert this repo's `.doti/memory/constitution.md` has its 9 §1 principles + a filled §2 and **no placeholder tokens**.
- **Acceptance Scenarios:**
  1. **Given** `hx new`, **When** the repo is generated, **Then** the §1/§2 `constitution-template.md` and the `doti-constitution` skill are present, with §1 as a fixed reference and §2 as the only placeholders.
  2. **Given** this repo, **When** the template is updated, **Then** `.doti/memory/constitution.md` (its §1 principles + filled §2) is not replaced by template content.

### Work Item 4 — Arch-review consumes the constitution's tech stack + coding style (Priority: P4)

The `/06-doti-arch-review` template is modified so the review draws its tech-stack and coding-style expectations from the (freshly-injected) constitution, rather than only generic lenses — so a change that violates a declared convention is caught with a finding that cites the constitution.

- **Why this priority:** it closes the gap where arch-review never sees project conventions; depends on WI-2 (fresh injection) being in place.
- **Independent Test:** declare a coding-style rule in the constitution, run arch-review on a change that breaks it, assert a finding citing the constitution.
- **Acceptance Scenarios:**
  1. **Given** a constitution declaring "thin CLI: parse→delegate→render", **When** arch-review runs on a change that puts logic in a `*.Cli` handler, **Then** a finding cites that constitution principle.

### Work Item 5 — Semantic finder tuned for .NET findings (Priority: P5)

Tune the 008 advisory drift finder (`hx doti drift-candidates`) so it is optimised for .NET/C# findings rather than general English: .NET-aware chunking (by type/member instead of whole-file), a code-aware embedding instruction, and thresholds recalibrated on a .NET-relevant gold set. Today thresholds are calibrated on Wikipedia/PAWS/STS-B and the code itself flags this as domain-sensitive ("recalibrate per domain", `Thresholds.cs`), chunking is whole-file (`DriftCandidateService.BuildChunks`), and the Qwen3 instruction prefix is unused by the symmetric drift path (`Qwen3Embedder.cs:79`).

- **Why this priority:** P5 because it tunes an existing *advisory, never-gating* engine — last in the safety-first order, after the workflow-correctness items (WI1–4). It links to the constitution theme: the §2 tech stack ("this is a .NET repo") is the context that justifies .NET-tuned findings.
- **On-demand agent tool (light):** beyond `/08-drift-review`, the tuned finder is documented as an **on-demand advisory tool the agent may invoke at ANY cycle stage** (e.g. mid-`/07-implement` to check a doc didn't go stale after a rename). The exposure is the *available, not mandated* middle path — the agent reaches for "semantic-grep" by judgment, the way it reaches for `grep`. (NOT the general arbitrary-query command or a warm daemon — those are deliberately out of 009 scope.)
- **Scope (non-goals):** the tuned finder stays on the **code↔docs/prose** axis — NOT wired into `converge` (structured `[covers]` markers → regex; semantic would risk false "covered") or `analyze` (contradiction detection → cosine is the wrong tool, it flags opposites as similar). Its docs MUST carry match-type guidance (use for paraphrase code↔docs; NOT for IDs/markers/contradictions) and the advisory-never-proof boundary. NOT in scope: an arbitrary-query `semantic-search` command or a warm-model daemon.
- **Independent Test:** on a .NET gold set, the tuned finder surfaces a drift the general-tuned finder misses (e.g. a renamed method vs. its stale XML-doc summary) without raising false positives above the calibrated band; and the agent context documents the finder as an on-demand advisory tool with the match-type guidance.

### Edge Cases

- A repo with **no** constitution reaching `/03-plan` / `/06-arch-review` — **surface-and-proceed** (FR-016): the context step reports the absence with an actionable note and the stage continues; it never blocks and never pretends a §2 check passed.
- An **amendment mid-cycle** — plan stamped, constitution amended, plan re-run: the re-injected content must be the amended version (no staleness window).
- The constitution as a **project artifact vs. cycle artifact** — it persists across `001..NNN` feature cycles and must not be staled or re-stamped by ordinary cycle transitions.
- `doti payload check` / `render-skills --check` must stay clean: a new skill + template are managed payload assets that must be single-sourced and rendered, not hand-placed.
- A generated repo's template must NOT clobber an operator's already-filled constitution on `doti install` re-run (managed-asset preservation, like other Doti assets).
- **Project-name derivation** with **no** solution file or **multiple** solutions on `doti install` — fall back deterministically (repo directory name) rather than leaving a `[PROJECT_NAME]` token or guessing ambiguously (FR-015).

## Scope

**Included:** an unnumbered `doti-constitution` skill ahead of specify; the **§1/§2** constitution template (doti's leaner structure — §1 inherited invariants cited, §2 project declarations as the only placeholders — single-sourced in `.doti/core`); a command-backed `hx` surface that emits the current constitution for fresh injection; plan + arch-review consuming fresh **§2**; arch-review using the §2 tech stack + coding style; shipping the template + skill into `hx new` repos with operator-edit preservation; this repo's constitution restructured to keep its 9 principles (§1) + add an explicit §2; and .NET-tuning the advisory drift finder (code↔docs only) + documenting it as an on-demand agent tool with match-type guidance.

**Excluded (this feature):** restructuring every existing cycle stage; a fully command-ENFORCED Constitution Check gate (decided in `/02-clarify`: §2 stays **agent-evaluated** — the deterministic gate is unchanged; the operator owns/amends §2 via the `doti-constitution` skill); re-introducing any Spec Kit principle that doti already codifies (versioning, CLI shape, gates — see FR-004); Spec Kit's SemVer doc-versioning ritual + Sync Impact Report (FR-005) and its extension-hook mechanism (`.specify/extensions.yml`). The semantic finder is tuned (WI-5) but stays on the **code↔docs axis** — it is explicitly NOT wired into `converge` or `analyze` (structured/contradiction checks where cosine adds noise).

## Functional Requirements

- `FR-001`: An **unnumbered** `doti-constitution` utility skill MUST exist (rendered like `doti-bug`/`converge`/`doti-amend`/`doti-drift-fix`), run ahead of and independent of the numbered cycle, authoring/amending `.doti/memory/constitution.md`. It MUST NOT be a numbered `/0N` cycle stage nor appear in `workflow.yml` (FR-003). `[WI1]`
- `FR-002`: The constitution template MUST follow doti's **two-layer structure** (NOT a blind copy of Spec Kit): an **auto-filled title** (the project name — FR-015, not a placeholder); **§1 Inherited doti invariants** — a named, non-fillable *reference* to what doti already codifies (deterministic gates, library-first/pure-core, thin CLI + `CliResult`, GitVersion versioning, Sentrux complexity, cross-platform, hygiene/SAST, codified cycle, engineering discipline) which the project MUST NOT re-declare or weaken; and **§2 Project declarations** — the only operator-fillable placeholders: `[DOMAIN_PRINCIPLES]`, `[TECH_STACK]` (beyond the .NET 10 baseline), `[CODING_STYLE]`, `[SECURITY_COMPLIANCE]`, `[PERFORMANCE]`. `[WI1]`
- `FR-003`: The constitution MUST be treated as a **project-level artifact** (authored once, amended occasionally) that persists across feature cycles and is NOT a per-feature cycle stamp. `[WI1]`
- `FR-004`: The template MUST NOT carry a placeholder (or a fillable principle) for anything in §1 — a doti-codified invariant. In particular it MUST NOT ask a project to declare a **versioning policy** (GitVersion owns it), a **CLI/output shape** (the scaffold + `Channel Independence` own it), or **quality-gate/workflow** rules (the codified cycle owns them) — nor for **identity metadata the scaffold/solution already knows** (the project name; see FR-015). The rule generalises: if doti, the scaffold, or the solution can *determine* it, the constitution states it — it is never a fill-in blank. `[WI1]`
- `FR-005`: Constitution amendments MUST be tracked by the **doti cycle + git history** — NOT a hand-maintained SemVer doc-version line or Sync Impact Report (doti has codified versioning; the constitution carries no governance-versioning ritual). `[WI1]`
- `FR-006`: `hx` MUST expose a deterministic, command-backed surface that emits the **current** `.doti/memory/constitution.md` content (the §2 declarations), reporting its absence explicitly when none exists. It serves both as the **carrier for the codified stage delivery** (FR-007/008) and as the on-demand agent tool. `[WI2]`
- `FR-007`: `/03-doti-plan` MUST receive the current **§2** as **output of the stage's command-backed context step** — codified, automatic, delivered by the command the stage already runs (NOT a separate "remember to run it" instruction the agent can skip) — so the Constitution Check always evaluates against fresh content. §1 is not re-checked by the agent (already gate/ArchUnit/Sentrux/GitVersion-enforced). `[WI2]`
- `FR-008`: `/06-doti-arch-review` MUST receive the current **§2** the same codified way (folded into / alongside its existing `review-context` invocation) and scope its review using the §2 **tech stack + coding style**. The *delivery* is enforced by code; the *evaluation* of the change against §2 is agent-judged (FR-012). `[WI2, WI4]`
- `FR-009`: The structured constitution template MUST be single-sourced in `.doti/core` and ship into `hx new`-generated repos along with the `doti-constitution` skill. `[WI3]`
- `FR-010`: `hx new` MUST initialize a generated repo's constitution from the template; `doti install` re-runs MUST preserve an operator-edited constitution (managed-asset preservation). `[WI3]`
- `FR-011`: This repo's `.doti/memory/constitution.md` MUST remain a real, filled constitution — its existing 9 principles are the §1 invariants and MUST be retained; this feature ADDS an explicit **§2** (this repo's tech stack + coding style: .NET 10, pure-core/thin-CLI, the analyzer/Sentrux conventions) and introduces **no placeholder tokens** into this repo's file. `[WI3]`
- `FR-012`: The §2 Constitution Check MUST be **agent-evaluated** (not a fail-closed gate step) — the arch-review record/template surfaces a finding citing **§2** when a change violates a §2-declared coding-style or tech-stack convention, while the deterministic gate (build/test/ArchUnit/Sentrux/hygiene) is unchanged. §2 is operator-owned (authored/amended via the `doti-constitution` skill), so the evaluated content is the operator's, not doti-imposed. `[WI4]`
- `FR-013`: The advisory `hx doti drift-candidates` finder MUST be tuned for .NET findings (member-level chunking, code-aware instruction, .NET-calibrated thresholds) without changing its never-gating, advisory, **code↔docs-only** contract (NOT extended to `converge`/`analyze`). `[WI5]`
- `FR-014`: The tuned finder MUST be documented (agent context / skill) as an **on-demand advisory tool** the agent may invoke at any cycle stage, carrying explicit **match-type guidance** (use for paraphrase code↔docs; NOT for IDs/markers → `grep`, NOT for contradictions → reasoning) and the advisory-never-proof boundary. It MUST NOT add an arbitrary-query `semantic-search` command or a warm-model daemon (out of scope). `[WI5]`
- `FR-015`: The constitution's project name MUST be **auto-derived**, never a `[PROJECT_NAME]` placeholder: `hx new` pre-fills it from `--name`; `doti install` into an existing repo derives it from the solution (`.slnx`/`.sln`) file name, falling back to the repo directory name when no single solution is found. `[WI1, WI3]`
- `FR-016`: When **no** constitution exists, `/03-plan` and `/06-arch-review` MUST **surface-and-proceed** — the codified context step reports the absence with an actionable note (`run /doti-constitution`) and the stage continues; it MUST NOT block. The constitution is optional advisory context (its absence is not a deterministic-proof failure); §1 stays gate-enforced regardless. `[WI2]`

## Success Criteria

- `SC-001`: Running the constitution stage on a repo without one yields a complete `.doti/memory/constitution.md` with **zero** unfilled `[PLACEHOLDER]` tokens (placeholders exist only in §2).
- `SC-002`: A `hx new`-generated repo contains the structured constitution template, the `doti-constitution` skill, and an initialized constitution; `doti payload check` + `render-skills --check` remain clean.
- `SC-003`: After amending the constitution, the content emitted by the fresh-injection surface at `/03-plan` and `/06-arch-review` is **byte-identical** to the on-disk constitution.
- `SC-004`: An arch-review of a change that violates a constitution-declared convention produces a finding that cites the constitution; a conforming change does not.
- `SC-005`: Across all 009 work, this repo's authored constitution is preserved (no placeholder tokens introduced, principles retained).
- `SC-006`: A `doti install` re-run on a repo with an operator-edited constitution preserves the operator's content (does not resurrect the template).
- `SC-007`: On a .NET gold set, the tuned finder surfaces ≥1 .NET-specific drift the general-tuned finder misses, with false positives no higher than the calibrated band.
- `SC-008`: The shipped template contains **no** placeholder or fillable principle for any §1 invariant — specifically none for versioning, CLI/output shape, or quality-gate/workflow rules (FR-004); a reviewer can diff the template against the §1 list and find every codified item present only as a fixed reference.
- `SC-009`: The agent context/tool docs describe the finder as an on-demand advisory tool and explicitly steer paraphrase code↔docs → semantic, IDs/markers → `grep`, contradictions → reasoning — so a reader can tell when NOT to reach for it.
- `SC-010`: A constitution produced by `hx new --name Acme.Widget` and one produced by `doti install` into a repo whose solution is `Foo.slnx` each carry the correct project name in the title with **no** `[PROJECT_NAME]` token remaining.

## Key Entities

- **Constitution** — a project artifact with an **auto-filled title** (the project name, derived by scaffold/solution — FR-015) over two layers: **§1 Inherited doti invariants** (the codified givens — deterministic gates, library-first, thin CLI/`CliResult`, GitVersion versioning, Sentrux, cross-platform, hygiene, codified cycle, engineering discipline; cited, never re-declared) and **§2 Project declarations** (domain principles, tech stack, coding style, security/compliance, performance — the only operator-authored content). Lives at `.doti/memory/constitution.md`; one per repo; persists across feature cycles. Only **§2** is re-injected/checked by plan + arch-review (§1 is gate-enforced).
- **Constitution template** — the source (in `.doti/core/templates`) carrying §1 as a fixed reference block and §2 as the only placeholders. doti's template is deliberately **leaner than Spec Kit's** because doti codifies the universals Spec Kit asks a project to declare. Ships to generated repos; never the source of *this* repo's real (already-filled) constitution.

## Deterministic Surfaces

- A new `doti-constitution` skill (rendered from `.doti/core/skills.json`) + a command template under `.doti/core/templates/commands/`.
- A command-backed constitution-emit surface on `hx` (e.g. `hx doti constitution` — **planned, advisory until built**) that the plan + arch-review skills invoke for fresh injection.
- `.doti/core/templates/constitution-template.md` (restructured) + `.doti/core/memory/constitution.md` (this repo's real one) + the generated-repo install path (`DotiInstaller` managed-asset preservation).
- `.doti/core/skills.json` — the new **unnumbered** `doti-constitution` skill (NOT `workflow.yml`, which holds only the numbered `/01`–`/09` cycle stages).
- `doti payload check`, `doti render-skills --check`, `gate run` — the parity/gate proofs that must stay green.

## Architecture Impact

- Doti-prose: new skill + command templates + the constitution + arch-review template edits (single-sourced in `.doti/core`, rendered).
- `Hx.Doti.Core` (the renderer/installer + managed-asset preservation for the constitution) and possibly `Hx.Runner.Cli` (the constitution-emit command) — thin CLI delegating to a `*.Core` surface, per the cliSurfaceConfinement/cliDelegation families.
- The constitution as a project artifact may touch `Hx.Cycle.Core`'s stage model only insofar as the workflow.yml placement is decided; it should NOT become a diff-bound per-cycle stamp (FR-003).
- WI-5: `Hx.Embedding.Core` (chunking/instruction) + `Hx.Semantic.Core` + the calibration doc — the Gate/Cycle ↛ Semantic boundary (008 FR-020) MUST be preserved.

## Sentrux And Hygiene Impact

- New managed payload assets (skill, templates) must be single-sourced + rendered (no hand-edited installed files) and pass `doti payload check`.
- No new Sentrux baseline; any new `*.Core` logic stays within the function-size limit and the layer/cycle boundaries.
- The constitution + template are Doti prose → Sentrux source-excluded (008 FR-029); they must not enter the code-scope graph.

## Assumptions

- The constitution is authored once per project + amended, not stamped per feature cycle (FR-003) — chosen because a per-feature constitution stamp contradicts the Spec Kit model where the constitution governs *all* features.
- The fresh-injection surface is command-backed on `hx` (a `doti constitution` emit), because the user said "reinjected … by the hx" — a static path reference is explicitly what we are replacing.
- This repo's actual constitution has **9** principles (Deterministic Ownership, Bootstrap Honesty, Template Boundary, Public Hygiene, Cross-Platform, Engineering Discipline, Operator Decisions, Codified Cycle, Channel Independence) — these ARE the §1 invariants and are retained verbatim. (Note: `plan-template.md:15`'s Constitution Check lists only 7 — it is **stale**; a side-fix is to refresh it to the 9 §1 names, or better, to cite §1/§2 by reference rather than enumerate.)
- **Decided with the operator:** the constitution skill is an **unnumbered, labelled utility skill** (`doti-constitution`), joining the existing unnumbered utilities (`doti-upgrade`/`doti-bug`/`converge`/`doti-amend`/`doti-drift-fix`) rather than a numbered `/0N` cycle stage — because it is project-level, authored once, and *consumed* by the numbered stages, so it does not belong in the per-feature `/01`–`/09` sequence.
- **Decided with the operator (constitution rationalisation):** doti is more opinionated than Spec Kit, so the constitution is **two-layer** — **§1 inherited doti invariants** (codified, cited, never re-declared) + **§2 project declarations** (the only fillable content). Anything doti already codifies (versioning via GitVersion, CLI/output shape via the scaffold + `Channel Independence`, gates/workflow via the codified cycle, complexity via Sentrux, hygiene/SAST) is a §1 *given* and MUST NOT appear as a template placeholder. Only §2 (domain, tech stack, coding style, security, performance) is project-authored and the only thing plan/arch-review re-inject and check.
- **Decided with the operator (this-repo migration):** restructure this repo's `.doti/memory/constitution.md` to keep its 9 principles as §1 and **add an explicit §2** (its .NET 10 stack + pure-core/thin-CLI + analyzer/Sentrux coding style) — introducing **no** placeholder tokens here (a real constitution, not a template).
- **Decided with the operator (no doc-versioning ritual):** the constitution carries no Spec Kit-style SemVer doc-version line or Sync Impact Report; amendments are tracked by the doti cycle + git history (doti already codifies versioning).

## Acceptance

- Command-backed today: `doti render-skills`/`--check`, `doti payload check`, `gate run`, `doti install` managed-asset preservation — all exist and gate the new assets.
- Advisory / planned: the `hx doti constitution` emit surface and the fresh-injection wiring into plan/arch-review do not exist yet (this feature builds them); the .NET-tuned finder (WI-5) is advisory and never-gating by 008's contract.

## Clarifications

### Resolved during specify (2026-06-28)

- Q: Is `doti-constitution` a numbered cycle stage or an unnumbered utility? → A: **Unnumbered utility skill** (joins `doti-bug`/`converge`/…), not a `/0N` stage, not in `workflow.yml` (FR-001/003).
- Q: Should the template copy Spec Kit's principle set? → A: **No** — doti's **§1/§2** structure: §1 inherited doti invariants (cited, never re-declared) + §2 project declarations (the only placeholders). Anything doti codifies (versioning, CLI shape, gates, complexity, hygiene) is §1, never a placeholder (FR-002/004).
- Q: Restructure this repo's constitution, or keep prose + only template generated repos? → A: **Restructure** — keep the 9 principles as §1 and **add an explicit §2** (this repo's stack + style); introduce no placeholder tokens here (FR-011).
- Q: Keep Spec Kit's SemVer doc-versioning + Sync Impact Report? → A: **No** — amendments tracked by the doti cycle + git; doti already codifies versioning (FR-005).
- Q: Split the .NET semantic-finder tuning (WI-5) to a separate 010? → A: **No — keep it in 009.** And the tuned finder stays **code↔docs-only**; it is NOT wired into `converge` (structured markers) or `analyze` (contradiction detection) where cosine adds noise (FR-013).

### Clarify session 2026-06-28

- Q: Should the §2 Constitution Check be command-enforced (a fail-closed gate step) or agent-evaluated? → A: **Agent-evaluated by default** — the deterministic gate stays on the existing proofs (build/test/ArchUnit/Sentrux/hygiene); plan + arch-review judge the change against fresh §2 and cite it in findings (no false "enforced" claim for free-text §2). §2 is **operator-owned**: the operator authors and amends it via the `doti-constitution` skill, so they control what the agent evaluates against (FR-008/FR-012; the enforcement mode is a property of the *constitution the operator maintains*, not a doti-imposed gate).
- Q: Fresh injection — a command the skills invoke, or a read-the-file step? → A: **Command-backed (A), and CODIFIED**: the §2 is delivered automatically as output of the stage's command-backed context step (e.g. folded into / alongside the `review-context` invocation the stage already makes), NOT a "remember to run `hx doti constitution`" instruction the agent can skip. *Delivery* is codified (enforced by code, not convention); *evaluation* stays agent (Q1). The on-demand `hx doti constitution` command still exists for ad-hoc/agent-tool use (FR-006).
- Q: When no constitution exists, block or proceed? → A: **Surface-and-proceed** — the context step reports the absence with an actionable note (`run /doti-constitution`); plan/arch-review proceed; the deterministic gate is unaffected (§1 still governs). Blocking would make the optional utility a hidden hard-prerequisite and fail-close on the absence of *advisory* context (FR-016).

### Status — all resolved

All `/02-doti-clarify` questions are resolved (see the dated sessions above). No open `[NEEDS CLARIFICATION]` markers remain; the spec is ready for `/03-doti-plan`.
