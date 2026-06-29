# scaffold-dotnet Constitution

> Two layers. **§1 — Inherited doti invariants** are the codified principles this project authors and every doti-generated repo inherits; they are gate/ArchUnit/Sentrux/GitVersion-enforced and not re-declared per feature. **§2 — Project declarations** are this repo's own domain, stack, and style — the content `/03-doti-plan` and `/04-doti-arch-review` re-inject and evaluate. Amendments are tracked by the doti cycle + git history (no SemVer doc-version line; doti codifies versioning). This is a real, filled constitution — it carries **no** placeholder tokens.

## §1 — Inherited doti invariants

### Deterministic Ownership

The scaffold is a deterministic .NET tooling system. Agents may help plan, run, inspect, and summarize work, but source-controlled .NET tools and generated configuration are the authority for build policy, quality policy, affected-test selection, release readiness, and architecture conformance.

### Bootstrap Honesty

Until a deterministic command exists, any related Doti workflow guidance is advisory. Advisory checks must be labeled as advisory and must not be reported as gate proof.

### Template Boundary

The .NET template engine owns static file layout, renaming, metadata substitution, and optional content. The scaffold CLI owns dynamic finishing, Sentrux vendoring, local skill rendering, version calculation, hygiene scanning, and JSON proof.

### Public Hygiene

The repository is intended to be public and MIT licensed. Avoid developer-local paths, secrets, private-key material, local binary mirrors, and generated binaries unless they are deliberately vendored with manifest and hash proof.

### Cross-Platform Rule

Custom runner logic must be .NET code or dotnet-hosted tooling. Do not add PowerShell or Bash runners to generated repositories.

### Engineering Discipline

Work is held to a 95%-confidence bar. On any failure or surprise, root-cause it (read the code/output, reproduce, find the underlying cause) before changing anything — never patch symptoms. Validate every assumption by reading the code, running the command, or observing the output, and prove claims (reproduce/RCA) rather than assert them. Take no shortcuts: do not silence checks, hard-code around problems, stub past failures, or declare work done without proof. If you are not at least 95% confident a solution and its code are correct, keep finding better approaches and validating until you are; if you are genuinely blocked (missing access, an operator decision, an unverifiable premise), surface it with what you tried and what is needed rather than guess. Report honestly — what was validated and how, what remains, and any deferral with its rationale.

### Operator Decisions

When a blocking decision or ambiguity needs the operator, surface it with full context (assume the operator has not re-read the material), each option's pros, cons, and consequence to the design, a recommendation with its reasoning, the assumptions behind it, and a confidence level. Questions must rest on verified facts, not speculation (see Engineering Discipline). This format applies to any operator-facing question, not only the clarify stage; `/doti-clarify` adds the stage-specifics (one blocking question at a time, each folded into the spec before the next). The operational format is the single-sourced **Operator-Question Protocol** rendered into every skill's `SKILL.md` and `.doti/agent-context.md` (source: `.doti/core/skills.json`).

### Codified Cycle

The doti cycle is enforced by code, not honored by convention. A stage proceeds only when its prerequisites are stamped and **fresh** (`doti cycle check --stage <X>`, fail-closed), and commits are owned by coded Doti workflow transitions and release paths that re-verify the prerequisite chain, task hashes, a fresh passing gate proof, and the scoped change set before mutating Git. **Never commit by hand**: an untracked, logic-free insurance pre-commit hook (installed by `doti install-hooks`) blocks bare `git commit` and routes the operator back to the numbered workflow. Freshness is diff-bound — changing code after a stage's proof invalidates that proof.

### Channel Independence (Thin Adapter)

Behavior lives in `*.Core` libraries and must be drivable from any channel. CLI/entry projects are thin adapters: they parse input, delegate to a core type, and render the `CliResult` — they hold no business logic. A CLI type may construct and inject channel adapters (network, process, console) into pure core, but the logic itself lives in core. Adding a feature means adding it to a `*.Core` library with a command that only wires it — keeping the core reusable across channels (CLI, daemon, HTTP). Enforced by the thin-CLI architecture families (`cliSurfaceConfinement`, `cliDelegation`).

## §2 — Project declarations

### Domain principles

This repository **is** the doti scaffold generator and cycle engine — it produces guarded .NET repositories and enforces the doti workflow on itself. `Hx.Scaffold.Cli new` is the single generation entry point and runs trusted prerequisite preflight before output mutation. Operational `hx` commands require an executable-adjacent `hx.config.json`. The workflow is command-enforced at three chokepoints (`doti cycle check`, `doti question check`, and an untracked insurance pre-commit hook); commits are owned by coded workflow transitions and release paths, never by an agent-visible commit command. Installed skills are rendered from one source (`.doti/core/skills.json`) and never hand-edited. The constitution is a project artifact authored once and amended via `/doti-constitution`, not a per-cycle stamp.

### Self-hosting defect handling

This repository **is** the doti cycle engine and runs that engine on itself, so a defect in the doti process (the cycle-state/stamp machinery, the gate, the affected-test planner, the structural gates, freshness, or the transition/release paths) is a first-class bug in THIS repo's source — never an obstacle to route around. When the workflow misbehaves, root-cause it (Engineering Discipline) and **fix the actual defect in `*.Core` code**; do not band-aid it with repeated re-stamps, re-runs, manual commits, gate downgrades, or by rushing a run to completion with a known hole. The fix is **dogfooded immediately**: rebuild `hx` and use the fixed tool to continue the very cycle that surfaced the defect, so the fix validates itself on the live run. A clean, bug-free, self-testing run is prioritized over finishing a run quickly — a run that reaches release by working around a workflow defect is not done; the defect is the work. Capture each such defect (RCA + the in-code fix) so the codified workflow keeps eliminating the hand-wrangling it exists to prevent.

### Tech stack (beyond the .NET 10 baseline)

C# on .NET 10; `System.CommandLine` for the CLI surface; `ArchUnitNET` + the Heurex Sentrux fork (v0.5.12, pinned + SHA-256-verified, regression `gate` honors `--include-untracked`) for architecture and structural-complexity gates; Gitleaks (vendored, pinned) for secret scanning; GitVersion (vendored win-x64) for versioning; `YamlDotNet` for the `workflow.yml` stage model; `LLamaSharp` (Qwen3-Embedding GGUF, primary) + `Microsoft.ML.OnnxRuntime` (BGE-M3 ONNX, fallback) for the advisory offline semantic drift finder; xUnit for tests. Vendored tool binaries are gitignored and fetched per pinned manifests; the build is green on win-x64 and fails closed on undeclared RIDs.

### Coding style

Behavior lives in pure `*.Core` libraries; CLI/entry projects stay thin (parse → delegate to a named `*.Core` type → render `CliResult`, JSON-first, no business logic). One concern per file/class; a named `*.Core` type per behavior; compose rather than inline; keep methods within the Sentrux function-size limit (do not over-split). Gates are deterministic and fail-closed; nothing is silently downgraded from enforced to advisory. Error codes are stable `<PREFIX><NNNN>` diagnostics registered append-only in `errorcodes/registry.json`. New behavior is added to a `*.Core` library with a command that only wires it. Doti prose (templates, skills, agent-context, docs) is single-sourced in `.doti/core` and rendered — never hand-edited — and is Sentrux source-excluded.

### Security & compliance

MIT-licensed, intended-public repository. No developer-local paths, secrets, or private-key material in source; vendored binaries are pinned and SHA-256-verified, gitignored as an operational vendor step. The .NET security analyzers (CA3xxx/CA5xxx promoted to build errors) plus Gitleaks are the SAST gate — the build is the SAST enforcement point. The security gate runs package-vulnerability SCA (`dotnet list package --vulnerable`) and is enforced at release, advisory in dev. The semantic finder runs local/CPU-only over operator-provisioned, hash-verified models — no network egress, never on the gate path.

### Performance

Dev gates scope tests to the affected project graph (the affected-test planner) for fast feedback; release runs the full suite. Builds are single-threaded (`/m:1`) for deterministic, reproducible proof. The semantic finder is advisory and offline — it never sits on the gate hot path and skips cleanly when no model is provisioned. Gate proofs are change-set-bound and hashed so freshness is decided by content, not wall-clock.
