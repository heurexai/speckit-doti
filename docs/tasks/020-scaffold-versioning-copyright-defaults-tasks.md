# Tasks — 020 Scaffold versioning + copyright defaults

**Plan:** [docs/plans/020-scaffold-versioning-copyright-defaults-plan.md](../plans/020-scaffold-versioning-copyright-defaults-plan.md). **Stage:** `/04-doti-tasks`. Template-content + test change; no `*.Core`/`*.Cli` code, no rule delta.

## Phase 1 — Setup / premise check

- [ ] T001 Confirm a new root config file is instantiated + packed: `scaffold/templates/dotnet-cli/.template.config/template.json` does not exclude a root `GitVersion.yml` from instantiation, and `scaffold/Hx.Scaffold.Templates.csproj` globs `templates\**\*` (so the new file is packed) — `scaffold/templates/dotnet-cli/.template.config/template.json`, `scaffold/Hx.Scaffold.Templates.csproj` — [premise for FR-001]

## Phase 2 — WI1 Versioning defaults (Priority: P1) 🎯 MVP

**Goal:** a generated repo starts at 0.1.0 and increments patch (bug) / minor (feature) with no operator-authored config. **Independent test:** `hx version calculate` on a generated repo → 0.1.0; the golden test pins the shipped config.

- [ ] T002 [US1] Golden test asserting `scaffold/templates/dotnet-cli/GitVersion.yml` is present and pins `workflow: GitHubFlow/v1` + `next-version: 0.1.0` (write first; MUST fail before T003) — `test/Hx.Templates.Tests/TemplateGoldenTests.cs` — [covers FR-001, FR-004, FR-005, FR-009, SC-001, SC-004]
- [ ] T003 [US1] Add `scaffold/templates/dotnet-cli/GitVersion.yml` — `workflow: GitHubFlow/v1`, `next-version: 0.1.0`, `assembly-*-scheme: MajorMinorPatch`, `assembly-informational-format: '{MajorMinorPatch}'`, `ignore.sha: []` — `scaffold/templates/dotnet-cli/GitVersion.yml` — [covers FR-001, FR-002, FR-003, FR-004, FR-005, SC-002]

**Checkpoint:** `gate run --profile normal` green over the change set.

## Phase 3 — WI2 Auto-year copyright (Priority: P2)

**Goal:** a generated repo's release assembly carries `Copyright © <release-year> <Company>`. **Independent test:** a Release build of a generated repo emits `AssemblyCopyrightAttribute` with the current year + company.

- [ ] T004 [US2] Golden test asserting `scaffold/templates/dotnet-cli/Directory.Build.props` contains a `<Copyright>` using the build-time year function `$([System.DateTime]::UtcNow.Year)` + `$(Company)` (write first; MUST fail before T005) — `test/Hx.Templates.Tests/TemplateGoldenTests.cs` — [covers FR-006, FR-007, FR-008, FR-009, SC-003, SC-004]
- [ ] T005 [US2] Add `<Copyright>Copyright © $([System.DateTime]::UtcNow.Year) $(Company)</Copyright>` immediately after `<Company>` in `scaffold/templates/dotnet-cli/Directory.Build.props` — `scaffold/templates/dotnet-cli/Directory.Build.props` — [covers FR-006, FR-007, FR-008]

**Checkpoint:** `gate run --profile normal` green over the change set.

## Phase 4 — Polish & release docs

- [ ] T006 Note the new generated-repo defaults (0.1.0 start + patch-default/minor-on-feature versioning; auto-year company copyright) in `CHANGELOG.md` and the scaffold section of `README.md` — `CHANGELOG.md`, `README.md` — [covers SC-001, SC-003 docs hygiene]
- [ ] T007 `gate run --profile release` green over the full change set; stamp implement on green — [covers SC-002, SC-004]

## Dependencies & Execution Order

Phases sequential. Within each user story, the test (T002 / T004) is written before its implementation (T003 / T005) and must fail first. T001 is a read-only premise check. T006–T007 are last.

## Coverage

- FR-001 → T002, T003 | FR-002 → T003 | FR-003 → T003 | FR-004 → T002, T003 | FR-005 → T002, T003 | FR-006 → T004, T005 | FR-007 → T004, T005 | FR-008 → T004, T005 | FR-009 → T002, T004
- SC-001 → T002, T003, T006 | SC-002 → T003, T007 (config-level; the live round-trip is `HX_TEMPLATE_ROUNDTRIP`-gated, advisory) | SC-003 → T004, T005, T006 | SC-004 → T002, T004, T007

## Gate Notes

`gate run` is the per-phase checkpoint (`--profile normal` between phases, `--profile release` before release). SC-002's live end-to-end behavior (a generated repo's actual patch/minor increment) is exercised only under `HX_TEMPLATE_ROUNDTRIP=1`; the default gate verifies the deterministic golden assertions on the shipped template config — noted honestly as the coverage boundary.
