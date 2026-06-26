---
name: 01-doti-specify
description: Create or refine a scaffold-dotnet specification in command-aware-advisory mode.
compatibility:
  - claude
metadata:
  source: .doti/core/templates/commands/doti-specify.md
  maturity: command-aware-advisory
argument-hint: "[feature or goal]"
user-invocable: true
disable-model-invocation: false
---
# 01-doti-specify

Read `.doti/agent-context.md`, then follow `.doti/core/templates/commands/doti-specify.md`.

## Asking the operator a question (required format)

Any operator-facing question — at any stage, for Claude or Codex — uses this fixed format, presented immediately before the question:

- **Context** — the full background. Assume the operator has not re-read the material; restate what is unclear and the relevant facts.
- **Why it matters** — the concrete impact and why it needs a decision now.
- **Options** — for each option: **Pros**, **Cons**, and **Consequence** (what becomes true downstream if chosen).
- **Recommendation** — the option you recommend and the reasoning; list it first and label it "(Recommended)".
- **Assumptions** — the assumptions behind the recommendation.
- **Confidence** — High / Medium / Low, with a one-line reason.

Evidence requirement: every question, option, recommendation, and assumption must rest on verified facts. Verify each premise first (read the code, run the tool, observe the output) and cite that evidence; never present options whose premises you have not confirmed. An assumption is allowed only when the fact cannot be obtained from this environment — then label it UNVERIFIED, state why it cannot be verified and what would verify it. Prove claims (reproduce/RCA); never assert them. Do not use "assume" to defer work you could do — an unproven premise is a defect that can ruin the design.

Command availability: Hygiene scanning, restore/build/test, the bootstrap CLI, the platform probe, the Doti renderer, the `dotnet new` template pack, `Hx.Scaffold.Cli new`/`version`/`release`/`prereq`, `doti install`/`doti payload check`/`doti install-hooks`, the architecture gate, `gate run`, the affected-test planner (`Hx.Impact.Cli plan`), GitVersion versioning (`version calculate`; release tags are created only by `hx release --major|--minor|--patch`), and the security gate (`security scan` — package-vulnerability SCA via `dotnet list package --vulnerable`, plus the build-integrated analyzer SAST status; enforced at release, advisory in dev) are all available. Operational `hx` commands require an executable-adjacent `hx.config.json`; `hx release` validates that Microsoft Configuration file before inspecting the target repo, then reads `.doti/release.json` so a vendored hx publishes the target repo's declared product executable instead of requiring `tools/Hx.Scaffold.Cli` in that repo. It validates the requested release intent, creates or verifies the local annotated GitVersion tag, packs the declared product as a framework-dependent .NET global tool (`dotnet pack`) with a source-free install smoke, records the Microsoft Store MSIX channel proof plus payload/package checks and config source in `LocalReleaseResult` and `release.identity.json`, and copies artifacts to the configured local release root when `localReleaseOutput.enabled` is true. Installed `hx` updates via its channel (`dotnet tool update -g Heurex.SpeckitDoti`, or the Microsoft Store); repo asset install/repair remains `doti install`; scaffold payload parity is enforced by `doti payload check` and the gate. The scaffold CLI uses a trusted prerequisite manifest for .NET SDK/Git/directory preflight; Windows automatic remediation is only through `hx prereq install` with release-defined winget package/source metadata and an exact operator-approved plan digest. The .NET security analyzers + Gitleaks are the SAST gate. Secret scanning is the vendored Gitleaks; Sentrux is pinned to the Heurex fork v0.5.11 for declared RIDs with matching grammars; GitVersion is vendored for win-x64 (large binary gitignored, an operational vendor step). The build is fully green on win-x64; other undeclared tool RIDs fail closed. The doti cycle-state substrate (`doti cycle stamp`/`status`: diff-bound stage proofs + freshness detection) and the single-sourced Operator-Question Protocol render into every skill + the agent context. `cycle stamp` now fails closed when a non-initial stage's prerequisites are missing, stale, or invalid, and when the first-stage feature slug is not numbered (`NNN-short-name`); when stamping release, `--release-intent` writes the matching GitVersion `+semver:` signal into the coded drift-review transition commit. The enforcing chokepoints are `doti cycle check` (fail-closed prerequisites), `doti question check` (the operator-question validator), and an untracked insurance pre-commit hook (`doti install-hooks`). Commits are owned by coded Doti workflow transitions and release paths, not by an agent-visible commit command. `new` and `doti install` auto-arm or refresh the Doti hook for Git repos and refuse to overwrite non-Doti pre-commit hooks. `gate run` persists a change-set-bound proof. Direct `dotnet test` remains useful for diagnosis but cannot satisfy transition or release proof.

Next stage: Run `/02-doti-clarify` to resolve ambiguities, or `/03-doti-plan` when no clarification is needed.
