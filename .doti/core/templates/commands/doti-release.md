# doti-release

Purpose: run release readiness proof and produce command-backed local release output when configured.

Command-backed behavior:

1. Read `.doti/agent-context.md`.
2. The release lane is `gate run --profile release` (full tests + full hygiene + version calculation + the enforced security scan + proof).
3. Determine the release intent from operator direction: `--major`, `--minor`, or `--patch` (patch is the default for routine fixes).
4. Stamp the release stage with the same intent before running `hx release`: `doti cycle stamp --stage release --release-intent <major|minor|patch> --repo . --json`. This finalizes `drift-review` through the coded transition commit and adds the matching GitVersion `+semver:` signal without an agent-authored commit.
5. Run the release lane and treat failures as blocking.
6. Confirm the running `hx` has an executable-adjacent `hx.config.json`; operational `hx` commands fail hard without it. Local release output is controlled only by that Microsoft Configuration file: `localReleaseOutput.enabled` and `localReleaseOutput.directory` (absolute when enabled).
7. Confirm `.doti/release.json` declares the product being released: package name, publish `.csproj`, published executable name, and output executable name. A vendored `hx` must publish this target product, not `tools/Hx.Scaffold.Cli`.
8. Produce local release output with `Hx.Scaffold.Cli release --repo . --major|--minor|--patch --json`; do not pass release-root flags because local output is configured by executable-local `hx.config.json`.
9. Accept only the `LocalReleaseResult` envelope as proof of the local copy: it must report the release intent, local tag, GitVersion identity, Velopack package artifacts, payload checks, install-location proof when produced, config source/path, version directory, latest directory, artifact checksums, or the explicit disabled-copy reason.
10. Do not push tags from `hx release`; after the local release proof is accepted, push the reported tag through the release workflow and verify GitHub CI/release artifacts.

Expected output: release gate proof plus the `hx release` result showing the local tag, release intent, Velopack artifacts, install-location proof, config source/path, version directory, latest directory, checksums, or the explicit disabled-copy reason.

## Next

Cycle complete. Start the next feature with `/01-doti-specify`.
