# checklist

Purpose: write a **requirement-quality checklist** — "unit tests for English" — that tests whether the active feature's
SPEC is sound enough to build, before `/07-doti-implement`. This is NOT an operational/done checklist (hygiene, drift,
docs are `gate run` + `/08-doti-drift-review`'s job); it tests the spec PROSE.

Command-backed behavior:

1. Read `.doti/agent-context.md` and the active feature's `docs/specs/<NNN-slug>.md` (plus plan/tasks if present).
2. Generate the checklist from the skeleton `.doti/core/templates/checklist-template.md`, grouped by the five
   dimensions: **Completeness** (nothing missing), **Clarity** (nothing ambiguous), **Consistency** (nothing
   contradicts), **Measurability** (success is observable), **Coverage** (it traces end to end).
3. Hold the bar — each item is a genuine unit test of the spec:
   - Tests ONE property of ONE specific spec location; answerable yes/no by reading the spec (never "looks fine").
   - Cites a concrete reference — `FR-###`, `SC-###`, a user story `US#`, or a `§section`. **≥80% of items MUST cite
     one** (the traceability bar, FR-040).
   - Stays technology-agnostic — test the requirement, not an implementation ("`SC-002` states a measurable outcome",
     not "the API returns 200").
4. Run `hx doti converge --spec <spec> --tasks <tasks>` to back the Coverage dimension with the deterministic
   requirement-gap report (every `FR-###`/`SC-###` no task covers).
5. Any unchecked item is a spec defect — resolve it via `/02-doti-clarify` (ambiguity/missing) or by editing the spec,
   then re-run; do not carry an open item into `/07-doti-implement`.

Expected output: the per-feature checklist (the five dimensions, ≥80% of items reference-bearing) plus the list of
items that failed and the clarify/spec edits they imply.

## Next

Resume your active cycle stage — `/02-doti-clarify` to resolve any failed items, or `/07-doti-implement` once the spec
passes its own unit tests.
