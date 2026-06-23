# Plan: Release CI path portability fix

## Design

The failure is isolated to `Hx.Scaffold.Tests.ScaffoldCommandsTests.Update_refuses_to_overwrite_external_precommit_hook`. The assertion should continue to prove the diagnostic category, but it should not compare the absolute temporary directory prefix. The stable contract is that the diagnostic points at the repository's `.git/hooks/pre-commit` hook.

Implementation:
- Replace the exact absolute path comparison with a helper that normalizes separators and checks the `.git/hooks/pre-commit` suffix.
- Keep the diagnostic code assertion and hook-content preservation assertion.
- Build Release and run the release profile gate.
- Commit through `doti cycle commit`, move `v0.5.0` to the fixed commit, and push `main` plus the retargeted tag.

## Risks

- A suffix-only check could hide a completely unrelated path ending in the same hook suffix, but the diagnostic code and preserved external hook assertions keep the test scoped to the update plan under test.
- Moving a published tag can trigger a second release run; the release workflow uses `gh release upload --clobber`, so fixed assets can replace the earlier assets.
