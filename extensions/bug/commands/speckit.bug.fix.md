# speckit.bug.fix

The **only-writer** fix stage of the enforced bug mini-cycle. A fix is bound to the assessment that justified it.

Behavior:

1. Make the code change that implements the assessment's `remediation` — no scope creep beyond it. The fix stage is the only stage that changes code.
2. Record the fix, bound to the bug's current (confirmed) assessment: `hx doti bug fix --repo . --bug <NNN-slug> --summary "<one line>" --changed "<comma-separated paths>"`. The CLI auto-binds the fix to the assessment's content hash.

Enforcement (fails closed):

- **`bug-assessment-missing`** — there is no assessment for this bug. Run `speckit.bug.assess` first.
- **`bug-fix-unbound`** — the assessment is not `confirmed`, or the fix is not bound to its content hash. A fix can never float free of a confirmed assessment.

## Next

Run `speckit.bug.test` (`hx doti bug test`) to verify the fix honestly.
