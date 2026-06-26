# speckit.bug.assess

The **read-only** assess stage of the enforced bug mini-cycle (assess → fix → test).

Behavior:

1. Investigate the reported bug — reproduce it, read the relevant code, and establish root cause (RCA, not symptom).
2. Produce a verdict/severity/remediation **contract** and write nothing else. This stage NEVER changes code; it only records the assessment.
   - **Verdict**: `confirmed` (a real bug to fix), `rejected` (not a bug / works as designed), or `needs-info` (cannot decide yet).
   - **Severity**: `critical | high | medium | low`.
   - **Remediation**: the concrete change the fix must make. The fix stage is BOUND to this assessment, so be precise and evidence-based.
3. Record it: `hx doti bug assess --repo . --bug <NNN-slug> --verdict <verdict> --severity <severity> --remediation "<remediation>" --summary "<one line>"`.

Enforcement: the fix stage fails closed (`bug-assessment-missing`) until this assessment exists, and (`bug-fix-unbound`) unless the assessment is `confirmed` and the fix is bound to its content hash.

## Next

When the verdict is `confirmed`, run `speckit.bug.fix` (`hx doti bug fix`). A `rejected` or `needs-info` verdict ends the cycle here.
