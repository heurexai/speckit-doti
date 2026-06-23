# Tasks: Release CI path portability fix

- [x] Confirm the macOS CI failure is the scaffold update external pre-commit hook test.
- [x] Make the diagnostic path assertion portable across macOS `/var` and `/private/var` temp roots.
- [x] Run `Hx.Scaffold.Tests` locally.
- [x] Build the solution in Release.
- [x] Run the release profile gate.
- [ ] Commit through `doti cycle commit`.
- [ ] Retarget and push `v0.5.0`.
- [ ] Verify the replacement CI and release workflow runs.
