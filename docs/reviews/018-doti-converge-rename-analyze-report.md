# Analyze Report — 018 Rename `converge` → `doti-converge`

**Stage:** `/05-doti-analyze`. Consistency across [spec](../specs/018-doti-converge-rename.md) ↔ [plan](../plans/018-doti-converge-rename-plan.md) ↔ [tasks](../tasks/018-doti-converge-rename-tasks.md).

## Coverage

| Requirement | Tasks | Status |
|---|---|---|
| FR-001 (skill name + template renamed) | T001, T002 | covered |
| FR-002 (re-render, obsolete dirs removed, parity clean) | T004, T005 | covered |
| FR-003 (skill refs updated; command/capability left) | T003 | covered |
| SC-001 (renders as `doti-converge`, no `converge` skill) | T004 | covered |
| SC-002 (`hx doti converge` command unchanged) | T005 | covered |
| SC-003 (parity clean; docs refer to `/doti-converge`) | T003, T005 | covered |

No FR/SC orphaned; no task without a requirement.

## Consistency

- Spec ↔ plan ↔ tasks agree on the **skill-only** rename (name + template + the three `/converge` skill references) and the explicit **command/capability untouched** boundary — the highest-value consistency point (mis-renaming `hx doti converge` would be a regression). T003 names exactly the three skill-reference files and excludes the command/capability mentions.
- The obsolete-dir cleanup (T004) is consistently called out (rename, not add) so the rendered set + payload parity stay clean.
- No code/contract/proof surface — consistent with the docs/single-source Architecture Impact.

## Ambiguities / conflicts

- None. The skill-vs-command boundary is verified (the command lives in `RunnerCommandFactory.Doti.cs`; the `converge` capability mentions are command references). No `[NEEDS CLARIFICATION]`.

## Verdict

**Consistent and fully covered.** Proceed to `/06-doti-arch-review`.
