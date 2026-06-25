# Spec: Task Hash-Gated Velopack Completion

> WHAT and WHY only. This feature fixes the release-governance gap exposed by `v0.7.3`: Doti allowed release even though the Velopack task ledger still had unchecked work and the GitHub release workflow still produced raw archives.

## Goal

Doti gates must make the task ledger a coded release and implementation input, not an advisory document. A feature cannot pass the implementation/release gate while required tasks remain unchecked, and an agent cannot mark a task complete and later rewrite the task text without invalidating a canonical task hash.

This spec also carries forward the unfinished Velopack install/update work from `docs/specs/005-velopack-install-update.md` and `docs/tasks/005-velopack-install-update-tasks.md` so the next release cannot pass until Velopack installer/update artifacts and release proof are actually command-backed.

This spec also fixes workflow discoverability. Doti skills must be named and rendered with an explicit numeric workflow order, and every template/command ending must use the same coded next-step directive so Codex, Claude, and other agents receive identical stage guidance.

This spec also changes the Doti cycle from a single end-of-cycle commit model to a codified stage-transition commit model. When a later workflow step starts, Doti/Hx code must automatically finalize and commit the previous completed step before the new step runs. For example, starting `clarify` while the current cycle stage is `specify` must make Doti/Hx commit the `specify` state first, using the default generated subject `<stage>: <NNN-feature-slug>` such as `specify: 006-task-hash-gated-velopack-completion`, plus stable Doti trailers. This is not an instruction for agents to invoke a commit command; the next stage command must perform, verify, or refuse the previous-stage commit itself.

Because commits become automatic stage-transition behavior, the explicit `/doti-commit` workflow step must disappear. After `08-Drift-Review`, Doti must allow the operator to start another numbered spec cycle instead of forcing an immediate release. A later release can then aggregate one or more completed spec cycles into a single release proof and version.

## Current Failure RCA

The current release gate code in `tools/Hx.Gate.Core/GateRunner.cs` emits hygiene, Gitleaks verification, Sentrux verification, affected-change planning, restore/build/test, architecture, skill drift, Sentrux check, version calculation, and security scan. It does not emit a task-completion step.

The Doti cycle commit path verifies fresh stage proofs, a persisted gate proof, affected-test proof hashes, and a clean staged scope. It records task artifact hashes as part of stage freshness, but it does not parse the active task file for unchecked Markdown tasks and it does not validate per-task completion hashes.

Because of that gap, `docs/tasks/005-velopack-install-update-tasks.md` still contained unchecked Velopack tasks while `/doti-release` could pass the current release gate and publish archive-based `v0.7.3` artifacts. The gate proved the old release ladder, not the intended Velopack completion state.

The current workflow guidance also has duplicated next-step text. `doti/core/skills.json` renders `nextStage` text into Codex and Claude skill files, while the command templates in `doti/core/templates/commands` contain independent `## Next` sections. Because those endings are hand-maintained in more than one place, they can become inconsistent across agents and command surfaces.

The current cycle-state freshness model is also built around one uncommitted working diff until the final `doti cycle commit`. If Doti simply commits the `specify` diff without advancing the cycle baseline, the next stage can treat the previous diff-bound proof as stale. Stage-transition commits therefore require a first-class cycle model change: the previous stage's proof must be recoverable from the transition commit and the active cycle baseline must advance before the newly requested stage begins.

The current workflow also treats `/doti-commit` as a separate stage and routes `/doti-drift-review` to `/doti-commit`, then `/doti-release`. That no longer matches the desired model once stage changes are committed automatically. It also makes multi-spec release trains awkward because each feature cycle appears to be driving toward an immediate release rather than allowing another spec to be queued first.

## Scope

Included behavior:

- Add a coded task-completion gate step to Doti gates.
- Make the task-completion gate fail closed for implementation and release readiness when the active feature task file has unchecked required tasks.
- Require every checked task to carry a canonical hash that proves the checked task body has not changed since it was marked complete.
- Make task hashes ignore formatting-only whitespace and end-of-line differences while detecting meaningful content changes.
- Carry forward all unfinished Velopack tasks from feature `005-velopack-install-update` into the active completion work.
- Require the Velopack release gate to prove Velopack artifacts/update metadata, not only raw zip/tar archives.
- Make release proof fail if the GitHub release workflow or local release result exposes only raw archives as primary install artifacts.
- Render Doti skills with explicit two-digit workflow-order prefixes so the first skill is visibly `01-Specify`, followed by `02-Clarify`, `03-Plan`, `04-Tasks`, `05-Analyze`, `06-Arch-Review`, `07-Implement`, `08-Drift-Review`, and `09-Release`. There is no separate commit skill/stage in the new workflow.
- Codify workflow stage order, skill naming, and next-step wording in one workflow-stage registry used by Hx/Doti renderers and command/template endings.
- Make every generated next-step directive state whether the next stage is required, optional/advisory, conditional, or an alternate path.
- Add a sanctioned, code-enforced stage-transition commit path so starting the next Doti stage automatically commits the previous completed stage by Doti/Hx tooling before the requested stage runs.
- Generate default transition commit subjects in the form `<stage>: <NNN-feature-slug>`, for example `specify: 006-task-hash-gated-velopack-completion`, with stable Doti trailers in the commit body.
- Make stage-transition commits advance cycle freshness so later stages do not become stale solely because the previous stage was correctly committed.
- Remove the explicit `/doti-commit` step from the workflow, rendered skills, next-step guidance, command templates, and cycle stage model.
- Allow completed drift-review cycles to branch back to `/doti-specify` for another numbered feature instead of forcing `/doti-release`.
- Allow `/doti-release` to aggregate multiple completed feature cycles into one release train.
- Require the release stage to update README and repo documentation with release notes before release proof is accepted, so completed changes do not leave documentation debt.
- Require the Velopack-backed Doti update path to migrate existing repos away from the old root `doti/` directory and remove obsolete/orphaned Doti workflow assets that are no longer part of the supported scaffold layout.
- Make the released Doti product a Velopack installer/update package for the main `hx` executable and bundled Doti payload, not a source archive. The installer installs or updates `hx` into an operator-configured and operator-confirmed application directory. From that installed directory, `hx` is then used to install, repair, or update Doti workflow assets in any explicit target repo.
- Move hx operational configuration to Microsoft.Extensions.Configuration backed by a local JSON file stored beside `hx.exe`; release-root behavior must come from that local config rather than release command flags or environment-variable overrides.
- Apply every Doti workflow/runtime change in both places that matter: this speckit-doti repo's self-hosted Doti assets and the scaffold-installed Doti payload used by generated repos.
- Document and expose the task-completion gate in command help, `describe --json`, agent context, and generated skills.

Excluded behavior:

- Treating a human-written task checklist as sufficient proof without code-backed validation.
- Letting an agent bypass the task-completion gate by editing task files, deleting task files, renaming a task file away from the active feature, or changing a checked task after hashing.
- Releasing a feature with unchecked required tasks by using a manual tag, raw GitHub release edit, or direct CI artifact upload.
- Reopening already-completed historical cycles unless their task ledger is part of the active feature being released.
- Making whitespace-only Markdown formatting a task-hash failure.
- Hand-maintaining per-agent skill names or per-template next-step wording outside the coded workflow-stage registry.
- Allowing raw `git commit`, hand-written stage commit messages, agent-invoked commit commands, agent-managed commit sequencing, or ad hoc empty commits to satisfy Doti stage completion.
- Keeping `/doti-commit` as a required or optional normal workflow stage after stage-transition commits are implemented.
- Forcing every completed spec cycle to release immediately before the next spec can start.
- Treating generated release notes as sufficient when README or other repo documentation still describes old behavior.
- Leaving the old root `doti/` directory or obsolete Doti workflow files in upgraded repos when the supported layout has moved to `.doti/`.
- Deleting custom/live repo configuration while cleaning obsolete Doti-owned assets.
- Shipping source-code archives as the release product instead of installer/update artifacts.
- Treating a non-empty repo target with no Doti installation as an upgrade rather than a normal first-time Doti install.
- Installing `hx` into an unspecified or hidden app directory without an operator-visible directory configuration/confirmation path.
- Requiring released-package users to clone `speckit-doti`, compile source, or run `dotnet run --project tools/Hx.Runner.Cli` to update Doti assets in another repo.
- Requiring an interactive Windows folder-picker installer or MSI package in this feature; the supported Windows install-directory UX is Velopack `Setup.exe --installto <DIR>` plus installed `hx` path reporting.
- Letting repo asset install/update guess the current directory, or mutate any repo directory when installed `hx` was not given an explicit target repo directory.
- Reading hx operational release-root configuration from the target repo, process current directory, or environment variables instead of the JSON config beside the running `hx.exe`.
- Keeping release-root command options such as `--release-root`, `--release-root-env`, or `--save-release-root` as the normal hx release configuration mechanism.
- Updating only this repo's `.doti`/workflow assets while leaving the scaffold-installed Doti payload stale, or updating only the scaffold payload while leaving this repo's self-hosted Doti workflow stale.

## Carried-Forward Velopack Scope

The next Velopack completion work MUST include the unfinished tasks from `005-velopack-install-update`, including:

- Release-intent and command-surface removal tests that were still unchecked.
- GitVersion increment-signal validation.
- Pinned and verified Velopack CLI/tool metadata.
- Dedicated Velopack staging/packaging services and payload inspection.
- Installed `hx` Velopack identity in version reporting.
- Generated-app Velopack release defaults, tests, and package inspection.
- Microsoft.Extensions.Configuration-based hx configuration in the scaffold and in the released hx executable.
- `.doti/`-only render/source authority and legacy root `doti/` migration.
- Velopack-backed update/migration behavior that removes the old root `doti/` directory and obsolete Doti-owned files from existing repos while preserving custom/live configuration.
- Velopack installer behavior for a configurable/confirmed `hx` application install directory.
- Installed-`hx` repo asset behavior for missing, empty, non-empty without Doti, and existing Doti-enabled target directories.
- Managed-asset canonical baselines for `.doti/` source assets.
- Release-gate proof for Velopack metadata, tag identity, staged vendored asset hashes, and failure when only raw archives are present.
- GitHub release workflow changes so Velopack artifacts/update metadata are primary release outputs.
- Stable release-specific error codes and final command/model proof.

## Functional Requirements

- `FR-001`: `gate run --profile normal` MUST include a coded `task-completion` step for the active feature when the gate is being used as implementation or stage-transition-readiness proof.
- `FR-002`: `gate run --profile release` MUST include a coded `task-completion` step for the active feature and MUST fail if any required task is unchecked.
- `FR-003`: The task-completion gate MUST discover the active feature from Doti cycle state and resolve the expected task file using the numbered feature slug.
- `FR-004`: The task-completion gate MUST fail closed if the active feature has no matching task file.
- `FR-005`: The task-completion gate MUST fail closed if the active task file contains any required `- [ ]` Markdown task.
- `FR-006`: Every checked required task MUST include a machine-readable task hash marker generated by Doti tooling.
- `FR-007`: The task-completion gate MUST fail closed if a checked task is missing its task hash marker.
- `FR-008`: The task-completion gate MUST fail closed if a checked task's stored hash does not match the hash recomputed from its current canonical content.
- `FR-009`: Task hashes MUST be canonical across CRLF/LF line endings and whitespace-only formatting changes.
- `FR-010`: Task hashes MUST detect meaningful changes to task id, requirement references, success-criteria references, task text, declared files, declared commands, dependencies, or proof expectations.
- `FR-011`: Task hashes MUST exclude the checkbox state and the hash marker itself from the hashed content so changing `[ ]` to `[x]` does not change the content hash.
- `FR-012`: Task hashes MUST include a stable task identity, at minimum the task file path, feature slug, and task id, so duplicate copied task text cannot share one ambiguous completion proof.
- `FR-013`: Doti MUST provide a command-backed way to stamp or refresh checked task hashes; manual hash invention is not proof.
- `FR-014`: The task hash command MUST refuse to hash unchecked tasks as completed.
- `FR-015`: The task-completion gate MUST report exact path, task id, line number, and reason for each unchecked, missing-hash, duplicate-hash, or mismatched-hash failure.
- `FR-016`: The stage-transition commit path MUST refuse to finalize a stage that requires gate proof unless the persisted gate proof includes a passing task-completion step for the current change set.
- `FR-017`: `doti-release` MUST refuse to push tags or verify GitHub release artifacts unless the release gate proof includes a passing task-completion step.
- `FR-018`: Release proof for Velopack completion MUST fail when GitHub release assets are only raw `.zip`/`.tar.gz` source/archive installers without Velopack installer/update metadata.
- `FR-019`: Release proof MUST identify Velopack artifacts and update metadata by type, RID/channel, version, package id, and hash.
- `FR-020`: The carried-forward Velopack work MUST not be releasable until all tasks covering `005` unfinished scope are checked and hash-valid.
- `FR-021`: `describe --json`, human help, generated skills, README, and agent context MUST describe the task-completion gate and the task-hash workflow.
- `FR-022`: The task-completion gate MUST be implemented in production gate/cycle code, not only as agent instructions, documentation, or a test helper.
- `FR-023`: Doti skill names and display titles MUST be numerically prefixed according to the canonical workflow order. Display titles MUST use the exact two-digit format `01-Specify`, `02-Clarify`, `03-Plan`, `04-Tasks`, `05-Analyze`, `06-Arch-Review`, `07-Implement`, `08-Drift-Review`, and `09-Release`. Rendered skill identifiers MUST use a sortable equivalent such as `01-doti-specify` through `09-doti-release`. There MUST NOT be a `doti-commit` skill in the normal workflow.
- `FR-024`: The numeric skill order MUST be generated from a coded workflow-stage registry, not hand-maintained in rendered Codex skills, Claude skills, or copied Markdown templates.
- `FR-025`: The workflow-stage registry MUST define, for each stage, the stage id, ordinal, command name, skill name, display title, required/optional/conditional status, next-stage relationship, and canonical next-step wording.
- `FR-026`: Rendered Codex and Claude skills MUST use the same numeric skill names and the same next-step wording for the same stage.
- `FR-027`: Hx/Doti command output and command-template ending text MUST be generated from the same workflow-stage registry so agents receive consistent final "next stage" wording regardless of which agent surface they use.
- `FR-028`: Optional or advisory stages, including architecture review when architecture impact is absent, MUST be explicitly identified as optional/advisory or conditional in the next-step directive instead of being implied mandatory.
- `FR-029`: `describe --json` or an equivalent machine-readable command contract MUST expose the workflow stage order, numeric skill names, and next-stage relationships so agents can discover the workflow without relying on prose.
- `FR-030`: Render drift checks MUST fail when rendered skills or command-template endings contain stale, missing, or hand-written next-step wording that does not match the workflow-stage registry.
- `FR-031`: Doti MUST provide a sanctioned command-backed stage-transition commit path that is executed automatically by Doti/Hx code at the start of the next workflow stage, before the newly requested stage runs.
- `FR-032`: A stage-transition commit MUST be created for every completed previous stage in the canonical workflow order, including review or no-file-change stages, so Git history records the stage boundary.
- `FR-033`: Stage-transition commits MUST use Doti-generated commit messages derived from the workflow-stage registry and the active feature slug. The default subject MUST be `<stage>: <NNN-feature-slug>`, for example `specify: 006-task-hash-gated-velopack-completion`.
- `FR-034`: Stage-transition commit messages MUST include stable Doti trailers, including feature slug, stage id, stage ordinal, stage proof hash, previous stage commit when present, runner identity, and artifact/gate proof identifiers when applicable.
- `FR-035`: After a stage-transition commit succeeds, Doti MUST advance the active cycle baseline so the newly requested stage's prerequisite check treats the committed previous stage as fresh instead of stale because the working diff was cleared by Git.
- `FR-036`: Doti cycle status/check/stamp MUST recover stage freshness from committed stage trailers if `.doti/cycle-state.json` is missing, stale, or not yet updated after a successful stage-transition commit.
- `FR-037`: A stage-transition commit MUST fail closed when unrelated unstaged, staged, or untracked files would be included or hidden from the stage boundary; the diagnostic MUST name the blocking paths.
- `FR-038`: A no-file-change stage commit MUST be explicit, Doti-generated, and trailer-backed; raw `git commit --allow-empty` MUST NOT satisfy the Doti cycle.
- `FR-039`: The final release stage MUST not collapse prior stage commits into one squashed commit unless the operator explicitly asks for a history rewrite outside the normal Doti cycle.
- `FR-040`: The pre-commit hook and commit chokepoints MUST continue to block bare Git commits while allowing only sanctioned Doti stage-transition commits and sanctioned release/tag commits.
- `FR-041`: Agent instructions MUST NOT be the enforcement mechanism for per-stage commits. Rendered skills and templates may tell agents to run the next Doti stage, but the next stage command itself MUST create, verify, or refuse the previous stage-transition commit before doing stage work.
- `FR-042`: If the Doti/Hx next-stage command cannot create the required previous-stage transition commit, it MUST return a failing `CliResult` with exact blockers and MUST NOT allow the cycle to advance to the requested stage.
- `FR-043`: Agents MUST NOT be required or expected to invoke a separate commit command between stages. The only normal action is invoking the next Doti workflow step; transition commit behavior is internal to Doti/Hx.
- `FR-044`: `/doti-commit`, `doti cycle commit`, and their rendered skills/templates MUST be removed from normal workflow, help, describe metadata, generated skills, and source command assets. They MUST NOT remain as compatibility commands.
- `FR-045`: After `implement` completes, the workflow registry MUST route to `drift-review`; it MUST NOT allow cycling to `specify` directly from `implement`.
- `FR-046`: After `drift-review` completes, the workflow registry MUST allow the next action to be either `release` or `specify`; `specify` starts another numbered feature cycle for the same eventual release train.
- `FR-047`: Doti MUST track completed-but-unreleased feature cycles so `/doti-release` can aggregate one or more completed specs into a single release proof.
- `FR-048`: `/doti-release` MUST report the feature slugs, stage commit range, task-completion status, gate proof status, and release inclusion status for every feature cycle included in the release train.
- `FR-049`: `/doti-release` MUST fail closed if any included feature cycle has missing/invalid stage-transition commits, unchecked or hash-invalid tasks, missing required gate proof, or a stale drift-review/implementation boundary.
- `FR-050`: Starting a new `specify` after a completed `drift-review` MUST automatically finalize `drift-review` first, then create a new active feature cycle without erasing the completed-but-unreleased cycle from the release train.
- `FR-051`: The release stage MUST instruct the agent, through generated release-stage wording, to update `README.md` and all relevant repo documentation with the release notes for the included release train before release is accepted.
- `FR-052`: The release stage MUST produce or require a documentation update proof that lists every repository documentation surface inspected, whether it was updated, and the reason when no change was required.
- `FR-053`: Release proof MUST fail closed when release notes introduce user-facing, workflow, CLI, installer/update, compatibility, security, or operational behavior that is not reflected in the README and relevant repo documentation.
- `FR-054`: The generated release notes MUST summarize every included feature cycle and MUST be reusable as the source text for README/docs updates, GitHub release notes, and local release metadata.
- `FR-055`: Release documentation updates MUST be committed through the same codified Doti transition/release path and MUST NOT be left as unstaged or advisory follow-up work after release artifacts are produced.
- `FR-056`: The Doti Velopack update/install path MUST migrate the speckit-doti repository itself and existing installed repos from the old root `doti/` directory layout to the supported `.doti/` layout.
- `FR-057`: When upgrading an existing repo with an older Doti workflow, the installer/update path MUST remove the root `doti/` directory after the replacement `.doti/` assets have been installed and validated.
- `FR-058`: The installer/update path MUST remove obsolete Doti-owned files and directories that are no longer part of the supported scaffold layout, including orphaned rendered skills/templates/workflow files that are superseded by the new manifest.
- `FR-059`: Obsolete-asset cleanup MUST be driven by a release-owned manifest of managed Doti assets and removals, not by broad directory deletion or agent judgment.
- `FR-060`: Obsolete-asset cleanup MUST preserve custom/live configuration and repo-owned files, including release configuration, sentrux baselines/configuration, local prerequisites policy state, user docs, and feature specs/plans/tasks unless the manifest explicitly classifies the file as Doti-owned and obsolete.
- `FR-061`: If an obsolete Doti-owned file has local modifications relative to its canonical managed hash, the update MUST fail closed with exact path diagnostics unless the operator explicitly uses a supported force/replace option.
- `FR-062`: The update proof MUST report every removed obsolete Doti-owned file/directory, every preserved custom/live file, and every skipped removal with its reason.
- `FR-063`: The Doti release output MUST be installer/update artifacts only; source-code zip/tar archives MUST NOT be published as the primary release product.
- `FR-064`: The Velopack installer/update experience MUST install or update the main `hx` executable and bundled Doti payload into an operator-configured and operator-confirmed application directory, not into an unspecified hidden directory that the operator cannot see before install.
- `FR-065`: On Windows, the Doti release MUST document and support Velopack `Setup.exe --installto <DIR>` as the install-directory path that satisfies `FR-064`; producing a Velopack MSI/bootstrapper for interactive directory selection is not required for this feature.
- `FR-066`: After Velopack installation or update, the installed `hx` executable from that application directory MUST be the supported operator-facing command for installing, repairing, or updating Doti workflow assets in target repos.
- `FR-067`: Installed `hx` MUST expose a repo asset command equivalent to `hx doti install --repo <target-directory> --agents <agents> [--force] [--json]`; the command MUST NOT require a separate runner executable, source checkout, compilation, or `dotnet run`.
- `FR-068`: If the installed-`hx` repo target directory does not exist, the repo asset command MUST create it, install Doti into it, and report that no scaffold has been generated yet.
- `FR-069`: If the installed-`hx` repo target directory exists and is empty, the repo asset command MUST install Doti into it and report that no scaffold has been generated yet.
- `FR-070`: After installing Doti into a missing or empty repo target directory, installed `hx` MUST print the exact next-step command or instruction for creating/installing the new scaffold in that directory.
- `FR-071`: If the installed-`hx` repo target directory is an existing repo with code and an installed Doti workflow, installed `hx` MUST upgrade Doti in place using the managed-asset update/migration rules.
- `FR-072`: hx and generated scaffold hx code MUST use Microsoft.Extensions.Configuration for operational configuration loading.
- `FR-073`: hx MUST load its operational configuration from a JSON file stored in the same directory as the running `hx.exe`; this file is the local authority for hx operational settings.
- `FR-074`: hx MUST NOT resolve operational configuration from the process current directory, target repo directory, user profile, machine-global location, or environment variable unless a future explicit requirement adds a named provider.
- `FR-075`: The hx local configuration schema MUST include a release-output setting with at least `enabled` and `directory` fields for producing a local release directory.
- `FR-076`: If the hx local configuration file is missing, hx MUST fail hard for every hx command except non-mutating help/describe discovery commands whose purpose is to explain command shape and configuration requirements; it MUST NOT silently create defaults or continue with implicit settings. Operational commands, including `new`, `version`, `release`, prerequisite install, and installer/update target commands, MUST require the executable-adjacent config file.
- `FR-077`: When local release directory output is enabled, hx release MUST fail hard before release mutation if the configured local release directory is missing, blank, invalid, or not absolute.
- `FR-078`: When local release directory output is disabled, hx release MUST NOT require a local release directory and MUST report that local release copy was intentionally disabled by local hx configuration.
- `FR-079`: The current hx release-root command options and arguments MUST be removed from the supported CLI surface; release-root behavior is configured through the local hx JSON config.
- `FR-080`: hx release help, `describe --json`, generated README/docs, and scaffold templates MUST document the local hx configuration file path, schema, defaults, and fail-hard local-release behavior.
- `FR-081`: The scaffold must install or generate an hx local configuration file beside the scaffolded/released hx executable, with local release output enabled and an explicit operator-provided directory or a documented fail-hard placeholder state.
- `FR-082`: The scaffold starter project code MUST include Microsoft.Extensions.Configuration support and follow the same executable-local JSON configuration style as Doti/hx.
- `FR-083`: Generated scaffold starter code MUST load its app/tool configuration from an executable-adjacent JSON file by default and fail hard when required configuration is missing, matching the Doti/hx configuration posture.
- `FR-084`: Every Doti workflow/runtime change in this feature MUST be applied to the speckit-doti repo's self-hosted Doti assets and to the scaffold-installed Doti payload that generated repos receive.
- `FR-085`: The source of truth for shared Doti assets MUST render or validate both the local/self-hosted install and the scaffold payload; drift between them MUST fail the relevant render/check gate.
- `FR-086`: Tests or deterministic drift checks MUST prove that generated repos receive the same numbered skills, workflow registry, next-step wording, transition-commit behavior, release-train behavior, installer/update migration rules, and hx configuration guidance as this repo.
- `FR-087`: The Doti constitution and every rendered constitution-bearing guidance surface MUST be updated from the old "commits go only through `doti cycle commit`" model to the sanctioned automatic stage-transition commit model, while continuing to forbid raw Git commits and agent-authored commit sequencing.
- `FR-088`: Architecture guidance, agent context, `rules/architecture.json`, architecture tests, and `architecture test` output MUST agree on the rule families actually enforced. This feature MUST remove the current overclaim where guidance describes nine ArchUnitNET families while the command-backed gate reports only `cliSurfaceConfinement` and `cliDelegation`; implementation MUST either restore the nine intended families as command-backed tests or change the generated guidance to describe only the implemented families, with tests preventing future drift.
- `FR-089`: If the installed-`hx` repo target directory is non-empty but does not contain an installed Doti workflow, installed `hx` MUST install Doti as a first-time install while preserving existing files and refusing unsafe overwrites.
- `FR-090`: Installed-`hx` repo asset output MUST distinguish `installed-new-target`, `installed-empty-target`, `installed-non-empty-non-doti-target`, and `upgraded-existing-doti-repo` outcomes in human text and JSON/update proof.
- `FR-091`: If the installed-`hx` repo asset command is run without an explicit target repo directory, it MUST fail hard before mutation with a usage/validation diagnostic and MUST NOT default to the current working directory.
- `FR-092`: `hx version --json` and `hx version --repo <path> --json` MUST report the installed `hx` executable path and Velopack install/update identity so an operator can confirm which app directory is driving repo updates.
- `FR-093`: Released-package docs, CLI help, generated skills, and agent context MUST show the normal repo-update path as installed `hx` commands, not source-checkout `dotnet run`; `dotnet run` examples MAY remain only in developer/source sections.

## Success Criteria

- `SC-001`: With one unchecked required task in the active task file, `gate run --profile normal --repo . --json` reports a failing `task-completion` step.
- `SC-002`: With one unchecked required task in the active task file, `gate run --profile release --repo . --json` reports a failing `task-completion` step.
- `SC-003`: With all tasks checked but one checked task missing a hash marker, the task-completion gate fails and names that task.
- `SC-004`: With all tasks checked but one checked task text changed after hashing, the task-completion gate fails and names that task.
- `SC-005`: Reformatting whitespace or changing line endings in a checked task does not change its canonical task hash.
- `SC-006`: Changing a task's requirement references, file paths, commands, or proof text changes its canonical task hash.
- `SC-007`: A release attempt for the Velopack feature fails before tag push if the active task file still contains any unchecked carried-forward Velopack task.
- `SC-008`: A release attempt fails if local or GitHub release proof contains only raw archive outputs and no Velopack installer/update metadata.
- `SC-009`: Gate failure JSON includes enough exact task locations for a non-coder operator to see what is incomplete without reading the whole task file.
- `SC-010`: After all active tasks are checked and hash-valid, task-completion passes and the rest of the normal/release gate remains responsible for build, test, hygiene, security, and release-artifact proof.
- `SC-011`: A rendered skill listing sorts naturally by numeric prefix and displays `01-Specify` first and `09-Release` last.
- `SC-012`: Codex and Claude rendered skills for the same stage contain identical next-step wording from the shared registry.
- `SC-013`: The analyze-stage next-step directive marks architecture review as conditional/advisory when architecture impact is absent and required when architecture-impact evidence exists.
- `SC-014`: Machine-readable command metadata exposes workflow order, numeric skill names, and next-stage relationships.
- `SC-015`: Editing generated skill or command-template next-step text by hand causes render/drift validation to fail until the registry is corrected and assets are re-rendered.
- `SC-016`: Starting `clarify` after `specify` automatically creates or verifies a Git commit with subject `specify: <NNN-feature-slug>` before clarify work begins.
- `SC-017`: Running the next stage after a stage-transition commit does not mark the previous stage stale solely because the previous stage's diff is now committed.
- `SC-018`: If `.doti/cycle-state.json` is deleted after a successful stage-transition commit, `doti cycle status` reconstructs the committed stage boundary from Git trailers without creating a duplicate commit.
- `SC-019`: Starting the stage after a review stage with no file changes still produces an explicit Doti no-file-change transition commit with trailers for that previous review stage.
- `SC-020`: A raw `git commit` or manually created empty commit is rejected by the hook/checkpoint and cannot satisfy stage freshness.
- `SC-021`: Running the next stage command creates or verifies the previous stage-transition commit without relying on a separate agent-authored or agent-invoked commit step.
- `SC-022`: If an agent edits the files for a stage and tries to move on by any path other than the next codified Doti stage command, the cycle refuses to proceed because the required transition commit is missing.
- `SC-023`: Rendered skills and workflow metadata no longer list `doti-commit` as a normal workflow step.
- `SC-024`: After a completed `drift-review`, starting a new `specify` finalizes `drift-review`, records the completed feature as unreleased, and starts the next numbered feature without requiring a release.
- `SC-025`: A release after two completed spec cycles reports both feature slugs and fails if either cycle has unchecked/hash-invalid tasks or missing required proof.
- `SC-026`: A compatibility call to the old commit command returns a clear diagnostic that commit is automatic stage-transition behavior and names the supported next actions.
- `SC-027`: After `implement`, generated next-step wording names `08-Drift-Review` as the next step and does not offer `01-Specify` until `08-Drift-Review` has completed.
- `SC-028`: A release whose notes change CLI behavior but leave README/help documentation stale fails before tag push or artifact publication.
- `SC-029`: Release proof includes a documentation update summary naming README and each inspected doc file with updated/no-change status.
- `SC-030`: GitHub release notes, local release metadata, and updated repo documentation describe the same included feature cycles and release version.
- `SC-031`: Updating an older repo that contains both root `doti/` and `.doti/` removes root `doti/` after validated `.doti/` installation, while preserving repo-owned specs, docs, and custom configuration.
- `SC-032`: Updating a repo with locally modified obsolete Doti-owned files fails with exact path diagnostics and does not partially delete assets.
- `SC-033`: Update proof lists obsolete Doti-owned removals and preserved custom/live configuration files.
- `SC-034`: Release assets contain Velopack installer/update artifacts and metadata, and do not contain source-code zip/tar archives as primary release assets.
- `SC-035`: Running Velopack `Setup.exe --installto <DIR>` installs or updates `hx` under that operator-selected application directory, and `hx version --json` reports that executable path.
- `SC-036`: Running installed `hx doti install --repo <missing-dir> --agents codex,claude --json` creates the repo target directory, installs Doti, and prints the scaffold-next-step instruction for that path.
- `SC-037`: Running installed `hx doti install --repo <empty-dir> --agents codex,claude --json` installs Doti and prints the scaffold-next-step instruction for that path.
- `SC-038`: Running installed `hx doti install --repo <existing-doti-repo> --agents codex,claude --json` upgrades Doti in place without replacing repo source code.
- `SC-039`: Running installed `hx doti install --repo <non-empty-non-doti-repo> --agents codex,claude --json` installs Doti as a first-time install, preserves existing files, and reports that the target was not an upgrade.
- `SC-040`: Running hx release with local release output enabled and no configured directory exits non-zero before creating tags, release artifacts, or filesystem output.
- `SC-041`: Running hx release with local release output disabled succeeds without a configured local release directory and reports local copy as disabled.
- `SC-042`: hx release help and `describe --json` no longer expose `--release-root`, `--release-root-env`, or `--save-release-root`.
- `SC-043`: Moving the target repo or running hx from another current directory does not change the loaded hx config; hx loads the JSON file beside the running executable.
- `SC-044`: Generated scaffold code uses the same Microsoft.Extensions.Configuration-backed local hx config model as speckit-doti itself.
- `SC-045`: Running operational hx commands such as `hx version --repo . --json`, `hx release --repo . --patch --json`, or `hx new ...` when the executable-adjacent config file is absent exits non-zero with a clear missing-config diagnostic before mutation or repo inspection; help and `describe --json` remain non-mutating discovery surfaces that explain the config requirement.
- `SC-046`: A newly generated scaffold starter project includes Microsoft.Extensions.Configuration package references, an executable-local JSON config file, and tests/proof that required config absence fails hard.
- `SC-047`: A scaffold-generated repo contains the same Doti workflow order, rendered skill numbering, next-step directives, and release/update guidance as the speckit-doti repo after rendering.
- `SC-048`: Drift validation fails when this repo's Doti assets and the scaffold-installed Doti payload diverge for any managed Doti file covered by this feature.
- `SC-049`: After rendering, `.doti/memory/constitution.md`, generated agent context, and rendered skill guidance no longer state that `doti cycle commit` is the sole sanctioned commit path; they name automatic Doti stage-transition commits as the sanctioned path and still reject raw Git commits.
- `SC-050`: `architecture test --repo . --json`, `rules/architecture.json`, `.doti/agent-context.md`, and the rendered arch-review skill agree on the same architecture family count and ids; if nine families are documented, the command-backed architecture proof reports those nine families rather than two.
- `SC-051`: Running installed `hx doti install` without a `--repo` target exits non-zero, names the missing target argument, and leaves the filesystem unchanged.
- `SC-052`: Released-package docs and help show installed `hx` as the normal repo-update command path; no non-developer update instructions require `dotnet run` or a `speckit-doti` source checkout.

## Key Entities

- **Active Feature Task File**: The numbered task file for the current Doti cycle feature, normally `docs/tasks/<NNN-feature>-tasks.md`.
- **Required Task**: A Markdown checklist task that represents work required for the active feature. Required tasks are gate-blocking until checked and hash-valid.
- **Task Completion Hash**: A Doti-generated canonical SHA-256 digest attached to a checked task. It proves the task content being marked complete is the same meaningful content later validated by the gate.
- **Canonical Task Content**: The parsed task identity and task body normalized to ignore whitespace and end-of-line differences while retaining meaningful tokens, references, paths, commands, dependencies, and proof expectations.
- **Task Completion Gate Step**: A production `GateRunner` step that validates active task completion and hash integrity, with failure evidence suitable for `CliResult` JSON and release proof.
- **Velopack Release Artifact Proof**: The release evidence that Velopack installer/update artifacts and metadata exist and match the intended package/version/RID, distinct from raw source/archive artifacts.
- **Workflow Stage Registry**: The coded source of truth for Doti stage order, command names, skill names, next-stage relationships, optionality, and canonical next-step wording.
- **Ordered Skill Name**: A generated skill name with a two-digit numeric workflow prefix, making the intended sequence visible and naturally sorted in agent skill lists and rendered docs, for example `01-doti-specify` with display title `01-Specify`.
- **Next Stage Directive**: The final generated stage guidance emitted by command templates and rendered skills, including whether the next action is required, optional/advisory, conditional, or an alternate path.
- **Stage Transition Commit**: A sanctioned Doti/Hx-created Git commit made automatically at the start of the next workflow stage to finalize the previous stage. It records the previous stage boundary with generated message text and trailers so the stage can be audited, recovered, or reverted independently. It is produced by the next stage command, not by agent discretion.
- **Cycle Baseline**: The commit or diff identity that the next stage uses when evaluating prerequisite freshness. In the stage-transition commit model, it advances after each successful transition commit before the requested stage runs.
- **Completed Unreleased Feature Cycle**: A numbered feature cycle whose required stages have been finalized by transition commits but whose changes have not yet been included in a Doti release train.
- **Release Train**: The ordered set of one or more completed unreleased feature cycles included in a single release proof, version, tag, and installer/update artifact set.
- **Release Notes**: The generated human-readable summary of the included release train, intended to feed README/docs updates, GitHub release notes, and local release metadata.
- **Documentation Update Proof**: Release evidence listing documentation files inspected and updated, plus explicit no-change reasons for docs that remain unchanged.
- **Managed Doti Asset Removal Manifest**: Release-owned metadata that names Doti-owned files/directories which must be removed during update because they are obsolete in the current scaffold layout.
- **Obsolete Doti-Owned Asset**: A file or directory previously installed by Doti that is no longer part of the supported layout and can be removed only when the canonical managed hash or manifest identity proves it is safe to delete.
- **Hx Application Install Directory**: The operator-selected or operator-confirmed directory where Velopack installs the main `hx` executable, update metadata, executable-adjacent configuration, bundled Doti payload, release metadata, and vendored assets.
- **Repo Asset Target Directory**: The directory supplied to installed `hx` as the intended repo/scaffold location for Doti asset install or upgrade. It may be missing, empty, non-empty without Doti, or an existing Doti-enabled repo.
- **Scaffold Next-Step Instruction**: The exact post-install message that tells the operator how to create/install the scaffold into a newly created or empty target directory.
- **Hx Local Configuration**: The JSON configuration file stored beside the running `hx.exe` and loaded through Microsoft.Extensions.Configuration as the local authority for hx operational settings.
- **Local Release Output Setting**: The hx configuration section that controls whether local release directory output is produced and, when enabled, the absolute target directory to use.
- **Scaffold-Installed Doti Payload**: The Doti assets bundled into the scaffold and installed into generated repos, which must remain behaviorally identical to the self-hosted Doti assets in this repo for managed workflow/runtime surfaces.

## Deterministic Surfaces

Command-backed or planned deterministic surfaces:

- `Hx.Runner.Cli gate run --profile normal --repo <path> --json`: MUST include `task-completion` and fail closed on active unchecked or hash-invalid tasks.
- `Hx.Runner.Cli gate run --profile release --repo <path> --json`: MUST include `task-completion` and Velopack artifact proof before release is accepted.
- Next-stage transition hook in Doti/Hx code: MUST stamp and commit the previously completed stage with generated message text and trailers, then advance the cycle baseline before the requested stage runs. This internal command path is the enforcement mechanism; rendered agent text is guidance only.
- `Hx.Runner.Cli doti cycle status --repo <path> --json`: MUST distinguish active feature, completed unreleased feature cycles, and released feature cycles.
- Planned task-hash command: MUST stamp or refresh hashes for checked tasks from canonical parsed content and emit exact changed/evaluated task ids.
- `Hx.Scaffold.Cli release --repo <path> --major|--minor|--patch --json`: MUST load hx local configuration from beside the running executable, report Velopack installer/update artifact metadata in `LocalReleaseResult`, and fail closed when local release output is enabled without a configured directory; empty `velopackArtifacts` or source-archive-only output is not release proof.
- hx local configuration file beside `hx.exe`: MUST be loaded through Microsoft.Extensions.Configuration and must own local release directory behavior.
- Velopack Doti installer/update entrypoint: MUST install or update `hx` and its bundled Doti payload into an operator-configured and operator-confirmed application directory.
- Installed `hx doti install --repo <target-directory> --agents <agents> [--force] [--json]`: MUST accept an explicit repo target directory, classify it as missing/empty/non-empty-without-Doti/existing-Doti, install or upgrade Doti assets accordingly, and emit outcome-specific human and JSON proof without source checkout or `dotnet run`.
- Installed-`hx` Doti asset migration: MUST install current `.doti/` assets, remove manifest-declared obsolete Doti-owned assets such as root `doti/`, preserve custom/live configuration, and emit an update proof.
- Workflow stage description/registry surface: MUST expose ordered skill names and next-stage directives for renderers, command help, and machine-readable agent discovery.
- `doti render-skills --check`: MUST verify generated skill names and next-step endings match the workflow-stage registry.
- Scaffold payload drift check: MUST verify this repo's Doti assets and the scaffold-installed Doti payload are generated from the same source or are otherwise byte/canonical-hash equivalent where managed.
- `doti-release`: MUST push tags only after the release gate and local release result prove every included feature cycle, task completion, documentation update proof, and Velopack artifacts.

## Architecture, Sentrux, And Hygiene Impact

- The gate layer gains a task-completion validator that reads Doti cycle state and task files.
- The cycle transition-commit validators must verify task-completion evidence is present in persisted gate proof when the previous stage requires it.
- The cycle-state engine must support committed-stage freshness and trailer-based recovery, not only uncommitted diff freshness.
- The cycle-state engine must support multiple completed unreleased feature cycles and release-train aggregation.
- The next-stage workflow command path must own previous-stage commit creation/refusal; agent-authored raw commits remain invalid even when their message looks correct.
- The release lane must treat missing Velopack artifacts and unchecked tasks as hard failures, not warnings.
- The release lane must include documentation update proof so README/docs drift is blocked before tag push and artifact publication.
- The update/migration lane must support manifest-driven obsolete asset removal, including root `doti/`, without deleting custom/live repo configuration.
- The installed-`hx` repo asset lane must classify repo target directories before mutation, and the release lane must avoid packaging/releasing source archives as the end product.
- The installer lane is split deliberately: Velopack owns the `hx` application install/update location, while installed `hx` owns repo target classification and Doti asset mutation.
- The hx/scaffold configuration layer must use Microsoft.Extensions.Configuration and a local executable-adjacent JSON provider for operational settings.
- The hx release command surface must remove release-root flags and rely on configuration for local release output behavior.
- Doti asset changes must be propagated to both the self-hosted repo install and the scaffold-installed Doti payload, with drift checked in code.
- Task canonicalization must be deterministic, cross-platform, and independent of CRLF/LF differences.
- The skill renderer and command-template renderer must move to a shared workflow-stage registry so names and next-step endings cannot drift per agent.
- Sentrux should cover the task parser/hash validator as production gate code.
- Architecture guidance and command-backed architecture proof must agree; no rendered Doti guidance may claim rule families that `rules/architecture.json` and `architecture test` do not enforce.
- Hygiene must continue to scan task files and release metadata for local paths or sensitive data.

## Assumptions

- The current active feature can be determined from `.doti/cycle-state.json`; when no active cycle exists, release-oriented commands must fail with a clear diagnostic or require an explicit feature argument rather than guessing.
- Existing completed historical task files do not need retroactive hashes unless they are reused as the active release task file.
- Whitespace-insensitive hashing means changes such as indentation, repeated spaces, and CRLF/LF normalization are ignored, but token/content changes remain detectable.
- The carried-forward Velopack work should remain part of the next active implementation/release cycle rather than being silently considered complete because `v0.7.3` was published.
- Numeric skill prefixes can use zero-padded machine names for stable lexical sorting, but the human-facing order must clearly show specify as stage 1.
- A stage that makes no file changes still needs an auditable Git boundary; this should be represented as an explicit Doti no-file-change transition commit with trailers when the next stage starts, not by pretending a raw empty commit is proof.
- The agent's responsibility is to invoke the next Doti/Hx workflow stage and respond to its diagnostics. The command's responsibility is to finalize and commit the previous stage before advancing.
- A completed unreleased feature cycle can be accumulated for a later release train; release remains explicit and does not happen merely because drift review completed. Starting another spec cycle is offered after drift review, not directly after implement.
- Documentation surfaces include README and repo-owned Markdown documentation by default; generated/vendor docs can be excluded only with an explicit no-change or out-of-scope reason in the documentation update proof.
- The old root `doti/` directory is considered a Doti-owned obsolete layout artifact once the `.doti/` layout has been validated, but custom/live configuration must be preserved even when adjacent to Doti-managed assets.
- A missing or empty repo asset target directory is an install/bootstrap case, not a full scaffold generation case; installed `hx` should install Doti assets and the operator should receive the next scaffold command/instruction.
- Existing source code in an upgraded Doti repo is repo-owned and must not be replaced by installer payload.
- The Velopack installer controls only the installed `hx` application directory. It does not infer or mutate a repo target from the process current directory.
- Installed `hx doti install` never infers a repo target from the process current directory; the target repo must be explicitly supplied.
- The hx local configuration file should be named consistently across platforms and live beside the running `hx.exe`/binary; this spec assumes `hx.config.json` unless implementation discovers a stronger established naming convention during planning.
- Local release output is opt-out, not opt-in. A missing local release directory is a blocking configuration error only while local release output is enabled, and a missing hx local configuration file is always a blocking configuration error for hx operational commands. Only non-mutating help/describe discovery commands may run without the config file, and they must document the failure contract.

## Dependencies

- Existing Velopack spec and task artifacts: `docs/specs/005-velopack-install-update.md`, `docs/plans/005-velopack-install-update-plan.md`, and `docs/tasks/005-velopack-install-update-tasks.md`.
- Doti cycle state and gate proof storage.
- Gate proof JSON contracts in `tools/Hx.Tooling.Contracts`.
- Error-code registry additions for task-completion failures and Velopack artifact proof failures.
- Existing skill-rendering source (`doti/core/skills.json`) and command templates (`doti/core/templates/commands`) must be migrated behind the workflow-stage registry.
- Existing cycle stage model (`.doti/workflows/doti/workflow.yml` / `doti/core/workflows/doti/workflow.yml`) must define enough ordering and metadata for generated stage commit messages and trailers.
- Existing workflow definitions and rendered skill surfaces must remove the `commit` stage and any `/doti-commit` next-step guidance.
- Constitution source and rendered constitution-bearing guidance must be migrated with the workflow change so the repository principle and the implemented transition-commit model agree.
- Release-stage templates and generated skill wording must include the documentation update requirement and proof shape.
- The Velopack update/install implementation must carry managed-asset baseline metadata and obsolete-removal metadata for older Doti layouts.
- Release workflows must stop publishing source-code archives as primary product assets and must publish only installer/update artifacts plus required metadata/checksums.
- hx, generated scaffold projects, and scaffold starter project code must reference the Microsoft.Extensions.Configuration packages needed to load JSON configuration from the executable directory.
- Existing release-root CLI docs, help, tests, and command options must be removed or replaced with hx local configuration documentation.
- Scaffold payload generation/tests must be updated alongside the repo-local Doti assets so generated repos do not receive stale workflows.

## Clarifications

### 2026-06-25

- The operator explicitly required the gate to be coded so unchecked tasks fail the implementation gate.
- The operator explicitly required each checked task to have a unique canonical hash that ignores whitespace/end-of-line differences but detects meaningful content changes.
- The operator explicitly required the unfinished Velopack tasks/specification from the previous run to be carried forward.
- The operator explicitly required Doti skill names to show numeric workflow order, with specify first.
- The operator explicitly required Hx/Doti to emit consistent template-ending next-step wording across agents, including whether a next stage such as architecture review is optional.
- The operator required Doti stage state to be captured in Git with a proper generated commit message so each stage can be easily reversed.
- The operator clarified that per-stage commits must be codified in Doti/Hx behavior, not implemented as an instruction asking agents to perform commits manually.
- The operator further clarified that agents must not invoke a separate commit command. The transition commit must happen automatically in code at the start of the next step; for example, when the cycle is at `specify`, calling `clarify` commits `specify` first with the default subject `<stage>: <NNN-feature-slug>`.
- The operator required the Doti workflow to support multiple completed spec cycles before one combined release, including the ability after implementation/drift-review to cycle back to a new spec.
- The operator required the explicit Doti commit step to be removed because commits are automatic stage-transition behavior.
- The operator required the release stage to instruct the agent to update README and all repo documentation with release notes so releases do not create documentation debt.
- The operator required the current root `doti/` directory to be removed from speckit-doti and from existing older repos during the Velopack-backed update/install migration, along with other obsolete/orphaned Doti-owned files and directories.
- The operator originally expected a repo-targeted installer, then clarified the product shape: the Velopack installer installs/updates `hx` into an explicit app directory, and installed `hx` then runs against an explicit target repo directory to create/install Doti for missing/empty directories, install Doti normally into non-empty directories without Doti, upgrade existing Doti-enabled repos, fail hard when no repo target is supplied, and avoid shipping source code as the release product.
- The operator required hx and scaffold code to use Microsoft.Extensions.Configuration, with a JSON config file beside `hx.exe`; local release directory output is enabled by default and requires a configured directory or hx fails hard, while disabled local release output does not require a directory.
- The operator clarified that if the hx config file is not present, hx must fail hard, and that Microsoft Configuration must also be included in the scaffold starter project code following the same executable-local style as Doti.

### 2026-06-26

- `/doti-clarify` found no remaining blocking operator questions. The only implementation-level assumption left is the exact executable-adjacent hx config filename; the spec records `hx.config.json` as the working default unless planning discovers a stronger existing convention.
- Clarified stale wording so non-empty targets without Doti are treated as first-time Doti installs, not unsupported targets.
- Clarified that a new spec cycle is offered after `08-Drift-Review`, not directly after `07-Implement`.
- Clarified that workflow numbering must use explicit two-digit ordered labels such as `01-Specify` and sortable skill identifiers such as `01-doti-specify`.
- Clarified that all Doti changes must be made both in this speckit-doti repo and in the scaffold-installed Doti payload used by generated repos.

### 2026-06-25 Installer Correction

- The operator clarified that the released Velopack installer/update experience is for installing or updating the main `hx` executable and bundled Doti payload into an operator-configured and operator-confirmed application directory.
- The operator clarified that repo Doti updates happen after that install by running `hx` from the installed directory against an explicit repo path.
- The operator rejected any normal released-package workflow that still requires `dotnet run`, source compilation, or a local `speckit-doti` checkout to update another repository.
- The previous wording that made the Velopack installer itself target arbitrary repo directories is superseded by the two-step model: Velopack installs/updates `hx`; installed `hx` installs/repairs/migrates Doti assets in target repos.
- The operator selected Option A for Windows install-directory confirmation: support and document Velopack `Setup.exe --installto <DIR>` as the required configurable install path. Interactive MSI/folder-picker installation is out of scope for this feature.
