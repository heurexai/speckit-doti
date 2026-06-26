# Bug extension — the enforced bug mini-cycle

A bug fix is a small spec-driven cycle of its own: **assess → fix → test**. This extension absorbs that
structure from Spec Kit but keeps doti's enforcement teeth — the boundary is non-forgeable, not honor-system.

| Stage | Role | Command | Fails closed when |
| --- | --- | --- | --- |
| **assess** | read-only verdict/severity/remediation contract | `hx doti bug assess` | — (writes only the assessment) |
| **fix** | the only writer, bound to the assessment | `hx doti bug fix` | no assessment (`bug-assessment-missing`); unbound or not `confirmed` (`bug-fix-unbound`) |
| **test** | honest verification, no over-claiming | `hx doti bug test` | no fix (`bug-fix-unbound`); a `pass` with no evidence is downgraded |

The artifacts live under `.doti/bugs/<bugId>/` (`assessment.json` → `fix.json` → `test.json`). Each stage is
**proof-bound**: the fix binds to the assessment's canonical content hash, the test to the fix's — so a fix can
never float free of the assessment that justified it, and a "fixed" claim can never float free of the fix.

The enforcement lives in `BugCycleService` (`Hx.Doti.Core`); the CLI is a thin wrapper. The per-stage command
guidance is in `commands/speckit.bug.{assess,fix,test}.md`.
