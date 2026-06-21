# Spec: Deterministic vendored-tool fetch

> WHAT and WHY only. Today `Hx.Scaffold.Cli new` only *copies* `gitleaks` + `sentrux` from the source repo (never `gitversion`), and nothing downloads a tool from its pinned manifest. A fresh clone, a generated project, or CI is therefore left with missing tool binaries — most visibly `gitversion` — so the version/release gate cannot run. This feature adds a deterministic, hash-verified fetch so the vendored tools provision themselves.

## Goal

Make the scaffold's vendored tool dependencies (**gitleaks, sentrux, gitversion**) install themselves automatically and deterministically from their pinned manifests, so that a fresh clone, a generated project, or CI ends up with every required tool binary present and verified — closing the gap where `gitversion` is never vendored and copy-vendoring yields no binary when the source repo doesn't carry one.

## Scope

**Included**
- A provisioning command that, for each `tools/*/*.version.json` manifest and the host RID, downloads the asset from its `downloadUrl`, verifies its SHA-256 (archive and/or executable), and installs the executable at `executablePath` — fail-closed on mismatch.
- Coverage of **all three** vendored tools, including `gitversion` (currently omitted from vendoring entirely).
- Support for both raw-executable assets (no archive) and zip-archived assets.
- Wiring into `Hx.Scaffold.Cli new` so a generated project ends up with every manifest tool present (fetch any whose binary is missing after the existing copy step).
- Structured error codes for fetch failures, in the existing registry.

**Excluded**
- Adding tool assets for RIDs the manifests don't yet cover (only `win-x64` assets exist today — other RIDs report cleanly, not crash).
- Changing tool versions, the manifest schema, or introducing a package manager.
- Running the fetch inside the offline gate (fetch is a provisioning step; the gate stays offline-deterministic against already-present binaries).

## Functional Requirements

- `FR-001`: The system MUST provide a command that, for a tool manifest and the host RID, downloads the asset from `downloadUrl`, verifies `archiveSha256` when present and `executableSha256`, and installs the executable at the manifest's `executablePath`. A missing asset for the RID, a download failure, or a hash mismatch MUST fail closed (non-Success).
- `FR-002`: The fetch MUST handle a raw-executable asset (no archive — `archiveSha256` null, the download *is* the executable) and a zip-archived asset (extract `executableName`).
- `FR-003`: The fetch MUST cover **gitleaks, sentrux, and gitversion** (the manifests under `tools/`), and `gitversion` MUST become a vendored tool the scaffold provisions.
- `FR-004`: `Hx.Scaffold.Cli new` MUST ensure every manifest tool is present in the generated project — fetching any whose binary is absent after the existing copy step — so generated projects carry a working `gitversion` (and `gitleaks`/`sentrux`) without a manual step.
- `FR-005`: When no asset exists for the host RID, the command MUST report it cleanly (skipped/blocked per the established "other RIDs fail closed" model), never throw.
- `FR-006`: Fetch failures MUST surface stable structured error codes (e.g. RID-unsupported, download failure, archive-hash mismatch, executable-hash mismatch, extraction failure) registered in `errorcodes/registry.json` and frozen by `errorcodes check`.
- `FR-007`: The command MUST render the `CliResult` envelope and appear in `describe`.

## Success Criteria

- `SC-001`: On a `win-x64` checkout missing the binaries, running the fetch leaves all three executables present at their `executablePath` with matching SHA-256, and `gate run` no longer blocks on tool verification.
- `SC-002`: A project produced by `new` contains a working `gitversion` (plus `gitleaks` and `sentrux`) binary with no manual step.
- `SC-003`: A tampered or wrong-hash download fails closed with a structured error code — never installs an unverified binary.
- `SC-004`: Build and the full test suite stay green on Windows, Linux, and macOS (the verify/extract logic is RID-agnostic and unit-tested with fixtures; tests perform no network I/O).

## Deterministic Surfaces

- New: the tool-fetch command (`dotnet run --project tools/Hx.Runner.Cli -- …`) and a `ToolFetcher` core; new entries in `errorcodes/registry.json` (+ `errorcodes check`).
- Existing: `tools/*/*.version.json` manifests (the pinned source of `downloadUrl` + SHA-256 + `executablePath`); `ToolVendor` / `ScaffoldNewRunner` (the `new` wiring point); `describe`; `gate run`'s tool-verification steps (the consumer of a provisioned binary).

## Architecture Impact

A `ToolFetcher` in `Hx.Runner.Core` (network download + hash verify + zip extraction) and a thin CLI command in `Hx.Runner.Cli`; new error-code registry entries. No new project/namespace/layer. `Hx.Runner.Core` already performs network I/O (`GitleaksUpdateChecker`), so no architecture-rule delta in `rules/architecture.json` or `.sentrux/rules.toml`.

## Sentrux And Hygiene Impact

None of concern. The fetched binaries are gitignored (never committed); manifests are pinned + hashed; no secrets. The fetch is opt-in/provisioning, not part of the scan or the offline gate.

## Assumptions

- Each tool manifest carries a `downloadUrl` + SHA-256 for the active RID — confirmed for `win-x64` (sentrux = raw `.exe`; gitversion = `.zip`; gitleaks per its manifest).
- `win-x64` is the active target; other RIDs have no manifest assets yet and are reported, not fetched.
- Network is available at fetch/provision time; the offline `gate run` still verifies already-present binaries.
- Command name and exact error-code prefix are design choices for the plan (recorded there), not blocking ambiguities.

## Acceptance

- **Command-backed:** the fetch command (new); `errorcodes check`; build + test on CI.
- **Advisory / network:** the actual download is a network provisioning step run on demand (or by `new`), outside the offline gate; success is observed by the binaries being present + verified (SC-001/SC-002).

## Clarifications

(Populated by `/doti-clarify`.)
