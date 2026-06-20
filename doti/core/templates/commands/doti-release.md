# doti-release

Purpose: guide release readiness once release tooling exists.

Command-aware advisory behavior:

1. Read `.doti/agent-context.md`.
2. The release lane is `gate run --profile release` (full tests + full hygiene + version calculation + the enforced security scan + proof); version bumps go only through `version bump`.
3. Once implemented, determine whether the release is major or minor by operator intent; patch releases need no bump command.
4. Use only `Hx.Runner.Cli version bump --major|--minor --repo . --json` for major or minor bumps.
5. Run the release lane and treat failures as blocking.

Expected output: release readiness notes or release proof after the lane exists.

## Next

Cycle complete - start the next feature with `/doti-specify`.
