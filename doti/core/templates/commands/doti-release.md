# doti-release

Purpose: run release readiness proof and produce command-backed local release output when configured.

Command-backed behavior:

1. Read `.doti/agent-context.md`.
2. The release lane is `gate run --profile release` (full tests + full hygiene + version calculation + the enforced security scan + proof).
3. Determine the release intent from operator direction: `--major`, `--minor`, or `--patch` (patch is the default for routine fixes).
4. Run the release lane and treat failures as blocking.
5. Confirm `.doti/release.json` declares the product being released: package name, publish `.csproj`, published executable name, output executable name, and default release-root environment variable. A vendored `hx` must publish this target product, not `tools/Hx.Scaffold.Cli`.
6. Produce local release output with `Hx.Scaffold.Cli release --repo . --major|--minor|--patch --json`. Use `--release-root <path>` for an explicit local store, `--release-root-env <name>` to read or save a non-default variable, and `--save-release-root` only when the operator explicitly wants the supplied root persisted. The default environment variable comes from `.doti/release.json`.
7. Accept only the `LocalReleaseResult` envelope as proof of the local copy: it must report the release intent, local tag, GitVersion identity, Velopack package artifacts, payload checks, resolved root source, version directory, latest directory, artifact checksums, or the explicit skipped-copy reason.
8. Do not push tags from `hx release`; after the local release proof is accepted, push the reported tag through the release workflow and verify GitHub CI/release artifacts.

Expected output: release gate proof plus the `hx release` result showing the local tag, release intent, Velopack artifacts, resolved root source, version directory, latest directory, checksums, or the explicit skipped-copy reason.

## Next

Cycle complete - start the next feature with `/doti-specify`.
