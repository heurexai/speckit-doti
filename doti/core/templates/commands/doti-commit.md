# doti-commit

Purpose: prepare a scoped commit for scaffold-dotnet work.

Command-aware advisory behavior:

1. Read `.doti/agent-context.md`.
2. Verify the worktree scope is limited to the intended task.
3. Run available deterministic checks that match the change risk.
4. Run changed-file hygiene: `dotnet run --project tools/Hx.Runner.Cli -- hygiene scan --repo . --scope changed --source staged --json`. It scans only staged blobs; scaffold-specific checks always run, and Gitleaks secret scanning is vendored for win-x64 (other RIDs fail closed).
5. Do not run full hygiene (`--scope all`) on ordinary commits; reserve it for release readiness, first scaffold smoke, and explicit audits.
6. Confirm the docs are current for the change.
7. Commit only after staged files match the intended scope.
8. **Codified commit (the only path).** Commit through `Hx.Runner.Cli doti cycle commit --message <m>` — it re-verifies the prerequisite chain (a fresh drift-review + the task-hash, via `cycle check`), a fresh passing gate proof with recomputable affected-test planner/test-scope/execution hashes (run `gate run` first so it is persisted), and a clean staged scope, then commits; it refuses otherwise. **Do not `git commit` by hand** — the insurance pre-commit hook (`doti install-hooks`) redirects you here. Direct `dotnet test` transcripts are not commit proof.

Expected output: commit-ready summary with checks and advisory gaps.

## Next

Run `/doti-release` to cut a version, or `/doti-specify` to start the next feature.
