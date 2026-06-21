# scaffold-dotnet Constitution

## Deterministic Ownership

The scaffold is a deterministic .NET tooling system. Agents may help plan, run, inspect, and summarize work, but source-controlled .NET tools and generated configuration are the authority for build policy, quality policy, affected-test selection, release readiness, and architecture conformance.

## Bootstrap Honesty

Until a deterministic command exists, any related Doti workflow guidance is advisory. Advisory checks must be labeled as advisory and must not be reported as gate proof.

## Template Boundary

The .NET template engine owns static file layout, renaming, metadata substitution, and optional content. The scaffold CLI owns dynamic finishing, Sentrux vendoring, local skill rendering, version calculation, hygiene scanning, and JSON proof.

## Public Hygiene

The repository is intended to be public and MIT licensed. Avoid developer-local paths, secrets, private-key material, local binary mirrors, and generated binaries unless they are deliberately vendored with manifest and hash proof.

## Cross-Platform Rule

Custom runner logic must be .NET code or dotnet-hosted tooling. Do not add PowerShell or Bash runners to generated repositories.

## Engineering Discipline

Work is held to a 95%-confidence bar. On any failure or surprise, root-cause it (read the code/output, reproduce, find the underlying cause) before changing anything — never patch symptoms. Validate every assumption by reading the code, running the command, or observing the output, and prove claims (reproduce/RCA) rather than assert them. Take no shortcuts: do not silence checks, hard-code around problems, stub past failures, or declare work done without proof. If you are not at least 95% confident a solution and its code are correct, keep finding better approaches and validating until you are; if you are genuinely blocked (missing access, an operator decision, an unverifiable premise), surface it with what you tried and what is needed rather than guess. Report honestly — what was validated and how, what remains, and any deferral with its rationale.

## Operator Decisions

When a blocking decision or ambiguity needs the operator, surface it with full context (assume the operator has not re-read the material), each option's pros, cons, and consequence to the design, a recommendation with its reasoning, the assumptions behind it, and a confidence level. Questions must rest on verified facts, not speculation (see Engineering Discipline). This format applies to any operator-facing question, not only the clarify stage; `/doti-clarify` adds the stage-specifics (one blocking question at a time, each folded into the spec before the next). The operational format is the single-sourced **Operator-Question Protocol** rendered into every skill's `SKILL.md` and `.doti/agent-context.md` (source: `doti/core/skills.json`).

## Codified Cycle

The doti cycle is enforced by code, not honored by convention. A stage proceeds only when its prerequisites are stamped and **fresh** (`doti cycle check --stage <X>`, fail-closed), and commits go **only** through `doti cycle commit` — the sanctioned path that re-verifies the prerequisite chain (including a fresh drift-review and the task-hash), a fresh passing gate proof, and a clean staged scope, then commits; it refuses otherwise. **Never commit by hand**: an untracked, logic-free insurance pre-commit hook (installed by `doti install-hooks`) redirects a bare `git commit` to the sanctioned path. Freshness is diff-bound — changing code after a stage's proof invalidates that proof.

## Channel Independence (Thin Adapter)

Behavior lives in `*.Core` libraries and must be drivable from any channel. CLI/entry projects are thin adapters: they parse input, delegate to a core type, and render the `CliResult` — they hold no business logic. A CLI type may construct and inject channel adapters (network, process, console) into pure core, but the logic itself lives in core. Adding a feature means adding it to a `*.Core` library with a command that only wires it — keeping the core reusable across channels (CLI, daemon, HTTP). Enforced by the thin-CLI architecture families (`cliSurfaceConfinement`, `cliDelegation`).
