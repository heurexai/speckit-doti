# Tasks: Single-Source the Constitution

Plan: `docs/plans/010-single-source-constitution-plan.md`. Spec: `docs/specs/010-single-source-constitution.md`. **Priority mode = workflow/tooling: safety-first** — keep parity green. Phases sequential; T004 is the final gate.

## Phase 0 — Single-source the constitution — Checkpoint: `gate run` green

- [ ] T001 [test] Single-source invariant: `.doti/core/memory/constitution.md` does NOT exist; `.doti/memory/constitution.md` exists, has `## §1` + `## §2`, and zero placeholder tokens — `test/Hx.Doti.Tests/ConstitutionTests.cs` — [covers FR-001, SC-001]
- [ ] T002 Delete `.doti/core/memory/constitution.md` (`git rm`); `.doti/memory/constitution.md` untouched — `.doti/core/memory/constitution.md` — [covers FR-001]
- [ ] T003 Drop the redundant `;$(RepoRoot).doti/core/memory/constitution.md` segment from the `_HxDoti` `Exclude` glob (keep the `.doti/memory/constitution.md` exclusion) — `tools/Hx.Scaffold.Cli/Hx.Scaffold.Cli.csproj` — [covers FR-002]

## Phase 1 — Docs + verification — Checkpoint: `gate run` green

- [ ] T004 CHANGELOG + README note (`010-single-source-constitution`) added during implement (release doc proof) — `CHANGELOG.md`, `README.md` — [covers code↔docs]
- [ ] T005 Run `gate run --profile normal` green; `doti payload check` + `render-skills --check` clean with the twin removed — [verifies FR-002, SC-002]
