# Plan Template

> Resolve unknowns and decide the design before code. The Constitution Check is a gate (before AND after design). Name the architecture rule deltas so the design is enforced, not just described.

## Technical Context

Approach, stack, dependencies, and constraints relevant to scaffold-dotnet. Mark anything undecided `[NEEDS CLARIFICATION]` and resolve it in Research — do not plan on an unproven premise.

## Constitution Check (gate)

Verdict against `.doti/memory/constitution.md` (Deterministic Ownership, Bootstrap Honesty, Template Boundary, Public Hygiene, Cross-Platform, Codified Cycle, Engineering Discipline) — evaluated BEFORE design and RE-EVALUATED after design. Record PASS, or a violation justified in Complexity Tracking. Do not proceed past an unjustified violation.

## Research (resolve unknowns)

For each unknown / dependency / integration:

- **Decision:** …
- **Rationale:** …
- **Alternatives rejected:** …

## Design

Technical approach and files likely to change. **Architecture delta:** projects / namespaces / layers added or moved, and the exact rule changes that encode it — the ArchUnitNET family in `rules/architecture.json` and the Sentrux layer / boundary in `.sentrux/rules.toml`, kept mutually consistent (`/doti-arch-review` validates both engines encode the same intent). A structural change without a matching rule change will drift.

## CLI surface & error contract

(Only if the feature adds or changes a CLI command/operation; omit this section otherwise.) For each new or changed command, declare:

- **Error codes** it emits — the stable `<PREFIX><NNNN>` diagnostics, registered in `errorcodes/registry.json` (frozen append-only by `errorcodes check`).
- **Exit class** — Success / Usage / Validation / Integrity / Internal.
- **`describe` entry** — the command/option/exit-class surface it adds to the capability model, so an agent learns it in one call.
- **Envelope** — returns the `CliResult` envelope, JSON-first (no direct console writes).
- **Channel boundary** — new behavior lives in a `*.Core` library; the CLI delta is wiring-only (parse → delegate to a named core type → render). Name the core type each new command delegates to. Enforced by the thin-CLI families (`cliSurfaceConfinement` / `cliDelegation`).

## Command Availability

| Area | Command | Status |
| --- | --- | --- |
| Restore | `dotnet restore .\scaffold-dotnet.slnx` | implemented |
| Build | `dotnet build .\scaffold-dotnet.slnx -c Release --no-restore /m:1` | implemented |
| Test | `dotnet test .\scaffold-dotnet.slnx -c Release --no-build /m:1` | implemented |
| Platform probe | `dotnet run --project tools/Hx.Runner.Cli -- platform probe` | implemented |
| Hygiene changed | `dotnet run --project tools/Hx.Runner.Cli -- hygiene scan --repo . --scope changed --source staged --json` | implemented; Gitleaks vendored win-x64 (other RIDs fail closed) |
| Sentrux verify | `dotnet run --project tools/Hx.Runner.Cli -- sentrux verify --repo . --json` | implemented; vendored win-x64 |
| Architecture | `dotnet run --project tools/Hx.Runner.Cli -- architecture test --repo . --json` | implemented |
| Gate | `dotnet run --project tools/Hx.Runner.Cli -- gate run --repo . --profile normal --json` | implemented |

Mark any planned-but-absent command advisory; never downgrade a planned gate.

## Complexity Tracking

Fill ONLY if the Constitution Check surfaced a violation that must be justified.

| Violation | Why needed | Simpler alternative rejected because |
| --- | --- | --- |

## Risks

Circular dependencies, drift risks, and any checks that are not deterministic proof yet.
