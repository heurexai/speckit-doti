# doti-drift-review

Purpose: detect drift between source assets, installed bootstrap files, and docs.

Command-aware advisory behavior:

1. Read `.doti/agent-context.md`.
2. Use `Hx.Runner.Cli doti render-skills --check` as the authority for skill / agent-context drift.
3. Check root `AGENTS.md` and `CLAUDE.md` remain thin entrypoints.
4. Treat hand-edited installed skills as drift.
5. Run `gate run --profile normal --repo .` and treat any gate failure as blocking before handing off to commit. Direct `dotnet test` output is diagnostic only; the persisted gate proof is the commit-authorizing evidence.
6. On a clean review, stamp the stage (`doti cycle stamp --stage drift-review`) so the commit chokepoint can verify it, then confirm commit-readiness with `doti cycle check --stage commit` (fail-closed: every prerequisite stamped + fresh). The stamp refuses stale or missing prerequisites.

Expected output: drift findings and the authoritative remediation path.

## Next

Run `/doti-commit` to prepare the scoped commit.
