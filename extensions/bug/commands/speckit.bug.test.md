# speckit.bug.test

The **honest verification** stage of the enforced bug mini-cycle. No over-claiming.

Behavior:

1. Verify the fix actually resolves the assessed bug at its **root**: reproduce the original failure and confirm it is gone — because the root cause was corrected, not because the symptom was masked — and run the relevant tests.
2. Record the result honestly, bound to the fix: `hx doti bug test --repo . --bug <NNN-slug> --outcome <pass|fail> --evidence "<what you ran and observed>"`.

Enforcement (fails closed / no over-claiming):

- **`bug-fix-unbound`** — there is no fix for this bug. Run `speckit.bug.fix` first.
- A `pass` **requires evidence**. An evidence-free `pass` is downgraded to `fail` — the stage cannot claim a fix works without showing the work.
- Report the true result: if the verification did not pass, record `fail` and say what remains. Do not declare done without proof.

## Next

A `pass` closes the bug mini-cycle. A `fail` returns to `speckit.bug.fix` (or `speckit.bug.assess` if the root cause was wrong).

In a bug-only repo (no numbered feature cycle), a `pass` may make this test-passed bug a release-train member. If a subsequent release run's `release-documentation` gate demands the bug's slug in `README.md`/`CHANGELOG.md`, edit those two files and commit the fix with the sanctioned `hx doti bug release-docs --repo . --bug <NNN-slug>` — never a manual `DOTI_SANCTIONED_COMMIT=1` bypass.
