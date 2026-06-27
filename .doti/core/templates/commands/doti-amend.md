# doti-amend

Purpose: amend an already-stamped cycle stage after an approved change to its artifact (a spec/plan/tasks edit, or a follow-up code change), and reconcile the cycle state HONESTLY — re-establish freshness through the recovery plan instead of hand-guessing which stamps still hold. A utility: it runs inside the active feature cycle and never reorders `/01`–`/09`.

Command-backed behavior:

1. Read `.doti/agent-context.md`.
2. Classify what actually changed: `hx doti review-context --repo .` projects the current change set into change categories and which arch-review lenses it touches. Use it to scope the amendment — a docs/prose-only edit reaches different stages than a production-code edit.
3. Derive the recovery plan, never a guess: `hx doti cycle refresh-plan --target <stage>` reports, per transitively-required stage, whether its existing stamp is a `SafeReinterpret` (the artifact is unchanged under canonical hashing — the stamp can be re-bound to the new change set), `RerunRequired` (the artifact changed — the stage must be re-run through its `/0N` skill), or `NotBound` (no artifact binding). This is the same freshness logic `doti cycle check` enforces, surfaced as a plan.
4. Apply only the safe re-bindings: `hx doti cycle refresh --target <stage> --apply-safe` re-stamps the `SafeReinterpret` stages. It NEVER fabricates a stamp for a `RerunRequired` stage — re-run that stage's `/0N` skill so its proof is earned, then refresh again.
5. Confirm the cycle is whole: `hx doti cycle check` must pass (every transitive prerequisite stamped + fresh) before you continue. amend reconciles within the cycle; it does not invent a new stage or change the `/01`–`/09` order.

Expected output: the review-context classification, the recovery plan (per-stage `SafeReinterpret`/`RerunRequired`/`NotBound`), the stages re-bound by `--apply-safe`, and the list of stages you re-ran by hand — ending with a green `doti cycle check`.

## Next

Resume your active cycle stage. If the amendment touched code already under review, run `/08-doti-drift-review` to reconcile the diff against the approved design.
