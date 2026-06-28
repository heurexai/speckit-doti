# Analyze Report — 020 Scaffold versioning + copyright defaults

**Stage:** `/05-doti-analyze`. Cross-artifact consistency across spec ↔ plan ↔ tasks. **No CRITICAL.**

## Coverage (every FR/SC → ≥1 task)

| Req | Tasks | OK |
| --- | --- | --- |
| FR-001 ship GitVersion config | T002, T003 | ✅ |
| FR-002 trunk default = Patch | T003 | ✅ |
| FR-003 feature → minor preserved | T003 | ✅ |
| FR-004 first release = 0.1.0 | T002, T003 | ✅ |
| FR-005 not GitFlow default | T002, T003 | ✅ |
| FR-006 release assembly Copyright | T004, T005 | ✅ |
| FR-007 year = release year | T004, T005 | ✅ |
| FR-008 holder = `--company` | T004, T005 | ✅ |
| FR-009 test regression guard | T002, T004 | ✅ |
| SC-001 v0.1.0 first tag | T002, T003, T006 | ✅ |
| SC-002 patch(bug)/minor(feature) | T003, T007 (+ advisory note) | ✅ |
| SC-003 release-year copyright | T004, T005, T006 | ✅ |
| SC-004 regression guard fails closed | T002, T004, T007 | ✅ |

No FR/SC is uncovered; no task is orphaned (T001 is a labelled premise check, T006–T007 are docs/gate polish).

## Consistency checks

- **Spec ↔ plan:** WI1 (versioning) → plan Decision 1 (GitHubFlow/v1 + next-version 0.1.0); WI2 (copyright) → plan Decision 2 (build-time MSBuild year). The plan's rejected alternatives (TrunkBased/preview1, release-time injection, static year) trace back to the spec's FR-005/FR-007. Aligned.
- **Plan ↔ tasks:** every plan file (`GitVersion.yml`, `Directory.Build.props`, the golden test, CHANGELOG/README) has a task; the tasks introduce no file the plan didn't name. The TDD ordering (T002 before T003; T004 before T005) matches the plan's test-first intent for FR-009.
- **No contradiction:** the build-time copyright mechanism is stated identically in spec FR-007 (WHAT), plan Decision 2 (HOW), and tasks T005. The "no `*.Core`/`*.Cli` change / no rule delta" claim is consistent across plan Design and tasks (no task touches production code or `rules/*`).
- **Terminology:** "trunk-based / GitHubFlow/v1", "release-year copyright", "0.1.0 start" are used consistently across all three artifacts.

## Honest gaps (non-blocking)

- **SC-002 live behavior** (a generated repo's actual patch/minor increment end-to-end) is exercised only under `HX_TEMPLATE_ROUNDTRIP=1`; the default gate verifies the deterministic golden assertions on the shipped config. This boundary is recorded in the spec, plan (Risks), and tasks (Gate Notes) — consistently disclosed, not hidden.

## Verdict

Consistent and fully covered. No CRITICAL, no uncovered FR/SC, no contradiction. Ready for `/06-doti-arch-review`.
