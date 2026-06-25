# doti-arch-review

Purpose: a **focused, multi-lens architecture/design review** — adversarially pressure-test the proposed design (spec + plan + tasks) against the **current code**, BEFORE any implementation, so design flaws are caught while they are still cheap to fix.

This is a **design review, not a structural-rule pass.** Sentrux and ArchUnitNET measure *implemented* code; at this stage there is no new code, so they have nothing to check here — do **not** run or validate them. Structural-rule coverage (`architecture test`, `sentrux verify`/`check`) is enforced by `gate run` during/after `/07-doti-implement`. The plan's declared **architecture delta** is reviewed here only as a design artifact.

**Run only the lenses the change actually touches.** Triage the change first; a lens whose concern the change does not touch **exits immediately** ("not applicable — no _X_ change; nothing to review"), is **not** a finding, is **not** a backstop failure, and **spawns no sub-agent.** Example: a templates/docs-only change skips the architecture-fit, blast-radius, edge-case/failure-mode, security, and testability lenses — they exit "code is not changing."

## How to run

1. Read `.doti/agent-context.md`, the active spec/plan/tasks, and the relevant current code.
2. **Get the changed-files context once (don't hand-hunt).** Obtain the list of changed source files and inject it into every lens so no lens wastes tokens rediscovering scope. Preferred: the first-class command `hx`/doti emits for this (planned — 007 FR-043; `Hx.Impact.Cli plan --json` already produces the git-diff + project-graph closure today). Fallback: `git diff --name-only <cycle-base>..HEAD` plus `git status --porcelain` for untracked/working-tree files (the cycle base ref is in `.doti/cycle-state.json`). Pass this file list verbatim to each lens.
3. **Triage — classify the change footprint** from the changed files + spec/plan/tasks. Which does it touch? **production code** (`src/`, `tools/*/*.cs`); **CLI surface** (new/changed commands); **data/contracts** (schemas, error codes, JSON contracts, public interfaces, file formats, versioning); **security surface** (untrusted input, secrets, network, vendored binaries, privilege); **project graph / dependencies / layering**; **runtime behavior**; or only **templates / docs / skills / config** (no runtime, no code).
4. **Select + run lenses.** Activate only the lenses whose *Applies when* matches the triage; record each non-applicable lens as **skipped (not applicable)** with a one-line reason.
   - **Sub-agents:** *if you are capable of spawning sub-agents, run each applicable lens as its own clean-context sub-agent IN PARALLEL.* Hand each sub-agent: the lens's **Look for** checklist (below), the changed-files list (step 2), the spec `FR`/`SC` IDs + plan decisions + tasks, and pointers to the current code it should read. If you cannot spawn sub-agents, run the applicable lenses yourself, sequentially, one focused pass per lens.
5. Each finding carries **severity** (BLOCKER / HIGH / MEDIUM / LOW), **evidence** (a spec/plan/task ref or `file:line` — never an unverified assertion), and a **concrete recommendation**. Synthesize into one report ordered by severity; de-duplicate; adversarially verify high-stakes claims (a second pass that tries to refute).
6. **Verdict + backstop:** an open **BLOCKER** in an *applicable* lens, or a **missing *applicable* lens**, blocks `/07-doti-implement`. A correctly-skipped lens does **not** block. Record proven-vs-advisory and which lenses **ran** vs. **skipped (not applicable)**.

## Lens panel (applicability + pre-canned "Look for")

- **Design soundness** — *Applies when:* there is any spec/design to satisfy (almost always). **Look for:** any `FR`/`SC` with no mechanism in the plan/tasks; contradictions across spec ↔ plan ↔ tasks; requirements silently dropped; "magic"/hand-waved steps; success criteria the design cannot actually achieve.
- **Edge cases & failure modes** — *Applies when:* the change adds/alters **runtime behavior** (code, commands, scripts). *Skips when:* docs/template text only. **Look for:** unhandled empty/missing/malformed inputs; behavior on partial failure or mid-operation crash (idempotency/resumability); concurrency/races; offline/network failure; large/pathological inputs; failures that surface raw exceptions instead of a fail-closed structured `CliResult` diagnostic.
- **Data & contract integrity** — *Applies when:* the change touches **schemas / error codes / contracts / public interfaces / file formats / versioning**. *Skips when:* none change. **Look for:** new/changed error codes not registered (registry is append-only); exit-class inconsistency; schema/JSON-contract changes without a version bump; breaking changes to public interfaces/formats; producer/consumer mismatch; missing migration path for existing data.
- **Security & trust boundaries** — *Applies when:* the change touches **untrusted input (URLs, files, network), secrets, auth, vendored binaries, or privilege**. *Skips when:* no security-relevant surface. **Look for:** untrusted input treated as trusted or as instructions; secrets in logs/output/artifacts; unverified vendored binaries / supply-chain; path traversal / injection; privilege or scope creep; whether the spec's trust model is actually enforced.
- **Blast radius & dependencies** — *Applies when:* the change touches **production code, the project graph, or dependencies**. *Skips when:* no code/structure change. **Look for:** new dependency edges that violate layering (`*.Core`→`*.Cli`, contracts depending on internals); new project-graph cycles; changes to widely-used types (broad ripple); migration/compat risk to existing or generated repos; fights with existing idioms.
- **Simpler alternative** — *Applies when:* the change introduces a **new mechanism or non-trivial design**. *Skips when:* a trivial text/template tweak. **Look for:** a new abstraction where an existing one would do; redundant layers; premature generality (built for N when 1 is needed); an existing component/command that already does this; the simplest design that still meets the FRs — name it + the trade-off.
- **Testability & proof** — *Applies when:* the change adds **behavior/requirements needing a deterministic proof**. *Skips when:* nothing provable changes. **Look for:** `FR`/`SC` with no deterministic acceptance signal; test tasks placed after (not before) the impl they cover; gates/proofs left advisory where they should fail closed; behaviors only verifiable by manual review; missing negative/failure-path tests.
- **Fit with current architecture** — *Applies when:* the change touches **production code structure / layering / naming**. *Skips when:* **code is not changing** (templates/docs/config only) — exit "no code change; nothing to review." **Look for:** logic in a `*.Cli` project instead of `*.Core` (Channel-Independence violation); commands that are not thin (parse→delegate→render); naming/pattern drift from the existing code; the plan's architecture-delta claim being wrong or incomplete vs. what the change actually introduces.

## Output

Findings ordered by severity (from the **applicable** lenses), each with evidence + recommendation; an explicit list of which lenses **ran** vs. **skipped (not applicable, + reason)**; the verdict (any BLOCKER open in an applicable lens?); and a proven-vs-advisory note. **A review where every lens correctly skipped is a valid "nothing to review" outcome — record it and proceed.**

## Command availability

This stage is an advisory design review — no single command mints its proof. The changed-files context comes from `hx`/`Hx.Impact.Cli plan` (or `git diff` as fallback); the command-backed gates (`architecture test`, `sentrux verify`/`check`, `gate run`) run at `/07-doti-implement`, not here.

## Next

Run `/07-doti-implement` to implement the tasks — only once no BLOCKER is open in an applicable lens.
