# Arch Review — 013 Doti Auto Mode

**Stage:** `/06-doti-arch-review`. **Change under review:** [spec](../specs/013-doti-auto-mode.md) / [plan](../plans/013-doti-auto-mode-plan.md) / [tasks](../tasks/013-doti-auto-mode-tasks.md).

## Triage

**Doti-PROSE change — no code.** The deliverable is one `.doti/core/skills.json` entry + one command template `.doti/core/templates/commands/doti-auto.md`, rendered to `.claude`/`.agents`. No `*.Core`, no contract, no CLI, no `scaffold/templates/**` generated-code, no rule/Sentrux/ArchUnit surface. Per the arch-review triage split, the **code lenses do not apply** (design-soundness-of-code, data-contract, blast-radius-of-code, modularity, testability, security-of-code, edge-case/failure-mode-of-code all EXIT *not applicable* — there is no code to fault). The applicable concern is **correctness/soundness of the prose** the skill encodes — specifically that the orchestration text does not become a way to weaken a gate or publish unattended.

## Applicable lens — prose design-soundness & safety-by-construction

**Concern:** an auto-driver skill is only safe if its text correctly enumerates where it MUST stop and what it must NEVER do. A drift here is a documentation-correctness defect (the skill would read as licence to skip a check), so it is in-scope even for a prose change.

**Findings (all advisory, none blocking):**

- **F1 (MEDIUM → resolved in design):** the stop conditions must be exhaustive and unambiguous, or auto mode could guess past an operator-decision point. *Mitigation:* the spec FR-004 enumerates the four classes (clarify ambiguity/`[NEEDS CLARIFICATION]`; arch-review BLOCKER or missing applicable lens; unrecoverable gate failure; genuine blocker incl. <95% confidence) and T002 requires the template to encode each verbatim. Evidence: spec FR-004, tasks T002.
- **F2 (MEDIUM → resolved in design):** the never-publish guarantee must be explicit and unconditional. *Mitigation:* FR-006 + the template (T002) state a "to release" run ends at the LOCAL `hx release` and surfaces the remote `v*` push as a separate explicit operator step — never performed by auto mode. Consistent with `CLAUDE.md` ("`hx release` does not push tags; `/09-doti-release` owns the later remote push"). Evidence: spec FR-006.
- **F3 (LOW → resolved):** the auto-fix boundary must not become a licence to relax the spec. *Mitigation:* FR-005 + the spec `## Clarifications` fix a spec↔code gap in the CODE in-cycle, never by downgrading the spec — matching the standing drift-review invariant. Evidence: spec FR-005, Clarifications.
- **F4 (LOW):** `doti-auto` must not be mistaken for a new enforcement surface. *Mitigation:* FR-008 + plan Architecture delta state it adds NO numbered stage, NO reorder, NO chokepoint replacement — it chains existing stage skills. The parity authorities (`render-skills --check`, `payload check`) are the only deterministic proof. Evidence: plan, spec FR-008.

## Lenses exited *not applicable*

design-soundness-of-code, edge-case/failure-mode (code), data-contract, security (code/secret/SCA), blast-radius (code), modularity/design-smells, testability — no code in this change.

## Verdict

**No open BLOCKER in any applicable lens.** The single applicable lens (prose safety-by-construction) is satisfied by the spec's enumerated stop conditions + never-weaken/never-publish requirements, which T002 must encode and `/08-doti-drift-review` verifies in the rendered skill text. Cleared for `/07-doti-implement`.
