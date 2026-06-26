# Checklist Template — "unit tests for English"

> A checklist is a set of **unit tests for the spec prose**, not a generic to-do list. Each item tests ONE quality
> property of a SPECIFIC spec location and is answerable yes/no by reading the spec — never "looks fine". Generate one
> per feature from its spec/plan/tasks (process: `.doti/core/templates/commands/checklist.md`), grouped by the five
> dimensions below. **Bar: ≥80% of items cite a concrete
> reference** (`FR-###`, `SC-###`, a user story `US#`, or a `§section`). Operational readiness (hygiene, drift,
> docs, advisory gaps) is enforced by `gate run` and `/08-doti-drift-review` — keep it OUT of this checklist; this one
> tests whether the SPEC is sound enough to build.

## Completeness — is anything missing?

- [ ] Every user story has acceptance scenarios (Given/When/Then). `[US#]`
- [ ] Every `FR-###` maps to ≥1 user story; every `SC-###` is present and numbered. `[FR-###]` `[SC-###]`
- [ ] Edge cases / failure / partial / re-run behavior are stated, not implied. `[§Edge Cases]`

## Clarity — is anything ambiguous?

- [ ] No `[NEEDS CLARIFICATION]` markers remain unresolved. `[§Clarifications]`
- [ ] Each `FR-###` is one testable, unambiguous MUST (no "and/or" smuggling two requirements into one). `[FR-###]`
- [ ] Terms are used consistently; no undefined jargon.

## Consistency — does anything contradict?

- [ ] No requirement contradicts another or the declared Scope. `[FR-###]` `[§Scope]`
- [ ] Story priorities (P1/P2/P3) are coherent — nothing lower-priority blocks a higher one. `[US#]`
- [ ] No success criterion conflicts with the explicitly-excluded scope. `[SC-###]` `[§Scope]`

## Measurability — can success be observed?

- [ ] Each `SC-###` is a number or observable outcome, technology-agnostic (not "fast" or "API < 200ms"). `[SC-###]`
- [ ] Each acceptance scenario has a checkable **Then**. `[US#]`

## Coverage — does it trace end to end?

- [ ] Each `FR-###`/`SC-###` traces to a task (`[covers …]`) — run `hx doti converge` to confirm no gap. `[FR-###]` `[SC-###]`
- [ ] Every user story is independently testable (delivers value on its own). `[US#]`
- [ ] ≥80% of the items in THIS checklist cite a concrete spec reference (the traceability bar above).
