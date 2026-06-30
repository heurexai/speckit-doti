# doti-drift-fix

Purpose: patch a drift that `/08-doti-drift-review` surfaced — a spec↔code or code↔docs gap — by correcting the side that is WRONG, then reconciling the cycle state. The spec is the source of truth: a spec↔code gap is closed by fixing the CODE in-cycle, never by downgrading or deferring the spec. A utility: it runs inside the active feature cycle and never reorders `/01`–`/09`.

Command-backed behavior:

1. Read `.doti/agent-context.md`.
2. Classify the drift's blast radius: `hx doti review-context --repo .` projects the change set into change categories + applicable arch-review lenses, so you patch with the right scope (a contract/CLI/production-code drift reaches more lenses than a prose fix).
3. Take the advisory semantic hint, never a verdict: `hx doti drift-candidates --repo .` reports changed-code chunks that sit semantically close to a doc/skill/help section — drift the deterministic grep can miss. It reports the active engine and skips cleanly when no model is provisioned. CRUCIAL: an empty candidate list is NOT a clean-bill signal — only the deterministic `/08-doti-drift-review` axes clear drift. Treat every candidate as a spot to look, then confirm or dismiss it deterministically.
4. Fix the wrong side, in-cycle: correct the CODE (or the docs/prose, when the docs are the drifted artifact) so it matches the approved spec/plan. Never edit the spec to match the code — that launders a defect into the source of truth. Build it modularly, exactly as `/07-doti-implement` requires.
5. Reconcile the cycle honestly: `hx doti cycle refresh-plan --target <stage>` shows which stamps can be re-bound (`SafeReinterpret`) vs re-run (`RerunRequired`) vs assessed (`ReviewedNoImpact`); `hx doti cycle refresh --target <stage> --apply-safe` re-binds the safe ones; re-run the rest through their `/0N` skill. For a `ReviewedNoImpact` stage, the engine surfaces the upstream diff — read it and decide: re-author if it affects this stage, or `hx doti cycle review-rebind --target <stage> --attest no-impact --reason "<why>"` if it does not. Clearing the flag without assessing the diff is forbidden (a bare `stamp` of such a stage refuses). Then `hx doti cycle check` must pass before you continue.

Expected output: the review-context classification, the advisory drift candidates (with the active engine + the "absence ≠ clean" caveat), the in-cycle CODE/docs patch that closes the gap, and a green `doti cycle check` — with the spec left untouched as the source of truth.

## Next

Re-run `/08-doti-drift-review` to confirm the diff now matches the approved design, then resume your cycle.
