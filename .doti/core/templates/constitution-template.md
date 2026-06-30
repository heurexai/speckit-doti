# {PROJECT_NAME} Constitution

> doti's constitution has **two layers**. **§1 — Inherited doti invariants** are the codified givens every doti project inherits; they are cited here as a fixed reference and a project MUST NOT re-declare, weaken, or override them. **§2 — Project declarations** are the only operator-authored content (the only placeholders) — the project's own domain, stack, style, security, and performance conventions. Author and amend §2 with `/doti-constitution`. Amendments are tracked by the doti cycle + git history; there is **no** SemVer doc-version line or Sync Impact Report (doti codifies versioning). Only **§2** is re-injected and checked by `/03-doti-plan` and `/04-doti-arch-review`; §1 is gate/ArchUnit/Sentrux/GitVersion-enforced and not re-checked by the agent.

## §1 — Inherited doti invariants (codified; cite, never re-declare)

doti already codifies the following. A project inherits them and MUST NOT add a placeholder, principle, or override for any of them:

- **Deterministic ownership** — source-controlled .NET tools + generated configuration are the authority for build, quality, affected-test selection, release readiness, and architecture conformance; advisory checks are never reported as proof.
- **Library-first / pure core** — behavior lives in `*.Core` libraries, reusable across channels.
- **Thin CLI + `CliResult`** — entry/CLI projects parse input, delegate to a core type, and render the JSON-first `CliResult`; they hold no business logic (enforced by the `cliSurfaceConfinement`/`cliDelegation` families).
- **Versioning (GitVersion)** — versions and release tags are GitVersion-calculated. A project MUST NOT declare a SemVer/versioning policy.
- **Quality gates & complexity (Sentrux + analyzers)** — function-size and structural-degradation limits, the .NET security analyzers, and the deterministic gate ladder are codified. A project MUST NOT declare its own quality-gate/workflow rules.
- **Cross-platform** — runner logic is .NET / dotnet-hosted; no PowerShell or Bash runners in generated repos.
- **Hygiene & SAST** — public hygiene (no developer-local paths, secrets, private keys, or unvendored binaries) plus Gitleaks + the .NET security analyzers are the SAST gate.
- **Codified cycle** — the `/01`–`/09` workflow is enforced by code; a stage proceeds only when its prerequisites are stamped + fresh; commits are owned by coded transitions; never commit by hand.
- **Engineering discipline** — a 95%-confidence bar: root-cause failures, validate every assumption by reading/running/observing, take no shortcuts, surface genuine blockers, report honestly.
- **Operator decisions** — blocking questions use the single-sourced Operator-Question Protocol (full context, each option's pros/cons/consequence, a recommendation, assumptions, confidence).
- **Self-describing automation** — the program does the bookkeeping and surfaces the exact evidence a decision needs — what changed (the file and the lines), what is stale and why, and the single valid next action — at the point of decision; the agent decides over information the engine has already laid out and never has to go discover it (hand-diffing, hunting for state, or guessing freshness). Determining and presenting the deltas is the program's job; judgment is the agent's. (The gate names its own offender; a stale stage names what diverged; every `CliResult` carries its next action — self-describing CI.)

**The rule generalises:** if doti, the scaffold, or the solution can *determine* it (versioning, CLI/output shape, quality-gate/workflow rules, and identity metadata such as the project name), the constitution states it as a fixed §1 reference — it is **never** a fill-in blank. In particular there is no placeholder here for a versioning policy, a CLI/output shape, or quality-gate/workflow rules.

## §2 — Project declarations (operator-authored — the only fillable content)

The project's own conventions, *beyond* the .NET 10 baseline §1 already guarantees. Fill these with `/doti-constitution`; `/03-doti-plan` and `/04-doti-arch-review` re-inject and evaluate against them.

### Domain principles

[DOMAIN_PRINCIPLES]

### Tech stack (beyond the .NET 10 baseline)

[TECH_STACK]

### Coding style

[CODING_STYLE]

### Security & compliance

[SECURITY_COMPLIANCE]

### Performance

[PERFORMANCE]
