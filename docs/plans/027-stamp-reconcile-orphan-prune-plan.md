# 027 — Plan: Codified stamp reconciliation + update orphan-prune

## Summary

Make stamp reconciliation a deterministic projection over the freshness the chokepoint already computes, so the agent never hand-re-stamps — but auto-rebind ONLY when content is provably unchanged (own artifact unchanged AND every producing upstream Fresh AND shared prereq content byte-identical), never for a real content change, a review-kind stage, or a change-set-bound stage. Symmetrically, make `hx doti update` prune managed skill dirs a payload renamed away (clean-baseline-gated). Both are `*.Core` changes; gate-proof hashing is untouched; all new proof fields are additive/nullable.

## Technical Context

Two engines: `Hx.Cycle.Core` (freshness/stamp/refresh) and `Hx.Doti.Core` (install/update reconcile). The RCA (workflow `doti-stamping-rca-harden`) located every site. Constraint: the diff-bound gate proof and the gate-proof digest MUST stay byte-unchanged; `ChangeSetDiffers` MUST keep mapping to `RerunRequired` (locks the 021/026 fix).

## Constitution Check (gate)

- §1 invariants — *Codified Cycle*: this strengthens the cycle (auto-reconcile is a projection over the same fail-closed `Check`; no enforcement weakened) — PASS. *Deterministic Ownership*: auto-rebind is content-equality-gated; a real change still earns a real re-run + gate proof; gate-proof digest byte-unchanged — PASS. *Bootstrap Honesty / Template Boundary*: `*.Core` logic, no CLI surface change — PASS.
- §2 — re-read fresh at implement; a stamping-engine change touches no domain declaration. Expected PASS.

## Research / decisions

- **Decision:** split `PrereqArtifactChanged` into `PrereqRebindable` (own-artifact-unchanged + only the prereq binding lags with byte-identical shared content) vs keep `PrereqArtifactChanged` (a shared upstream content hash changed value). Only the former may auto-rebind, gated by all-upstreams-Fresh, and never for review-kind dependents.
  - **Rationale:** this is the exact safety boundary the adversarial review required — re-binding a downstream to a CHANGED upstream it never re-derived against would rubber-stamp the proof (inverts "fix the artifact, never the proof"). The own-artifact-hash-unchanged guard alone is insufficient (it only proves the file wasn't touched); the byte-identical-shared-content + all-upstreams-Fresh conditions are what make it safe.
  - **Alternatives rejected:** (a) auto-rebind any `PrereqArtifactChanged` (the original synthesis) — UNSAFE (rubber-stamps content changes); (b) keep manual `/doti-amend` only — leaves the operator treadmill; (c) classify by `StaleReason` alone without a Fresh-upstreams gate — cannot tell a re-bindable lag from a real change.
- **Decision:** the `inserted-stage` verdict + the on-stamp auto-cascade are projections over the SAME `Check` report (never a second evaluator), so refresh and the chokepoint can never disagree.
- **Decision:** orphan-prune sources candidates from an ON-DISK scan of agent SkillsRoots (not only the prior manifest, which is empty on pre-category repos like agentx), deleting only baseline-clean files via the existing clean-only guard.

## Design (the 9 changes — file → change → why)

1. `FreshnessEvaluator.cs` — add `StaleReason.PrereqRebindable`; in the prereq arm (lines ~98-114) emit it ONLY when the own-artifact arm did NOT fire (own content unchanged), the stage is not change-set-bound, and the divergence is the prereq set with byte-identical shared-path content. *Why:* the machine-readable precondition for safe rebind; own-content/change-set-bound can never reach it.
2. `RestampSafetyClassifier.cs` — add `RestampSafety.ReBindContentEqual` tier; map `PrereqRebindable → ReBindContentEqual`; leave `OwnArtifactChanged`/`PrereqArtifactChanged`/`ChangeSetDiffers → RerunRequired`, `Missing* → SafeReinterpret`. Keep pure/total. *Why:* codifies the tier as a pure projection so refresh can't disagree.
3. `CycleRecoveryPlanner.cs` — gate `ReBindContentEqual`: downgrade to `RerunRequired` unless every producing stage in the step's transitive closure is `Fresh` in the same report; AND downgrade to `RerunRequired` for a **review-kind** dependent. Emit a distinct `inserted-stage` verdict (recommended cmd `/{stage.Command}`) for a current-graph-required stage absent from cycle-state. Map `ReBindContentEqual`'s next-cmd to `doti cycle refresh --apply-safe`. *Why:* the Fresh-upstreams + review-kind gates keep it fail-closed; inserted-stage kills the agentx dead-end.
4. `CycleService.Refresh.cs` — iterate `SafeReinterpret` AND `ReBindContentEqual`; re-derive the plan after EACH safe re-stamp so a chain settles in one pass. *Why:* makes `--apply-safe` actually clear the cascade.
5. `CycleService.Stamp.cs` — after `_store.Write`, auto-invoke the safe-only cascade over the stamped stage's dependents (closure-bounded, prereq-first, re-entrancy-guarded via an ambient flag); never auto-stamp RerunRequired/ChangeSetDiffers/inserted; wrap so a cascade failure never fails the primary stamp. *Why:* re-running the one changed stage auto-fixes the rest — the operator's ask.
6. `CycleService.TransitionRecords.cs` — in `RebaseProofsToHead`, also recompute `PrerequisiteArtifactHashes`. *Why:* a transition currently rebinds everything else, leaving a false `PrereqArtifactChanged` on the next check.
7. `Hx.Tooling.Contracts/CycleState.cs` (optional) — additive nullable `StageGraphFingerprint` (the ordered prereq-id set the proof was stamped against). *Why:* makes edge-only-vs-content distinction first-class + migration-detectable. Additive/nullable.
8. `Hx.Doti.Core/DotiInstaller.cs` — before `WriteBaseline`, an orphan-prune pass: prior-managed skill paths under an agent SkillsRoot not in `DotiRenderer.BuildTargets`, sourced via an on-disk `*-doti-*` scan, deleted ONLY on a clean baseline-hash match (else preserved/blocked), empty dirs pruned, recorded in `ObsoleteAssets`. Generalize `RemoveObsoleteLegacyDotiAssets` beyond the `doti/` prefix. *Why:* the renumber-orphan fix; clean-only guard preserves operator edits.
9. `Hx.Doti.Core/DotiPayloadParityChecker.cs` — flag a `*-doti-*` skill dir present in the repo but absent from render targets. *Why:* `payload check` currently can't see orphans.

**Architecture delta:** none structural — a new enum value + a safety tier + a planner gate + a prune pass + a parity check. No ArchUnit family or Sentrux boundary change; no new error code; no CLI surface change.

## CLI surface & error contract

No new/changed command; no new error codes; no `describe` delta. (`doti cycle refresh`/`check`, `doti payload check` already exist; behavior strengthens.)

## Command Availability

| Area | Command | Status |
| --- | --- | --- |
| Build | `dotnet build .\scaffold-dotnet.slnx -c Release` | implemented |
| Test | `dotnet test .\scaffold-dotnet.slnx -c Release` | implemented |
| Cycle | `hx doti cycle check` / `refresh --apply-safe` | implemented |
| Payload | `hx doti payload check --repo .` | implemented |
| Gate | `hx gate run --profile normal/release` | implemented |

## Complexity Tracking

No constitution violation.

## Risks

- Over-rebinding: mitigated by the own-artifact-unchanged + all-upstreams-Fresh + byte-identical-shared-content + review-kind + change-set-bound guards (all must hold).
- Cascade re-entrancy/termination: ambient suppress-flag + closure-bounded + prereq-first + per-pass progress (a non-Fresh step is left, never retried).
- Refresh/Check disagreement: the gate is a strict projection over `report.Prerequisites`, never a second evaluator.
- Orphan-prune destroying an operator-renamed skill: clean-baseline-hash guard; modified → preserved/blocked.
- Gate-proof determinism: `ReBindContentEqual` re-binds only via the existing Stamp path (no new hashing); `ChangeSetDiffers` stays `RerunRequired`; digest byte-unchanged.
- Two extra lifecycle bugs surfaced during this cycle's own setup (CI-release not marked locally; stamp relabels feature on slug-mismatch) — recorded; in scope only if cheap, else a follow-up.
