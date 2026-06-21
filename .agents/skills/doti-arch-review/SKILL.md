---
name: doti-arch-review
description: Review scaffold-dotnet architecture impacts and ArchUnitNET gate coverage.
compatibility:
  - codex
metadata:
  source: doti/core/templates/commands/doti-arch-review.md
  maturity: command-aware-advisory
---
# doti-arch-review

Read `.doti/agent-context.md`, then follow `doti/core/templates/commands/doti-arch-review.md`.

Validate that the ArchUnitNET config (`rules/architecture.json`, nine rule families: six structural plus a security/capability-confinement family, an agent-first output-confinement family, and a CLI surface-confinement (thin-adapter) family) and the Sentrux config (`.sentrux/rules.toml` layers/boundaries/constraints, baseline, `rules/sentrux.json` mapping) measure the intended architecture and stay mutually consistent. The command template seeds the specific items to check so they are not re-derived each run. The architecture gate (`architecture test` per-family proof) and the Sentrux gate are command-backed; this cross-engine consistency review is an advisory judgment.

## Asking the operator a question (required format)

Any operator-facing question — at any stage, for Claude or Codex — uses this fixed format, presented immediately before the question:

- **Context** — the full background. Assume the operator has not re-read the material; restate what is unclear and the relevant facts.
- **Why it matters** — the concrete impact and why it needs a decision now.
- **Options** — for each option: **Pros**, **Cons**, and **Consequence** (what becomes true downstream if chosen).
- **Recommendation** — the option you recommend and the reasoning; list it first and label it "(Recommended)".
- **Assumptions** — the assumptions behind the recommendation.
- **Confidence** — High / Medium / Low, with a one-line reason.

Evidence requirement: every question, option, recommendation, and assumption must rest on verified facts. Verify each premise first (read the code, run the tool, observe the output) and cite that evidence; never present options whose premises you have not confirmed. An assumption is allowed only when the fact cannot be obtained from this environment — then label it UNVERIFIED, state why it cannot be verified and what would verify it. Prove claims (reproduce/RCA); never assert them. Do not use "assume" to defer work you could do — an unproven premise is a defect that can ruin the design.

Command availability: Hygiene scanning, restore/build/test, the bootstrap CLI, the platform probe, the Doti renderer, the `dotnet new` template pack, `Hx.Scaffold.Cli new` + `doti install`, the architecture gate, `gate run`, the affected-test planner (`Hx.Impact.Cli plan`), GitVersion versioning (`version calculate`/`version bump`), and the security gate (`security scan` — package-vulnerability SCA via `dotnet list package --vulnerable`, plus the build-integrated analyzer SAST status; enforced at release, advisory in dev) are all available. The .NET security analyzers + Gitleaks are the SAST gate. Secret scanning is the vendored Gitleaks; GitVersion is vendored for win-x64 (large binary gitignored, an operational vendor step). The build is fully green on win-x64; other RIDs fail closed. The doti cycle-state substrate (`doti cycle stamp`/`status`: diff-bound stage proofs + freshness detection) and the single-sourced Operator-Question Protocol render into every skill + the agent context. The enforcing chokepoints are `doti cycle check` (fail-closed prerequisites), `doti cycle commit` (the sole sanctioned commit path; refuses unless a fresh drift-review + task-hash, a fresh passing persisted gate proof, and a clean staged scope), `doti question check` (the operator-question validator), and an untracked insurance pre-commit hook (`doti install-hooks`); `gate run` persists a change-set-bound proof.

Next stage: Run `/doti-implement` to implement the tasks.
