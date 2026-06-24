# Plan: Local release output directory

## Technical Context

The scaffold CLI (`Hx.Scaffold.Cli`) is the standalone `hx` binary shipped to operators. Release archives are currently assembled in `.github/workflows/release.yml`, but local release output was not command-backed. The feature adds a local `hx release` command that calculates the GitVersion-backed version, publishes the standalone `hx`, assembles the release archive/checksum/identity metadata, and copies the verified artifact set into a configured local release store.

## Constitution Check (gate)

PASS. The design keeps release mutation deterministic and command-backed, preserves the template/source boundary, does not introduce shell runners, and keeps machine-specific release roots out of tracked files.

## Research

- **Decision:** Implement the operator-facing release command on `Hx.Scaffold.Cli` as `hx release`.
- **Rationale:** `hx.exe` is the shipped standalone binary; operators need the local release behavior without relying on an agent to copy files.
- **Alternatives rejected:** Runner-only `doti release` was rejected because it would not be directly available from the shipped standalone `hx.exe`.

- **Decision:** Use `DOTI_RELEASE_ROOT` as the default environment variable and `--release-root-env <name>` as the override.
- **Rationale:** This matches operator clarification and keeps the root source explicit in the result/proof.
- **Alternatives rejected:** Hard-coded local paths were rejected because they would leak machine-specific paths into behavior and docs.

- **Decision:** Save an explicit root only when `--save-release-root` is supplied.
- **Rationale:** Persisting environment variables is a side effect that must be opt-in and auditable.
- **Alternatives rejected:** Automatically saving any explicit root was rejected as surprising state mutation.

## Design

Files changed:

- `src/Hx.Scaffold.Core/Release/LocalReleaseRootResolver.cs` owns root-source selection and environment-variable-name validation.
- `src/Hx.Scaffold.Core/Release/LocalReleaseService.cs` owns packaging, checksum verification, local copy publication, and opt-in Windows user environment persistence.
- `tools/Hx.Scaffold.Cli/ScaffoldCommandFactory.cs` adds the `release` command and options.
- `tools/Hx.Scaffold.Cli/ScaffoldCommands.Release.cs` maps usage/validation failures into the shared `CliResult` envelope.
- `tools/Hx.Tooling.Contracts/LocalReleaseResult.cs` defines the machine-readable release-copy proof.
- `test/Hx.Scaffold.Tests/LocalReleaseRootTests.cs` covers option precedence, default environment lookup, validation, and save-without-root.

Architecture delta: the core behavior lives in `Hx.Scaffold.Core.Release`; CLI code remains parse/delegate/render. No new ArchUnit family is required because the existing thin CLI confinement applies.

## CLI surface & error contract

- **Command:** `hx release --repo <path> [--rid <rid>] [--release-root <path>] [--release-root-env <name>] [--save-release-root] [--json]`
- **Error codes:** existing `USG0001` for invalid option combinations and invalid environment-variable names; existing `VAL0001` for release validation, packaging, path-safety, checksum, git, dotnet, or persistence failures.
- **Exit class:** Success for produced or explicitly skipped local copy; Usage for invalid command arguments; Validation for unsafe paths or failed packaging/copy/persistence.
- **`describe` entry:** exposes `release`, `--release-root`, `--release-root-env`, and `--save-release-root`.
- **Envelope:** returns `LocalReleaseResult` in the standard `CliResult` envelope.
- **Channel boundary:** `ScaffoldCommands.Release` delegates to `LocalReleaseService`.

## Command Availability

| Area | Command | Status |
| --- | --- | --- |
| Restore | `dotnet restore .\scaffold-dotnet.slnx` | implemented |
| Build | `dotnet build .\scaffold-dotnet.slnx -c Release --no-restore /m:1` | implemented |
| Test | `dotnet test .\scaffold-dotnet.slnx -c Release --no-build /m:1` | implemented |
| Local release copy | `dotnet run --project tools/Hx.Scaffold.Cli -- release --repo . [--release-root <path>] [--release-root-env <name>] [--save-release-root] --json` | implemented |
| Release gate | `dotnet run --project tools/Hx.Runner.Cli -- gate run --repo . --profile release --json` | implemented |

## Complexity Tracking

None.

## Risks

- `hx release` packages from committed `HEAD` via `git archive`, so uncommitted working-tree changes are not included. The doti cycle must commit before the final release artifact is accepted.
- `--save-release-root` is Windows user-environment persistence only; non-Windows behavior fails closed with a validation diagnostic.
- The command emits a local zip archive for the selected RID. The existing GitHub release workflow remains the authority for cross-platform GitHub asset publication.
