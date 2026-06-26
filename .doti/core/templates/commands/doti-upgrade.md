# doti-upgrade

Purpose: drive both update planes in one operator action — update the installed `hx` tool (Plane A) and reconcile this repo's `.doti` assets (Plane B) — without clobbering operator-modified files.

Command-backed behavior:

1. Read `.doti/agent-context.md`.
2. Record the current tool version and channel: `hx version --json` (note the running version + the active distribution channel).
3. Plane A — update the tool on this machine. For the NuGet global-tool channel: `dotnet tool update -g Heurex.SpeckitDoti`. For a Microsoft Store / MSIX install, update through the Store instead (the package install dir is read-only). This plane updates the tool binary on the machine, not this repo.
4. Detect whether a newer tool was installed: run `hx version --json` again and compare to step 2 (report `already current` if unchanged).
5. Plane B — reconcile THIS repo's `.doti`: `hx doti install --repo . --json`. The reconciliation is version-aware and preserves operator edits (FR-015): a modified managed asset is preserved and the bundled version is staged as a `<file>.new` sidecar; an operator-deleted managed asset is not resurrected without `--force`; a repo whose recorded `.doti/payload.json` is ahead of the bundled payload is refused (`Integrity_DotiRepoPayloadAhead`).
6. Report: the tool transition `vX -> vY` (or `already current`), and the repo reconciliation result from the `DotiInstallResult` envelope — installed / migrated / preserved / blocked / skipped counts — and explicitly list any `.new` sidecars the operator should review and merge.

Expected output: the tool version transition plus the repo reconciliation result (install / migrate / preserve / block / skip counts and any `.new` sidecars) from the `CliResult` envelope.

## Next

Resume your active cycle stage, or start a feature with `/01-doti-specify`.
