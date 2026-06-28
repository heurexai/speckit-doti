# Plan — 014 Structural-Engine Violation Detail (ArchUnitNET + Sentrux)

**Spec:** [docs/specs/014-structural-violation-detail.md](../specs/014-structural-violation-detail.md). **Stage:** `/03-doti-plan`. Builds on 012's gate-trace/ladder render.

## Summary

Surface **which files/types caused** a structural-gate failure. Capture the offender detail both engines already compute but discard (ArchUnitNET `EvaluationResult.Description`/`FailingObjects`; Sentrux per-violation `rule/path/file/line/detail`), carry it **additively** on render-only surfaces, and render it in the standalone commands and the 012 gate ladder. **Visibility-only:** the architecture/Sentrux pass-fail outcomes and the persisted gate proof stay byte-identical (FR-007/SC-006).

## Existing-architecture assessment (verified)

- **Contracts:** `ArchitectureTestCase(Name, Outcome)` ([ArchitectureResults.cs:15](../../tools/Hx.Tooling.Contracts/ArchitectureResults.cs)); `SentruxCheckResult.RuleViolations: IReadOnlyList<string>` ([SentruxCheckResult.cs:7](../../tools/Hx.Tooling.Contracts/SentruxCheckResult.cs)) — a flattened string list. `GateTrace` ([GateTrace.cs:47](../../tools/Hx.Tooling.Contracts/GateTrace.cs)) is the **render-only envelope** 012 established (M1: no `*ProofHasher` depends on it).
- **ArchUnit capture:** `ArchitectureTestRunner.ParseTrx` reads ONLY `testName`+`outcome` from the TRX ([ArchitectureTestRunner.cs:88](../../tools/Hx.Runner.Core/ArchitectureGate/ArchitectureTestRunner.cs)); the violating objects + rule description exist via `IArchRule.Evaluate(Arch)` but the tests assert the boolean `rule.HasNoViolations(Arch)`, discarding them.
- **Sentrux capture:** `SentruxOutputParser.FormatObjectViolation` already reads `rule/message/path|file|source/line|startLine/detail/recommendation` then **flattens to one string** ([SentruxOutputParser.cs:58](../../tools/Hx.Sentrux.Core/SentruxOutputParser.cs)). The structure is captured then thrown away.
- **THE PROOF BOUNDARY (load-bearing):** `gateProofDigest = SHA-256(JsonSerialize(PersistedGateProof))` ([CommitPreparation.cs:21,83](../../tools/Hx.Cycle.Core/CycleService.CommitPreparation.cs)) — it covers the **whole** persisted proof, including `GateProof.Steps[].Evidence`. The `GateRunResult.Trace` is **NOT** part of `PersistedGateProof` (only `.Proof` is persisted by `GateProofStore`). Therefore offender detail MUST attach to the **Trace envelope + the standalone result contracts**, never to `GateProof.Steps.Evidence` — else the digest (and SC-006) breaks.
- **Render seam:** `CliRenderer.WriteGateSummary`/`LadderLine` render the ladder from `GateTrace.Steps`, showing only the first evidence message capped at 70 chars ([CliRenderer.cs:347,424](../../tools/Hx.Cli.Kernel/CliRenderer.cs)). Offender lines render from a new trace field.

## Design

**Decision:** Additive, render-only violation detail on three envelopes; capture in `*.Core`; render in the kernel + standalone CLI. No proof, no rule-family, no Sentrux-policy change.

**New contracts (Hx.Tooling.Contracts, all additive/nullable):**
- `ArchitectureViolation(string Rule, string Description, IReadOnlyList<string> ViolatingObjects, string? UnknownReason = null)` — the granularity ArchUnitNET reports (types/namespaces), never fabricated.
- `SentruxViolation(string Rule, string? File, string? Function, int? Line, string? MeasuredValue, string? Limit, string? Message, string? UnknownReason = null)` — fields present per what the engine emits; absent → `UnknownReason` (FR-005), never zero/fabricated.
- `ArchitectureTestCase += IReadOnlyList<ArchitectureViolation>? Violations = null`.
- `SentruxCheckResult += IReadOnlyList<SentruxViolation>? RuleViolationDetails = null` (the `RuleViolations` string list is **unchanged** — backward-compat + so any existing consumer/proof path is untouched).
- `GateTrace += IReadOnlyList<StructuralStepViolations>? StructuralViolations = null`, where `StructuralStepViolations(string StepName, IReadOnlyList<ArchitectureViolation> Architecture, IReadOnlyList<SentruxViolation> Sentrux)` — keyed to the `architecture-test`/`sentrux-*` ladder steps.

**Capture (`*.Core`):**
- *ArchUnit (WI-1):* the architecture tests (`test/Hx.Architecture.Tests` AND the generated `scaffold/templates/**/Architecture.Tests` — the code-lens template) evaluate via `rule.Evaluate(Arch)` and, on failure, **emit `EvaluationResult.Description` + the failing object names into the assertion failure message** (the message the TRX records). `ArchitectureTestRunner.ParseTrx` is extended to read each failing `UnitTestResult`'s `Output/ErrorInfo/Message` and parse the emitted block into `ArchitectureViolation`s. Capture is **fail-closed**: a failing test whose message cannot be parsed yields one `ArchitectureViolation` with `UnknownReason`, never an empty "no violations" (Edge Case / FR-005). Green runs carry no violations.
- *Sentrux (WI-2):* `SentruxOutputParser` keeps a structured `SentruxViolation` per parsed object alongside the existing string flatten (one extra projection of data it already reads); `SentruxChecker.Build` sets `RuleViolationDetails`. A summary-style rule with no per-function attribution (the observed `max_cc` message, the structural-degradation whole-graph signal) → `SentruxViolation` with `Function/File` null + `UnknownReason` (FR-005, confirmed at implement).

**Render (WI-3):**
- `GateRunner` keeps the rich `ArchitectureTestResult` + `SentruxCheckResult` it already computes for the steps and hands their violations to `GateTraceProjector`, which sets `GateTrace.StructuralViolations` — **the `GateStep`/`GateProof` it builds is unchanged** (summary evidence only).
- `CliRenderer.WriteGateSummary` renders, under each FAILING `architecture-test`/`sentrux-*` ladder line, the offenders as concise one-line summaries (`max_cc: ProcessFoo() — Bar.cs:42 (CC 28 > 25)`, `cliSurfaceConfinement: FooService in Hx.X.Cli`), **deterministically ordered**, capped with "+N more" (FR-006); the full set stays in `--json`.
- Standalone `architecture test` / `sentrux check` render their `Violations`/`RuleViolationDetails` through the shared renderer.

**Decision — keep `RuleViolations` (string) intact rather than replacing it with the structured list.** Rationale: a clean additive extension keeps every existing consumer + any serialization-shape expectation byte-stable, and decouples the visibility feature from a contract migration; the structured list is the new, richer source while the string remains the legacy summary.

**Alternatives rejected:**
- *Put offender detail on `GateStep.Evidence`.* REJECTED — `Evidence` is inside `PersistedGateProof`, so the `gateProofDigest` would change with the offenders → violates FR-007/SC-006 (the proof must be byte-identical). The Trace envelope is the only proof-safe home.
- *Replace `RuleViolations: string` with the structured record.* REJECTED — a breaking contract change for no visibility benefit; additive is safer and keeps the proof/serialization stable.
- *Relax the boolean `HasNoViolations` assertions to non-failing.* REJECTED — that would weaken the gate (FR-007). The tests still FAIL on violation; they merely also EMIT the description.

## Architecture delta (enforced)

- **Proof-hash boundary (the M1 analogue, the must-prove invariant):** violation detail lives ONLY on `GateRunResult.Trace` + the standalone result contracts; it never enters `PersistedGateProof`, `gateProofDigest`, `AffectedTestProofHasher`, `CycleStageProofHasher`, or any `*ProofHasher`. **Proof:** a `ProofHashBoundaryTests` case asserting `DigestOf(PersistedGateProof)` is byte-identical with a null vs a fully-populated `StructuralViolations` trace, plus extending the 012 ArchUnit proof-hash-boundary assertion (`test/Hx.Architecture.Tests`) to the three new records.
- **Thin CLI preserved:** capture/parse in `Hx.Runner.Core` (ArchUnit) + `Hx.Sentrux.Core` (Sentrux); projection in `Hx.Gate.Core`; the CLI/kernel only renders. No ArchUnit family or Sentrux policy/limit change (the `cliSurfaceConfinement`/`cliDelegation` families + Sentrux boundaries are untouched). No new Sentrux baseline.
- **Generated-code template:** the `scaffold/templates/**` architecture tests are real generated code — the evaluate-and-emit change is applied there with the same care (it must still compile + pass green in a generated repo).

## Constitution Check

- §1 (inherited invariants): **PASS** — no gate weakened (outcomes unchanged, FR-007), thin-CLI/pure-core preserved, deterministic proof intact (the digest is provably unchanged). §2: **PASS** — within the .NET/coding-style baseline.

## Risk

- **Highest: the proof boundary (FR-007/SC-006).** Mitigated exactly as 012's M1 — detail on the Trace envelope only, proven by the digest-equality boundary test + the ArchUnit boundary assertion.
- **Medium: ArchUnit TRX capture.** The description must round-trip through the test failure message → TRX → parser. Mitigated by a deterministic emit format + a unit test over a crafted TRX (no real violation needed), and fail-closed `UnknownReason` when a message can't be parsed.
- **Low: Sentrux field availability.** The `max_cc` summary rule may lack per-function location → honest `UnknownReason` (confirmed at implement, per the spec's verified clarification).

## Next

`/04-doti-tasks` — break into contract / ArchUnit-capture / Sentrux-capture / render / boundary-proof tasks.
