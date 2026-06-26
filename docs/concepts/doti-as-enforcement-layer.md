# doti as the enforcement layer over the shared SDD workflow

> Decision record for **T001 / FR-026** (feature 007). This is the framing that drives the 007 build order; later tasks ride on it.

## Decision

**doti rides and absorbs the shared spec-driven-development (SDD) workflow; it does not maintain a silently-diverging fork.** The common workflow — `specify → clarify → plan → tasks → analyze → implement` (and the wider Spec Kit template/methodology/command-transpilation ecosystem) — is treated as the **shared layer**. doti's distinct, irreducible value is the layer it adds on top: a **non-forgeable proof + fail-closed chokepoint substrate**.

Concretely:

- **Shared (absorb, don't reinvent):** the workflow stages and their intent; the spec/tasks template structure (prioritised user stories, Independent Test, Given/When/Then, MVP-first phases, `[P]`/`[Story]` markers); the one-source→many-agent command transpilation; the methodology (bug mini-cycle, `converge`, checklist depth, context-budget implement). These are folded in (007 Phase 6 / FR-034–FR-040) rather than forked.
- **doti's teeth (the value):** diff-bound `CycleStageProof`s with read-time freshness (`doti cycle stamp`/`status`), the aggregated fail-closed `GateProof` (`gate run`), the enforcing chokepoints (`doti cycle check`, `doti question check`, the untracked insurance pre-commit hook), ordered-task enforcement (FR-028), the Living-Spec persistence model (FR-027), and tier-driven gate layering (FR-029–031). Spec Kit is honor-system with **no enforcement**; doti supplies exactly that.

## Why ride, not fork

- **Forking diverges silently.** A hard fork of the workflow drifts from upstream methodology improvements with no signal; doti's positioning (FR-026) is to *ride* the shared layer so improvements are absorbed, not missed.
- **The teeth are orthogonal to the methodology.** The proof/chokepoint substrate is enforcement *over* the workflow, independent of which template prose or command set the workflow uses — so it can sit on top of the shared layer without owning it.
- **Honor-system is the gap doti fills.** Spec Kit (reviewed at `D:\temp\spec-kit`, HEAD `e7ec7c1`) has no gates, no diff-bound proofs, no fail-closed transitions. Riding + adding teeth keeps the upstream's structure *and* makes it non-bypassable.

## Build-order implication (drives 007's phase order)

Because doti's value is the **substrate beneath** the workflow, the substrate + framing must exist before the distribution ships the structure that rides on it. That is why 007 is sequenced **framing + enforcement substrate first** (Phase 0: this decision, Living-Spec, ordered-task enforcement; Phase 1: contracts + the tier model), then the distribution (Phases 2–5) lands on that floor, then the Spec Kit absorptions (Phase 6) layer the shared methodology onto the substrate with teeth. Building the distribution before the substrate would retrofit the enforcement later; building the absorptions before the substrate would re-import honor-system prose with nothing to enforce it.

## Positioning statement (for surfaces that describe doti)

> doti is the non-forgeable proof + fail-closed chokepoint substrate over the shared spec-driven workflow. It absorbs the shared (Spec Kit) structure and methodology and makes it enforceable; it does not fork the workflow.

This statement is folded into the rendered agent context and docs by the later surface tasks (FR-041/FR-042; T043/T044) rather than hand-edited into rendered files here.
