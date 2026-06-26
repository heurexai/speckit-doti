# doti-bug

Purpose: run a bug fix as an ENFORCED mini-cycle — assess (read-only) → fix (bound to the assessment) → test (honest) — so a fix can never float free of the assessment that justified it, and a "fixed" claim can never float free of the fix.

Command-backed behavior:

1. Read `.doti/agent-context.md`.
2. **Assess** (read-only): reproduce + RCA the bug, then record the verdict/severity/remediation contract — `hx doti bug assess --repo . --bug <NNN-slug> --verdict <confirmed|rejected|needs-info> --severity <critical|high|medium|low> --remediation "<the concrete change>" --summary "<one line>"`. This stage writes ONLY `.doti/bugs/<bugId>/assessment.json`; it never changes code. Detail: `extensions/bug/commands/speckit.bug.assess.md`.
3. **Fix** (only writer): when the verdict is `confirmed`, make the change that implements the remediation, then record it bound to the assessment — `hx doti bug fix --repo . --bug <NNN-slug> --summary "<one line>" --changed "<paths>"`. Fails closed: `bug-assessment-missing` if there is no assessment, `bug-fix-unbound` if the assessment is not confirmed or the fix is unbound. Detail: `extensions/bug/commands/speckit.bug.fix.md`.
4. **Test** (honest): verify the fix and record the true result — `hx doti bug test --repo . --bug <NNN-slug> --outcome <pass|fail> --evidence "<what you ran and observed>"`. A `pass` requires evidence; an evidence-free pass is downgraded so the stage cannot over-claim. Fails closed `bug-fix-unbound` if there is no fix. Detail: `extensions/bug/commands/speckit.bug.test.md`.
5. Each stage is proof-bound: the fix binds to the assessment's canonical content hash, the test to the fix's. Report the stage outcomes and the bug-id.

Expected output: the `CliResult` envelope for each stage (`assess`/`fix`/`test`) — verdict and the bound hashes — or the fail-closed error code (`bug-assessment-missing` / `bug-fix-unbound`).

## Next

A passing test closes the bug. Resume your active cycle stage, or start a feature with `/01-doti-specify`.
