# Tasks: Single-Source the Constitution

Plan: `docs/plans/010-single-source-constitution-plan.md`. Spec: `docs/specs/010-single-source-constitution.md`. **Priority mode = workflow/tooling: safety-first** — keep parity green. Phases sequential; T004 is the final gate.

## Phase 0 — Single-source the constitution — Checkpoint: `gate run` green

- [x] T001 [test] Single-source invariant: `.doti/core/memory/constitution.md` does NOT exist; `.doti/memory/constitution.md` exists, has `## §1` + `## §2`, and zero placeholder tokens — `test/Hx.Doti.Tests/ConstitutionTests.cs` — [covers FR-001, SC-001] <!-- doti-task-hash: 912bf7ce71a23af6957bca6c5fba73ada96d430c8bed41a8f5f245e1f0bdc669 -->
- [x] T002 Delete `.doti/core/memory/constitution.md` (`git rm`); `.doti/memory/constitution.md` untouched — `.doti/core/memory/constitution.md` — [covers FR-001] <!-- doti-task-hash: 5cbf4e09d633a918af3ab0d2223fb6a5048be13ea95a236f6736b4ccf00e0aff -->
- [x] T003 Drop the redundant `;$(RepoRoot).doti/core/memory/constitution.md` segment from the `_HxDoti` `Exclude` glob (keep the `.doti/memory/constitution.md` exclusion) — `tools/Hx.Scaffold.Cli/Hx.Scaffold.Cli.csproj` — [covers FR-002] <!-- doti-task-hash: 510585b8819d566110eb8724a8979eaaf45b426d9be375e50cb8594062ddc8d0 -->

## Phase 1 — Docs + verification — Checkpoint: `gate run` green

- [x] T004 CHANGELOG + README note (`010-single-source-constitution`) added during implement (release doc proof) — `CHANGELOG.md`, `README.md` — [covers code↔docs] <!-- doti-task-hash: beb7052c103fc263b7fec94c0531aa883ab0197b4128c0c2d013ad6ab1442076 -->
- [x] T005 Run `gate run --profile normal` green; `doti payload check` + `render-skills --check` clean with the twin removed — [verifies FR-002, SC-002] <!-- doti-task-hash: 2834289d0d7acb50a72e47fcc7f42541aa06ffe8ee608cb2d4fbd22b1235811a -->
