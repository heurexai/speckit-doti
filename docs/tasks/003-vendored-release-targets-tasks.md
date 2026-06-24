# Tasks: Vendored Release Targets

- [x] T001 - Specify the vendored-release regression and acceptance criteria in `docs/specs/003-vendored-release-targets.md`.
- [x] T002 - Add `.doti/release.json` release-target manifest support and validation in `src/Hx.Scaffold.Core/Release/ReleaseTargetManifest.cs`.
- [x] T003 - Change `LocalReleaseService` to publish the manifest-declared target and include the target in release proof.
- [x] T004 - Allow each target manifest to define the default release-root environment variable while preserving `--release-root-env` override behavior.
- [x] T005 - Write a default `.doti/release.json` during `hx new`.
- [x] T006 - Add speckit-doti's own `.doti/release.json`.
- [x] T007 - Preserve existing `.doti/release.json` during update and auto-create it only when an older repo has one unambiguous executable project.
- [x] T008 - Update help/docs/generated agent context for manifest-driven vendored releases.
- [x] T009 - Add and run focused tests for manifest validation, target default release-root selection, and update-created release manifests.
- [x] T010 - Run build, render-skills check, local `hx release` proof, and Doti cycle stamps.
