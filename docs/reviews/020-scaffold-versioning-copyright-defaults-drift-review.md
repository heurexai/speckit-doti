# Drift Review — Feature 020: Scaffold versioning + copyright defaults

**Stage:** `/08-doti-drift-review`. Change set (implement diff): `scaffold/templates/dotnet-cli/GitVersion.yml` (new), `scaffold/templates/dotnet-cli/Directory.Build.props`, `test/Hx.Templates.Tests/TemplateGoldenTests.cs`, `CHANGELOG.md`, `README.md`, the tasks ledger. **Generated-template config + test + docs — no `*.Core`/`*.Cli`, no contract, no rule/proof change.**

## Axis 1 — spec ↔ code (PASS)

- **FR-001 / FR-004 / FR-005** (ship a non-GitFlow, 0.1.0-start config): `scaffold/templates/dotnet-cli/GitVersion.yml` exists with `workflow: GitHubFlow/v1` + `next-version: 0.1.0`. The explicit `workflow:` line is the enforcing mechanism — it overrides GitVersion's GitFlow default so a `dev` branch is not next-minor.
- **FR-002** (trunk default = Patch): `GitHubFlow/v1` sets `main` increment Patch by default; a bug-fix-only cycle writes no `+semver` signal → patch. Verified live in two generated repos (ergon v0.1.0→v0.1.1; agentx `hx version calculate` → 0.1.0).
- **FR-003** (feature → minor preserved): no change to the doti `+semver: minor` release-transition mechanism (`CycleService.ReleaseSemverSignal` untouched); the config composes with it. Nothing downgraded.
- **FR-006 / FR-007 / FR-008** (auto-year company copyright): `Directory.Build.props` gains `<Copyright>Copyright © $([System.DateTime]::UtcNow.Year) $(Company)</Copyright>` — build-time year (= release-build year), holder from `$(Company)` (the `--company` symbol).
- **FR-009** (regression guard): two golden tests (`Template_ships_trunk_based_versioning_config`, `Template_ships_auto_year_company_copyright`) assert both defaults.

Matches the approved plan (build-time copyright, `GitHubFlow/v1`, no `*.Core` change, no rule delta). No logic landed in a CLI project (no `.cs` production change at all). The spec is satisfied by real enforcing mechanisms, not prose.

## Axis 2 — code ↔ docs (PASS)

- The generated-repo behavior change is documented: `CHANGELOG.md` (`[Unreleased]` → `020-…` entry) and `README.md` (the "Latest cycle" line) both describe the new versioning + copyright defaults and carry the full `020-scaffold-versioning-copyright-defaults` slug (release-doc proof).
- No code symbol was added, removed, or renamed (the diff has no `*.cs` production change), so no `hx describe`/`--help`/agent-context (`CLAUDE.md`/`AGENTS.md`/`.doti/agent-context.md`) claim is made stale — those describe `hx` capabilities, which are unchanged; the template's internal config files are not enumerated there. Grep confirms no removed symbol survives in any doc.

## Axis 3 — source ↔ installed (PASS)

- `hx doti render-skills --check` → no skill/payload drift (93 managed files); `hx doti payload check` → parity passed (93 files). Expected: the change touches `scaffold/templates/**` (generated-template content) + a test + docs — none are `.doti` rendered/installed assets. No skill/template/profile *source* under `.doti/core` was edited, so nothing to re-render.

## Gate

`hx gate run --profile normal` green over the staged change set (build + affected `Hx.Templates.Tests` incl. the two new golden tests + the full ladder: hygiene, Gitleaks/Sentrux verify, affected-test, task-completion, architecture, skill-drift, payload parity, Sentrux check, version). No code, rule, limit, or proof change.

## Note — coverage boundary (disclosed)

SC-002's *live* end-to-end behavior (a generated repo's actual patch/minor increment) runs only under `HX_TEMPLATE_ROUNDTRIP=1`; the default gate verifies the deterministic golden assertions on the shipped template config. The live behavior is independently proven in ergon/agentx. Reported honestly, consistent with the spec/plan/tasks.

## Verdict

**No open drift** in any applicable axis. A template-content + test + docs change that ships two correct generated-repo defaults and closes the GitFlow-default minor-bump defect, with no production-code, contract, rule, or proof change. Ready for `/09-doti-release` (feature cycle → **minor** → v0.13.0).
