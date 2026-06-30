# speckit.bug.fix

The **only-writer** fix stage of the enforced bug mini-cycle. A fix is bound to the assessment that justified it.

Behavior:

1. Fix the **ROOT cause** the assessment's `remediation` named — the underlying defect, not the surface symptom — with no scope creep beyond it. The fix stage is the only stage that changes code.
   - A symptom patch / bandaid / workaround is **FORBIDDEN**: do not swallow the exception, clamp the bad value, special-case the failing input, retry-until-it-passes, or otherwise mask the symptom while the root defect remains. Correct the defect at its origin.
   - **DO the root fix.** Do NOT present the operator a bandaid-vs-root choice, and do NOT ask which fix approach to apply — you already have the confirmed root-cause remediation; implement it. Surface to the operator ONLY a genuine blocker: a real design decision you cannot make at ≥95% confidence, missing access/credentials, or an unverifiable premise. A bandaid is never an option you offer.
2. Record the fix, bound to the bug's current (confirmed) assessment: `hx doti bug fix --repo . --bug <NNN-slug> --summary "<one line>" --changed "<comma-separated paths>"`. The CLI auto-binds the fix to the assessment's content hash.

Enforcement (fails closed):

- **`bug-assessment-missing`** — there is no assessment for this bug. Run `speckit.bug.assess` first.
- **`bug-fix-unbound`** — the assessment is not `confirmed`, or the fix is not bound to its content hash. A fix can never float free of a confirmed assessment.

## Next

Run `speckit.bug.test` (`hx doti bug test`) to verify the fix honestly.
