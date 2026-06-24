# Plan: Doti Cycle State Gitignore

## Goal

Make `.doti/cycle-state.json` and `.doti/gate-proof.json` ignored anywhere Doti is installed or updated, matching the paths used by `CycleStateStore` and `GateProofStore`.

## Design

- Add a shared Doti gitignore helper in `Hx.Doti.Core`.
- The helper plans and applies only missing Doti runtime-state ignore entries.
- Preserve existing `.gitignore` content and append missing entries under a Doti comment block.
- Call the helper from `DotiInstaller.Install` so arbitrary `doti install` targets are protected.
- Call the helper from `ScaffoldUpdateService` so existing updated repos are repaired.
- Add `.gitignore` to update planned/changed paths only when the helper needs to write it, so dirty path protection still applies.
- Keep `CycleStateStore.RelativePath` unchanged.

## Proof

- Unit tests cover helper idempotence and preserving `.nomos/cycle-state.json`.
- Update tests cover an existing repo receiving `.doti/cycle-state.json` and `.doti/gate-proof.json`.
- Doti installer tests cover creating/updating `.gitignore`.
- Command proof reproduces the two-stage stamp path in a temporary Git repo with the corrected ignore entries.
