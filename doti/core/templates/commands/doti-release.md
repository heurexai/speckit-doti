# doti-release

Purpose: run release readiness proof and produce command-backed local release output when configured.

Command-backed behavior:

1. Read `.doti/agent-context.md`.
2. The release lane is `gate run --profile release` (full tests + full hygiene + version calculation + the enforced security scan + proof); version bumps go only through `version bump`.
3. Determine whether the release is major or minor by operator intent; patch releases need no bump command.
4. Use only `Hx.Runner.Cli version bump --major|--minor --repo . --json` for major or minor bumps.
5. Run the release lane and treat failures as blocking.
6. Confirm `.doti/release.json` declares the product being released: package name, publish `.csproj`, published executable name, output executable name, and default release-root environment variable. A vendored `hx` must publish this target product, not `tools/Hx.Scaffold.Cli`.
7. Produce local release output with `Hx.Scaffold.Cli release --repo . --json`. Use `--release-root <path>` for an explicit local store, `--release-root-env <name>` to read or save a non-default variable, and `--save-release-root` only when the operator explicitly wants the supplied root persisted. The default environment variable comes from `.doti/release.json`.
8. Accept only the `LocalReleaseResult` envelope as proof of the local copy. Manual file copying is not release proof.

Expected output: release gate proof plus the `hx release` result showing the resolved root source, version directory, latest directory, artifacts, checksums, or the explicit skipped-copy reason.

## Next

Cycle complete - start the next feature with `/doti-specify`.
