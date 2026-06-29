# Contributing to speckit-doti

Thanks for contributing! speckit-doti is MIT-licensed and uses a lightweight **DCO** (Developer Certificate of Origin) — there is **no CLA**.

## Quick start

1. Fork the repo and create a branch off `main`.
2. Make your change. The architecture is enforced (see [The gate](#the-gate)), so keep the layering intact — a pure domain library that depends on nothing, a CLI that may depend on the library.
3. Verify locally:
   ```bash
   dotnet build scaffold-dotnet.slnx -c Release
   dotnet test  scaffold-dotnet.slnx -c Release
   ```
4. Sign off your commits (see below) and open a pull request.

## Sign your commits (DCO)

By signing off you certify the [Developer Certificate of Origin](DCO): that you wrote the change (or have the right to submit it) under the MIT license. No separate agreement to sign.

Add a sign-off line to **every** commit:

```bash
git commit -s -m "your message"
```

That appends `Signed-off-by: Your Name <your@email>` — it must match your commit author. Forgot on some commits? Sign off the whole series:

```bash
git rebase --signoff main
```

The **DCO** check on your PR must pass before merge.

## The gate

CI runs **build + the full test suite** on every PR. That suite includes the **ArchUnitNET architecture families**, so a layering or boundary violation fails the build — not a review comment. Your PR needs the green check to merge.

Locally, you can run the full deterministic ladder (hygiene, secret scanning, Sentrux boundary analysis, build/test, security, versioning) aggregated into one fail-closed proof:

```bash
dotnet run --project tools/Hx.Runner.Cli -- gate run --repo . --profile auto --json
```

> The full `gate run` uses pinned vendored tool manifests (Gitleaks / Sentrux / GitVersion) and hash-verified binaries fetched operationally per `tools/*/*.version.json`. CI currently enforces build + architecture + unit tests; vendored-tool steps run locally for the host RIDs declared by the manifests (see the README **Status**).

## doti workflow (optional for contributors)

This repo is built with its own spec-driven workflow, **doti**. You don't have to run the full cycle for a contribution — a focused change with a green gate is enough. For larger features the maintainers may run the doti cycle (`specify → … → commit`) on merge. See the README for how it works.

## Merging

Contributor PRs are **squash-merged** into the working branch after a green gate and maintainer approval. Keep each PR focused on one change.

**Branch flow (maintainers).** `dev` is the permanent working branch; `main` is the protected release branch. Feature work lands on a short-lived `feat/NNN-*` branch off `dev` (never directly on `dev`, whose ref moves) and is **squash-merged** into `dev`. Releases promote `dev` to `main` with a **merge commit** (not a squash) — squashing `dev → main` rewrites history and diverges the two branches, so the release path always merges. The push to `main` triggers `release.yml` (GitVersion → tag → pack → NuGet Trusted Publishing → GitHub Release); release `v*` tags are created by that CI on the merge, never hand-pushed.

## Reporting issues

Open an issue for bugs and feature requests using the issue templates. For anything **security-sensitive**, follow [SECURITY.md](SECURITY.md) rather than filing a public issue.

For general help, see [SUPPORT.md](SUPPORT.md). All project spaces follow the [Code of Conduct](CODE_OF_CONDUCT.md).
