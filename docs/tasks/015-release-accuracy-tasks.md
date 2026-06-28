# Tasks — 015 Release accuracy

**Plan:** [docs/plans/015-release-accuracy-plan.md](../plans/015-release-accuracy-plan.md). **Stage:** `/04-doti-tasks`.

Docs + CI-config only. No code, no contract, no proof change.

## Phase 1 — README skill documentation (WI-1)

- [ ] T001 Replace the two stand-alone paragraphs after the numbered-stage table in `README.md` with an *Unnumbered utility skills* subsection: a lead-in (run outside/anytime in the cycle, never reorder `/01`–`/09`, single-sourced in `.doti/core/skills.json`); a fuller `/doti-constitution` block (the §1 inherited/codified vs §2 operator-authored — domain, tech stack, coding style, security, performance — split; `/03-doti-plan` + `/06-doti-arch-review` re-read §2 fresh via `hx doti constitution`; authored/amended with `/doti-constitution`, tracked by the cycle + git history); then a compact table of the remaining six (`doti-auto`, `doti-bug`, `doti-amend`, `doti-drift-fix`, `converge`, `doti-upgrade`) with a one-line purpose each — `README.md` — [covers FR-001, FR-002, FR-003]

## Phase 2 — Release CI tool provisioning (WI-2)

- [ ] T002 In `.github/workflows/release.yml` `pack-and-smoke`, after `dotnet tool install` + `export PATH` and before `hx new`, add a step that derives the installed payload root from `hx version --json` (`.data.prerequisites.manifestPath` minus the `/.doti/core/prerequisites.json` suffix) and runs `hx tools fetch --repo "$payload" --tool all --json` (host RID), so the generated repo's first-smoke verifies the vendored tools instead of failing-closed; the fetch is hash-verified + fail-closed — `.github/workflows/release.yml` — [covers FR-004, FR-005]

## Phase 3 — Verify

- [ ] T003 `doti render-skills --check` + `doti payload check` clean (the README change touches no rendered asset; the skills stay single-sourced); `gate run --profile normal` green over the change set; stamp implement on green. The CI smoke pass is observed on the v0.12.1 tag push (not locally reproducible) — `README.md`, `.github/workflows/release.yml` — [covers SC-003, SC-004]

## Coverage

- FR-001/002/003 → T001 | FR-004/005 → T002 | SC-001/002/003 → T001 | SC-004/005 → T002, T003.
