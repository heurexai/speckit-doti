# 014 — Structural-Engine Violation Detail (ArchUnitNET + Sentrux)

## Goal

When the architecture gate (ArchUnitNET) or the Sentrux structural gate fails, the operator currently sees almost nothing actionable — a family pass/fail count and a summarized rule string — so they cannot tell **which files or types caused the violation**. This feature captures and surfaces the offender detail for both engines: for ArchUnitNET, the violating types (classes/namespaces) and the rule they broke; for Sentrux, the offending file and function (and line + measured value vs the limit where the engine provides them). It renders this detail in the standalone commands AND in the gate ladder (014 builds on 012's WI-5 ladder rendering). It is the structural-engine half of the gate-visibility work: 012 shows *which steps ran and the test scope*; 014 shows *what made a structural step fail and where*.

The detail is not just unrendered today — it is **not captured**: ArchUnitNET tests assert `rule.HasNoViolations(Arch)` (a boolean, discarding the violating-object descriptions), and `SentruxCheckResult.RuleViolations` is a summarized `string` list with no file/function. So this feature must extend the capture path, then render it.

**Summary, not a dump** (following 012 FR-018): each violation renders as **one concise line** — e.g. `max_cc: ProcessFoo() — Bar.cs:42 (CC 28 > 25)` or `cliSurfaceConfinement: FooService in Hx.X.Cli` — capped with "+N more" when a rule has many offenders. The human surface is a scannable summary so an operator can spot the failing path and its likely cause; the complete offender set stays in `--json`.

## User Scenarios & Testing

**Priority Mode** — workflow / tooling change: fail-closed safety + deterministic proof before ergonomics. Visibility must never weaken the gate or fabricate an offender it cannot prove.

### Work Item 1 — Capture + surface ArchUnitNET violation detail (Priority: P1)

When the architecture gate fails, the operator sees which rule/family failed AND the violating types — not just "11/12 passed; 2 families".

- **Why this priority:** the architecture gate is a primary structural guard; an opaque failure ("a family failed") forces the operator to re-derive the offender by hand or read the raw test output.
- **Independent Test:** force an ArchUnitNET violation (a `*.Cli` type with a confined suffix) and verify the output names the failing family and the violating type(s) with the rule description.
- **Acceptance Scenarios:**
  1. **Given** a type that violates `cliSurfaceConfinement`, **When** `hx architecture test --json` runs, **Then** the failing family lists the violating type name(s) and the rule that was broken.
  2. **Given** all rules pass, **When** the gate runs, **Then** the architecture step reports pass with the family count and no violation noise.

### Work Item 2 — Capture + surface Sentrux violation detail (Priority: P1)

When the Sentrux gate fails, the operator sees the offending file and function (and line + measured value where Sentrux provides them), not just a rule name and a count.

- **Why this priority:** a structural-degradation or `max_cc` failure is only actionable if the operator knows the offending function; today they get a count with no location.
- **Independent Test:** force a Sentrux rule violation (a function over the CC limit) and verify the output names the offending function + file (+ line/value where available) and the rule.
- **Acceptance Scenarios:**
  1. **Given** a function exceeding `max_cc`, **When** `hx sentrux check --json` runs, **Then** the violation names the function, the file, the measured value vs the limit, and the rule id.
  2. **Given** Sentrux cannot provide a particular field for a rule (e.g., no line), **When** the output is produced, **Then** that field is marked unknown with the reason rather than omitted silently or shown as zero.

### Work Item 3 — Render structural detail in the gate ladder + standalone commands (Priority: P1)

The same offender detail appears wherever the operator meets the engine: in `hx architecture test` / `hx sentrux check`, and in the `hx gate run` ladder for the `architecture-test`/`sentrux-*` steps when they fail.

- **Why this priority:** the operator most often hits these failures inside a gate run; the detail must be at the point of failure, not only in a separate command.
- **Independent Test:** force each violation, run `hx gate run`, and verify the failing structural step in the ladder carries the offender detail (human + JSON), consistent with the standalone command.
- **Acceptance Scenarios:**
  1. **Given** an architecture or Sentrux violation, **When** `hx gate run` fails, **Then** the human ladder (012 WI-5) shows the failing step with the violating types / offending function+file, and the JSON proof carries the same.
  2. **Given** a passing run, **When** the gate completes, **Then** the structural steps show pass with no violation detail (no false offenders).

### Edge Cases

- A rule fails with many violating objects: output is deterministically ordered and may cap the listed offenders with an explicit "+N more" rather than an unbounded dump.
- ArchUnitNET reports a violation at namespace/assembly granularity (not a single type): the output names the granularity the engine actually reports, not a fabricated file path.
- Sentrux emits a rule with no per-function attribution (a whole-graph signal like structural degradation): output attributes it to the rule/graph level and marks function/file unknown with the reason.
- A pre-build run where ArchUnitNET assemblies or Sentrux outputs are unavailable: report the capture failure as a diagnostic, never an empty "no violations".
- The architecture gate runs via `dotnet test`/TRX today; the violation descriptions must be captured into the result even though the current path only parses test name + outcome from the TRX.

## Scope

Included:

- Extend the ArchUnitNET capture so a failing rule surfaces the **violating objects + rule description**, not a discarded boolean.
- Extend the Sentrux capture so a rule violation surfaces a **structured offender** (rule id, file, function, line, measured value vs limit) where the engine provides them.
- Additively extend the contracts: `ArchitectureTestCase` (per-failure detail) and `SentruxCheckResult.RuleViolations` (string list → structured violation records).
- Render the detail in the standalone `architecture test` / `sentrux check` output and in the `gate run` ladder (via 012 WI-5).
- Preserve fail-closed gate behavior and the existing proof/validation invariants.

Excluded:

- No change to the ArchUnitNET rule families or the Sentrux policy/limits themselves (this surfaces violations, it does not relax them).
- No new Sentrux baseline; no rebaseline path.
- No mapping of violations to remediation beyond what the engine reports.
- No replacement of the `dotnet test`/TRX architecture-gate execution model unless capture requires a minimal, justified adjustment.

## Functional Requirements

- `FR-001`: When the architecture gate fails, the output MUST identify, per failing rule/family, the **violating types** (classes/namespaces at the granularity ArchUnitNET reports) and the rule description — derived from ArchUnitNET's evaluation, not only the test name and pass/fail count. `[WI1]`
- `FR-002`: The architecture-test result contract MUST be extended **additively** to carry per-failure detail (violating objects + rule description), exposed in both JSON and human output; `ArchitectureTestCase` today carries only `Name` + `Outcome`. `[WI1]`
- `FR-003`: When the Sentrux gate reports a rule violation, the output MUST identify a **structured offender** — rule id, file, and function, plus line and measured-value-vs-limit where Sentrux provides them — per violation; `SentruxCheckResult.RuleViolations` today is summarized strings and MUST be extended additively. `[WI2]`
- `FR-004`: The structural-engine violation detail MUST be surfaced in the `gate run` ladder (012 WI-5) for the `architecture-test` and `sentrux-*` steps when they fail, so an operator running `hx gate run` sees which files/types caused a structural failure without a separate command. `[WI3]`
- `FR-005`: Where an engine does not provide a particular field, the output MUST mark it **unknown with a reason** (consistent with 012 WI-4's safe-trace rule) and MUST NOT imply no violation, show a fabricated location, or report unknown as zero. `[WI1, WI2]`
- `FR-006`: Violation lists MUST use **deterministic ordering** and MAY cap the displayed offenders with an explicit overflow indicator (e.g., "+N more"); the full set MUST remain available in JSON. `[WI3]`
- `FR-007`: This feature MUST NOT weaken the gates: the architecture and Sentrux pass/fail outcomes, the gate proof, and all existing validation remain authoritative; capturing detail MUST NOT change whether a run passes or fails. `[WI1, WI2, WI3]`

## Success Criteria

- `SC-001`: A forced ArchUnitNET violation produces output naming the failing family AND the violating type(s) with the rule description.
- `SC-002`: A forced Sentrux violation (a function over the CC limit) produces output naming the offending function + file (+ line/value where available) and the rule id — not just the rule + count.
- `SC-003`: Both structural-engine details appear in the human gate ladder for the failing step, matching the JSON proof.
- `SC-004`: When an engine cannot provide a field, the output marks it unknown with a reason; a passing run shows the structural steps as pass with no fabricated offenders.
- `SC-005`: Repeated runs over the same violating change set produce identical (deterministically ordered) offender output.
- `SC-006`: The architecture and Sentrux pass/fail outcomes and the gate proof are unchanged by the added detail (visibility-only).

## Key Entities

- **Architecture Violation** — a failing ArchUnitNET rule with its description and the violating objects (types/namespaces) ArchUnitNET reports.
- **Sentrux Violation** — a failing Sentrux rule with a structured offender: rule id, file, function, line, measured value vs limit (fields present per what the engine emits).
- **Structural Step Detail** — the offender detail attached to the `architecture-test`/`sentrux-*` ladder steps (012 WI-5) when they fail.

## Deterministic Surfaces

- `hx architecture test --repo . --json` — existing command; enriched with violation detail by this feature.
- `hx sentrux check --repo . --json` / `hx sentrux verify` — existing commands; enriched with structured offender detail.
- `hx gate run --repo . --profile normal --json` + the 012 WI-5 ladder — the structural steps carry the detail on failure.
- `tools/Hx.Tooling.Contracts` (`ArchitectureTestCase`, `SentruxCheckResult`) — additively extended contracts.
- `tools/Hx.Runner.Core/ArchitectureGate` (capture ArchUnitNET evaluation) and `tools/Hx.Sentrux.Core` (parse the sentrux engine's per-violation output).

## Architecture Impact

- Production code + contracts: two additive contract extensions (`ArchitectureTestCase` per-failure detail; `SentruxCheckResult` structured violations). The ArchUnitNET capture requires the architecture tests/runner to surface `rule.Evaluate`/`Check` descriptions (today they assert `HasNoViolations`, discarding them) — captured into the result even though the gate runs via `dotnet test`/TRX. The Sentrux capture requires parsing the sentrux binary's per-violation output rather than summarizing to a count string.
- Rendering is via 012 WI-5 (the shared kernel ladder) — 014 depends on 012's ladder for the gate surface; the standalone commands render through the existing shared CLI renderer.
- Thin CLI preserved: capture/parse logic lives in `*.Core`; the CLI only renders. No rule-family or Sentrux-policy change.

## Sentrux And Hygiene Impact

- **Operator-directed scope correction (2026-06-28, see Clarifications):** Sentrux is scoped to **production code only** — the repo's `test/` tree is excluded via `.sentruxignore` (the fork honours it for the scan AND the quality graph), alongside the already-excluded `scaffold/templates/`, docs, and Doti prose. Test code legitimately carries many test methods per file (a large, well-structured fixture is not a production "god file"), so counting it produced a false-positive structural-degradation reading. Excluding it gives a true barometer of production architecture and is a **strengthening** (the production floor rises). This surfaces violations and tightens the production reading — it never relaxes a rule or limit.
- **Consequent baseline raise:** removing the test noise raises the production-only quality signal above the prior (test-inclusive) baseline, so the Sentrux baseline is raised once via the authorized `hx sentrux baseline --authorize-rebaseline` path (FR-031: explicit operator intent + a change-set-fresh arch-review classifying the growth as legitimate, not a laundered regression). The raise locks in a stricter production floor; the baseline is never lowered or removed.
- New `*.Core` capture/parse logic stays within the function-size limit and the layer/cycle boundaries.
- Public hygiene low; release-facing docs may note the richer structural output after implementation.

## Assumptions

- ArchUnitNET's evaluation API can produce the violating objects + rule description for a failing rule (today discarded by the boolean `HasNoViolations` assertion); capturing them is an evaluation-path change, not new analysis.
- The Sentrux engine emits per-violation detail (at least rule + function + file for `max_cc`-class rules); fields the engine does not emit are surfaced as unknown, not fabricated. (Whether Sentrux emits line/value for every rule is a research/clarify point.)
- Some Sentrux signals are whole-graph (structural degradation) with no single offender; these are attributed at the rule/graph level honestly.
- Depends on 012 WI-5 (gate-ladder rendering) for the in-gate surface; the standalone commands do not depend on 012.

## Acceptance

- Command-backed today: `hx architecture test --json`, `hx sentrux check --json`/`verify`, `hx gate run --json`, `architecture test`, `sentrux check`, `doti render-skills --check`, `doti payload check`.
- Planned by this feature: the captured violating-type/offender detail and its rendering in the standalone commands + the gate ladder.
- Advisory until implemented: exact Sentrux offender field availability per rule and any human formatting beyond the shared renderer + the 012 ladder.

## Clarifications

### Pre-emptive clarify 2026-06-28 (no stamp — resolved ahead of the cycle)

- **Q: Does the engine data actually exist to surface offenders, or is this aspirational?** **A (verified):** Yes for both. **Sentrux** — `check --json` emits a `violations` array, and `SentruxOutputParser.FormatObjectViolation` already reads `rule`, `path`/`file`/`source`, `line`/`startLine`, `message`, `detail`, `recommendation` per violation, then **flattens them to one string** (`RuleViolations`). WI-2 preserves the structure (a `SentruxViolation` record) instead of flattening — the offender data is already captured, just discarded. **ArchUnitNET** — the violating objects + rule descriptions are available via `IArchRule.Evaluate(arch)` (`EvaluationResult.Passed`/`.Description`); the tests currently throw them away by asserting the boolean `rule.HasNoViolations(Arch)`. WI-1 captures the Evaluate descriptions. **Caveat (FR-005 path):** the observed `max_cc` violation (cycle 009) emitted only a summary `message` ("N function(s) exceed…") with no `path`, so per-**function** location for summary-style structural rules may be unavailable from the engine — those are marked `unknown` with a reason, confirmed at implement, never fabricated. **No operator blocker.**

### Mid-implement operator decision 2026-06-28 — scope Sentrux to production code

- **Decision (operator-directed during implement):** exclude the repo's `test/` tree from the Sentrux scan/quality graph (via `.sentruxignore`) so the structural barometer measures **production architecture only**. Trigger: a 014 *test* file (`test/Hx.Gate.Tests/ProofHashBoundaryTests.cs`) crossed the god-file threshold and dropped the whole-graph quality signal −14, a false positive (test fixtures legitimately have many methods per file). **Verified:** 014's *production* code added zero new god files or complex functions; with tests excluded the production-only signal rises to 6483 (god files back to 5, all production). **Consequence:** the prior test-inclusive baseline (6383) is stale-low, so the baseline is raised once to the production-only signal via the authorized `hx sentrux baseline --authorize-rebaseline` (FR-031) — a stricter floor, an operator-classified legitimate (non-regression) growth, never a lowering. Recorded here as the source-of-truth for the implement change set (`.sentruxignore` + `.sentrux/baseline.json`).
