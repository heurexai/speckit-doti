# Spec: Release CI path portability fix

## Goal

The v0.5.0 release commit must pass the cross-platform CI matrix. The macOS runner failed because a scaffold update test compared the exact temporary hook path string, while macOS resolves `/var` temporary paths to `/private/var` in diagnostics. The product diagnostic was correct; the test assertion was too platform-specific.

## Scope

Included:
- Make the external pre-commit hook refusal test validate the stable managed hook target instead of the host-specific temporary root spelling.
- Preserve the existing product behavior: `hx update` still refuses non-Doti pre-commit hooks and reports the hook diagnostic path.
- Re-run the release profile gate and retarget `v0.5.0` so CI and release assets are built from the fixed commit.

Excluded:
- Changing hook installation, ownership, or update behavior.
- Changing the released version number beyond the intended `0.5.0`.
- Reworking CI workflow structure.

## Requirements

- `FR-001`: The scaffold update test for external pre-commit hooks MUST pass on Windows, Linux, and macOS.
- `FR-002`: The test MUST still assert the `update.hook.external-precommit` diagnostic code.
- `FR-003`: The test MUST still prove the existing external hook content is preserved.
- `FR-004`: The release gate MUST pass before the CI fix is committed.
- `FR-005`: The `v0.5.0` tag MUST point at the CI-fixed commit before the release workflow is considered authoritative.
