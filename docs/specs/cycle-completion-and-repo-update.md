# Spec: Completed cycle status and existing-repo update

> WHAT and WHY only. This feature fixes the confusing post-commit doti cycle status and defines a first-class `hx update` experience for bringing an existing repository forward to the latest released speckit-doti assets.

## Goal

Users should be able to trust the doti cycle after a successful sanctioned commit and should be able to update an existing repository with the latest speckit-doti release without cloning this repository or manually copying generated assets.

The immediate workflow bug is that a completed cycle can look stale after `doti cycle commit` succeeds. The stale verdict is caused by the commit moving `HEAD`, which changes the diff-bound identity. That protects the proof from reuse, but the user-facing status is wrong for a just-finished cycle: it should say the cycle completed at a specific commit, not imply the work is now invalid.

The update goal is a product workflow: a released `hx.exe` should update the repository it is run inside by default, or update a repository supplied by path. It should discover the latest release from `heurexai/speckit-doti`, download the matching platform archive into a temporary cache only when necessary, reuse the cached latest archive when already present and verified, and prune older cached versions after a newer one is successfully acquired.

For update to be trustworthy, speckit-doti needs a clear product version identity that can be compared to the GitHub release it came from and to the version recorded in each target repository. The updater also needs to know whether a target repo's installed workflow templates or rendered doti skills were customized after installation. Customization must be detected through canonical hashes, reported precisely, and blocked by default; an operator can still choose a force replacement when they intentionally want to discard local Doti customization.

## Scope

**Included**
- A completed-cycle state for successful `doti cycle commit`, including the commit SHA, feature slug, cycle stage, and proof identity that authorized the commit.
- `doti cycle status` and related cycle reporting that distinguish "completed" from "stale because code changed before commit."
- Proof-reuse protection after completion: completed proofs must not authorize a second commit or a different change set.
- Completed-cycle recovery semantics for crashes or write failures between a successful `git commit` and completion-state persistence.
- Loop prevention for repeated `status`, `check`, or `commit` invocations after a completed or partially completed sanctioned commit.
- Workflow bypass hardening so direct diagnostic commands, hand-edited proof files, forged hook sentinels, stale build outputs, and bare Git commits cannot silently satisfy the sanctioned commit contract.
- A new `hx update` command on the standalone scaffold executable.
- `hx update` defaults to the current working directory as the target repository.
- `hx update --repo <path>` updates the repository at the supplied directory, including Windows absolute paths to existing doti-enabled repositories.
- Latest-release discovery from GitHub repository `heurexai/speckit-doti`.
- Platform-specific release asset selection for the host RID.
- Verified temporary caching of downloaded speckit-doti release archives/extracts.
- Reuse of an already cached and verified latest platform asset instead of re-downloading it.
- Removal of older cached speckit-doti versions only after the newer platform asset has been downloaded and verified.
- Older-running-`hx` handoff: when the process that starts update is older than the target repo or cannot safely update its own installed copy, it downloads/verifies the latest platform release into the temporary cache and delegates the actual repo update to that cached `hx` with `--repo <target>`.
- Existing-repo update of scaffold-owned/doti-owned assets, rendered agent skills, root agent entrypoints, tool manifests, and version stamps without overwriting user-owned application code.
- Legacy pre-versioned speckit-doti repository update that can replace current managed templates/instructions/tool manifests while preserving old live configuration and leaving unknown legacy files untouched.
- A canonical, comparable scaffold version identity exposed by `hx`, recorded in installed repositories, and compared against GitHub releases during update.
- A repo-aware version report that can be run from inside a target repository or against `--repo <path>` and reports scaffold version plus managed Doti modification state.
- Canonical hashes for the originally installed workflow templates and rendered doti skills so local customization can be detected before update.
- Parser-backed semantic content hashes for structured textual managed content so formatting-only presentation changes do not count as customization, while changes to the parsed content model still do.
- Category-specific update blocking when installed workflow templates or doti skills have been modified since their recorded canonical hashes.
- A `--force` update path that intentionally replaces modified/outdated/customized installed Doti assets after reporting exactly what will be overwritten.
- Preservation of target-repo live configuration and runtime baseline files, including Sentrux baselines and repo-local rule/config decisions.
- Creation of a backup Git worktree from the target repository before managed files in the original target checkout are mutated by default.
- An explicit `--noworktree` no-backup direct-replacement option that can switch off worktree creation while still mutating the original target checkout.
- A machine-readable update report showing checked version, selected asset, cache decision, target repo, files changed, and follow-up validation commands.
- A trusted prerequisite manifest and preflight experience for `hx new`, `hx update`, repo-aware version checks, and generated-repo validation so required external tools such as the .NET SDK and Git are detected before work starts.
- No-coder-friendly remediation output for missing prerequisites: hard failure, exact missing/outdated items, and trusted installation instructions that an operator can approve outside the deterministic command.
- Windows-only, operator-approved automatic prerequisite installation through winget when the trusted prerequisite manifest declares a supported winget package for the missing prerequisite and winget itself is available.
- Release classification for this feature as a minor speckit-doti version update when it is eventually released.

**Excluded**
- Running the release workflow, creating release tags, or publishing assets as part of this feature.
- Performing the minor version bump, release tag creation, or release publication during specify/plan work; this spec records release intent only.
- Automatically creating a second Git commit to recover from a lost or partially written completed-cycle record.
- Automatically resetting, amending, rebasing, or deleting commits as part of completed-cycle recovery.
- Treating local hooks as a cryptographic security boundary against a user or agent with arbitrary filesystem and Git access.
- Accepting direct `dotnet test`, hand-written transcripts, or hand-written proof JSON as equivalent to a gate-minted proof.
- Updating arbitrary non-doti application source in a target repository.
- Treating GitHub availability as part of the offline gate. `hx update` is explicitly network-enabled.
- Silently downgrading a repository generated by a newer speckit-doti version.
- Letting an older running `hx` mutate a newer target repo directly when a verified latest updater is required.
- Overwriting the currently running `hx` executable in place during update instead of delegating to a verified temporary updater when executable locking can apply.
- Mutating repositories outside the resolved target root.
- Mutating the original target checkout without first creating the backup worktree unless the operator explicitly passes `hx update --noworktree`.
- Silently overwriting modified workflow templates or doti skills.
- Replacing target-repo live configuration, baselines, or operator-maintained rule state such as Sentrux baselines.
- Deleting, moving, or renaming unknown legacy files left from pre-versioned speckit-doti installs merely because they are no longer part of the current managed asset set.
- Removing older cached versions before the latest replacement has been verified.
- Making local cache presence alone proof that a version is still the latest when GitHub cannot be reached.
- Using an LLM or agent judgment inside the deterministic updater to decide which legacy files should be deleted or which live configuration should be edited.
- Silently installing .NET, Git, package managers, shells, or other system prerequisites without explicit operator approval and a trusted install plan.
- Automatically installing prerequisites on non-Windows platforms; Linux/macOS prerequisite remediation remains instructions-only unless a future platform-specific installer flow is specified.
- Automatically installing prerequisites on Windows through any mechanism other than winget.
- Trusting target-repo, cache-local, or user-editable configuration to supply executable download URLs or to weaken the release-defined prerequisite policy.

## Functional Requirements

### A. Completed cycle state

- `FR-001`: After `doti cycle commit` successfully creates a commit, the system MUST record that the active cycle completed, including the commit SHA and the feature/stage identity that authorized it.
- `FR-002`: `doti cycle status` MUST report a completed cycle as completed at the recorded commit instead of reporting every prerequisite stage as stale only because `HEAD` moved after the sanctioned commit.
- `FR-003`: Completed cycle reporting MUST still preserve the original proof boundary: the stage and gate proofs used for the completed commit MUST NOT authorize another commit, another staged scope, or another change set.
- `FR-004`: `doti cycle check --stage commit` MUST produce a clear completed-cycle or no-active-commit-scope verdict after a successful commit, not a misleading "code changed since stamp" verdict for the just-committed cycle.
- `FR-005`: Starting a new cycle after a completed commit MUST require a new specify stamp and fresh downstream prerequisites for the new change set.
- `FR-006`: Completion state MUST survive a process restart and be visible in JSON output.

### B. Existing-repo update command

- `FR-007`: The standalone scaffold executable MUST expose `hx update` as a first-class command in the human help and `describe --json` capability model.
- `FR-008`: Running `hx update` without a repository argument MUST target the current working directory.
- `FR-009`: Running `hx update --repo <path>` MUST target the repository at the supplied directory.
- `FR-010`: `hx update` MUST validate that the target directory is a Git repository before making changes; a directory carrying doti/scaffold-owned assets without Git support MAY be identified as a recognizable doti-shaped target, but it MUST fail closed with a no-Git-recovery diagnostic unless a future explicit non-Git migration mode is specified.
- `FR-011`: By default, before mutating managed files in the original target checkout, `hx update` MUST create a dedicated backup Git worktree from the target repository's current `HEAD` so the pre-update state remains recoverable.
- `FR-012`: The update worktree MUST be recorded in the update report with its path, branch/ref identity, reversal instructions, and an explicit statement that the worktree is the backup copy, not the mutation target, and that it represents committed `HEAD` only rather than staged, unstaged, or untracked edits in the original checkout.
- `FR-013`: If the backup worktree cannot be created, `hx update` MUST fail before changing files in the original target checkout.
- `FR-014`: `hx update` MUST expose `--noworktree` as the explicit option to disable backup worktree creation and perform no-backup direct replacement in the original target checkout.
- `FR-015`: When `--noworktree` is used, `hx update` MUST state in human and JSON output that no recovery worktree was created before mutating the original target checkout.
- `FR-016`: `hx update` MUST refuse to update a target when any planned write would collide with staged, unstaged, untracked, or ignored scaffold-owned/doti-owned paths unless the operator explicitly requests a separately named dirty-path override; `--force` is not that override.
- `FR-017`: `hx update` MUST update only scaffold-owned/doti-owned assets and generated instruction surfaces; it MUST NOT overwrite user-owned product code, product tests, local planning artifacts outside the managed workflow contract, or unrelated repository files.
- `FR-018`: The update MUST be idempotent: running `hx update` twice against an already-current repository MUST produce no file changes and report that the target is already current.
- `FR-019`: The update MUST render installed agent skills and root agent entrypoints from the updated source assets rather than hand-editing generated files.
- `FR-020`: The update MUST report every changed path and any required follow-up command through the existing `CliResult` JSON envelope.

### C. GitHub latest-release and cache behavior

- `FR-021`: `hx update` MUST query GitHub for the latest non-prerelease speckit-doti release from `heurexai/speckit-doti` by default.
- `FR-022`: `hx update` MUST select the release asset matching the host platform/RID and fail with a clear diagnostic when no supported asset exists.
- `FR-023`: Downloaded release assets MUST be integrity-verified before they can be used to update a repository.
- `FR-024`: If the temporary cache already contains the latest version's platform asset and its integrity verification passes, `hx update` MUST use that cached asset instead of downloading it again.
- `FR-025`: If GitHub reports a newer version than the cached version, `hx update` MUST download and verify the newer platform asset before using it.
- `FR-026`: After a newer platform asset has been downloaded and verified, `hx update` MUST remove older cached speckit-doti platform assets from the temporary cache.
- `FR-027`: If the newer download or verification fails, `hx update` MUST leave the older cache entry intact and MUST NOT update the target repository from an unverified asset.
- `FR-028`: If GitHub cannot be reached, `hx update` MUST fail closed for "latest" mode unless the operator explicitly requests a cache-only mode that does not claim latest-version freshness.

### D. Scaffold version identity

- `FR-029`: speckit-doti MUST have one canonical scaffold product version identity suitable for human display, JSON output, target-repo stamps, and GitHub release comparison.
- `FR-030`: The canonical scaffold version MUST include a normalized SemVer value, release tag, source commit, build metadata, and release asset identity when the executable came from a release asset.
- `FR-031`: `hx --version`, human help, `describe --json`, update dry-run, and update reports MUST all expose the same canonical scaffold version identity.
- `FR-032`: Generated and updated repositories MUST record the scaffold version identity that installed or last updated their managed doti/scaffold assets.
- `FR-033`: Version comparison MUST normalize `v` prefixes, SemVer prerelease labels, and build metadata so local `hx`, target-repo stamps, cached assets, and GitHub releases can be compared deterministically.
- `FR-034`: `hx update` MUST report whether the target repo is behind, equal to, or newer than the latest compatible GitHub release.
- `FR-035`: A target repository stamped with a newer scaffold version than the running updater MUST NOT be mutated directly by that older updater; the older updater MUST either delegate to a verified latest temporary `hx` or fail closed before target mutation.

### E. Canonical managed-asset hashes and customization detection

- `FR-036`: On `new`, `install`, and successful `update`, the system MUST record canonical hashes for every managed workflow-template file installed into the target repository.
- `FR-037`: On `new`, `install`, and successful `update`, the system MUST record canonical hashes for every rendered doti skill and root agent entrypoint installed into the target repository.
- `FR-038`: Before updating a target repository, `hx update` MUST recompute the current hashes of managed workflow-template files and compare them with the recorded canonical hashes.
- `FR-039`: Before updating a target repository, `hx update` MUST recompute the current hashes of rendered doti skills and root agent entrypoints and compare them with the recorded canonical hashes.
- `FR-040`: If workflow-template hashes differ from the recorded canonical hashes, `hx update` MUST warn and fail hard before mutation, with diagnostics that identify the category as workflow-template modification and name the affected paths.
- `FR-041`: If doti skill or root agent entrypoint hashes differ from the recorded canonical hashes, `hx update` MUST warn and fail hard before mutation, with diagnostics that identify the category as skill/generated-instruction modification and name the affected paths.
- `FR-042`: `hx update --force` MUST allow the operator to intentionally replace modified/outdated/customized managed workflow templates, doti skills, and root agent entrypoints after reporting the exact modified paths that will be overwritten.
- `FR-043`: `--force` MUST NOT bypass release asset integrity verification, target-root containment, older-updater handoff requirements, unsupported RID refusal, dirty managed-path refusal, or arbitrary user-code protection.
- `FR-044`: The update report MUST distinguish unmodified managed files, modified workflow-template files, modified skill/generated-instruction files, missing managed files, and files replaced because `--force` was used.

### F. Existing-repo compatibility and proof

- `FR-045`: `hx update` MUST preserve repository-local customization outside managed scaffold/doti assets.
- `FR-046`: `hx update` MUST provide a dry-run mode that reports the latest version, target version, cache action, backup-worktree/no-backup decision, canonical-hash status, and file-change plan without mutating the target repository.
- `FR-047`: `hx update` MUST be able to update an existing doti-enabled repository that was not generated during the current process, including a repository supplied through a Windows absolute path.
- `FR-048`: `hx update` MUST emit stable structured diagnostics for network failure, unsupported RID, missing asset, failed integrity verification, backup worktree creation failure, no-backup selection, dirty managed paths, modified workflow templates, modified doti skills, missing/corrupt/unsupported canonical hash metadata, older-updater handoff failure, too-new target repo with no verified compatible updater, target-not-repository, and recognizable doti-shaped targets that lack Git worktree recovery support.

### G. Repo-aware version and modification report

- `FR-049`: The standalone scaffold executable MUST expose a repo-aware version report command that reports the running `hx` scaffold version and, when a target repo is resolved, the target repo's installed scaffold version.
- `FR-050`: Running the version report from inside a doti-enabled repository MUST default the target repo to the current working directory.
- `FR-051`: Running the version report with `--repo <path>` MUST evaluate the repository at the supplied directory.
- `FR-052`: The version report MUST use the same canonical managed-asset hash set as `hx update` to detect workflow-template and doti skill/generated-instruction modifications.
- `FR-053`: The version report MUST report workflow-template modification separately from doti skill/generated-instruction modification.
- `FR-054`: The version report MUST identify each modified workflow template by repo-relative path and show enough hash metadata to distinguish recorded canonical hash from current content hash.
- `FR-055`: The version report MUST identify each modified doti skill and root agent entrypoint by repo-relative path and show enough hash metadata to distinguish recorded canonical hash from current content hash.
- `FR-056`: The version report MUST report a clean managed-asset state when all recorded workflow-template and doti skill/generated-instruction hashes match.
- `FR-057`: The version report MUST return stable structured diagnostics when the target repo has no version stamp, no canonical managed-asset hash set, missing managed files, unsupported hash schema, or an invalid repo path.
- `FR-058`: The version report MUST NOT mutate the target repository, create worktrees, download release assets, prune caches, or force-replace files.

### H. Live configuration and baseline preservation

- `FR-059`: `hx update` MUST classify target-repo live configuration and baseline files separately from managed scaffold/doti assets.
- `FR-060`: Live configuration and baseline files MUST NOT be replaced, reset, deleted, re-rendered, or hash-normalized by `hx update`.
- `FR-061`: `hx update --force` MUST NOT replace live configuration or baseline files; force applies only to managed Doti/scaffold assets whose replacement is already listed in the update report.
- `FR-062`: The live-preserved set MUST include Sentrux baselines and repo-local runtime gate state by default.
- `FR-063`: If a future scaffold release changes the recommended shape of a live configuration file, `hx update` MUST report it as advisory guidance or a separate operator action, not apply the change automatically.
- `FR-064`: The update report MUST list preserved live configuration/baseline paths when they are relevant to the update, so the operator can see they were intentionally left untouched.
- `FR-065`: The repo-aware version report MUST identify live configuration/baseline files as preserved target-owned state and MUST NOT report them as modified managed templates or modified skills.

### I. Release classification

- `FR-066`: The eventual release containing this feature MUST be classified as a minor speckit-doti version update.
- `FR-067`: Release-readiness output, release notes, and the final release proof MUST identify the release classification as minor before publication.
- `FR-068`: The eventual release path MUST use the sanctioned minor version-bump surface and MUST NOT use a major bump, patch-only bump, manual tag, or hand-edited version stamp for this feature.
- `FR-069`: Once the minor release is published, `hx update` version comparison MUST treat that released minor tag as the comparable latest-version identity for target repositories.

### J. Semantic content-hash canonicalization

- `FR-070`: Any hash whose purpose is textual content-piece identity, managed-asset customization detection, or content drift reporting MUST use a named canonicalization profile instead of raw file bytes or ad hoc whitespace stripping.
- `FR-071`: For parser-backed structured text formats, including YAML and JSON, content hashes MUST be semantic: presentation-only whitespace, comments, style, and end-of-line differences that do not change the parsed content model MUST NOT change the hash, while changes to structure, scalar values, sequence order, mapping entries, or other parsed content MUST change the hash.
- `FR-072`: YAML managed-content hashes MUST use a YAML parser and representation model, preferring the existing repository-pinned YamlDotNet dependency unless `/doti-plan` proves a better maintained or more conformant .NET library is required.
- `FR-073`: YAML semantic hashing MUST fail closed when a YAML document cannot be parsed, contains unsupported representation features for the selected hash profile, has duplicate mapping keys, or cannot be converted deterministically into the canonical content model.
- `FR-074`: JSON managed-content hashes MUST use RFC 8785 JSON Canonicalization Scheme (JCS) semantics or a compatible existing .NET/C# implementation when JSON is the source format or the intermediate canonical form, and MUST fail closed for duplicate object names, JSON-with-comments or trailing commas unless a named JSONC profile is explicitly selected, unsupported encodings or byte-order marks, invalid JSON, and numeric values that cannot be represented deterministically by the selected JCS/I-JSON-compatible profile.
- `FR-075`: If a managed textual format has no accepted semantic canonicalization standard or suitable library, `/doti-plan` MUST either define a named local canonicalization profile with examples and test vectors or keep that format byte-exact with an explicit rationale.
- `FR-076`: Each recorded canonical content hash MUST include the canonicalization profile identifier, profile version, source format, and library/version used so future updates can compare, migrate, or fail closed on unsupported hash schemas.
- `FR-077`: Release-asset integrity hashes, downloaded archive hashes, executable hashes, and other binary/integrity hashes MUST remain byte-exact and MUST NOT use semantic content normalization.

### K. Legacy pre-versioned repository update

- `FR-078`: If a target repository has recognizable pre-versioned speckit-doti assets but lacks a scaffold version stamp or canonical managed-asset hash set, `hx update` MUST classify it as a legacy pre-versioned target instead of failing solely because the stamp or hash baseline is missing.
- `FR-079`: Legacy pre-versioned update MUST still use the same default backup-worktree behavior, target-root containment checks, release-asset integrity verification, and dirty managed-path protections as a normal update.
- `FR-080`: In legacy pre-versioned mode, `hx update` MUST compute the impacted write set from the current release's managed asset manifest: create missing current managed files and replace current managed files whose content differs from the release payload.
- `FR-081`: Legacy pre-versioned mode MUST NOT delete, move, rename, or normalize files that are not in the current release's managed asset manifest, even when those files look like old templates or old generated Doti assets.
- `FR-082`: Legacy pre-versioned mode MUST preserve target-owned live configuration and baseline files exactly, including Sentrux baselines, repo-local rule decisions, and target-owned gate state.
- `FR-083`: Legacy pre-versioned mode MUST report that previous customization detection is unavailable for replaced managed paths because no trusted canonical baseline exists, and MUST list every path it will create or replace before mutation in dry-run output.
- `FR-084`: Legacy pre-versioned mode MUST NOT require `--force` solely because the target lacks a version stamp or canonical hash baseline; replacing current release-managed paths after dry-run-visible reporting is the only exception to default customization blocking, and `--force` MUST NOT broaden deletion or live-configuration powers.
- `FR-085`: After a successful legacy pre-versioned update, the target repository MUST receive the new scaffold version stamp and canonical managed-asset hash set for every current managed asset that was installed or verified.
- `FR-086`: The update report MUST list untouched possible-orphan legacy files separately from preserved live configuration and from files changed by the update.
- `FR-087`: For legacy pre-versioned updates, the update report MUST include an LLM-agent follow-up review instruction that asks for a thorough post-update sweep of untouched possible-orphan files and any final repo-specific adjustments; that sweep is outside the deterministic updater and must happen through the normal doti workflow before committing.

### L. Completed-cycle recovery and loop prevention

- `FR-088`: Before invoking `git commit`, `doti cycle commit` MUST persist enough recoverable commit-completion intent to distinguish "commit not attempted", "commit attempted", "commit succeeded but completion record missing", and "completion recorded".
- `FR-089`: The recoverable intent MUST bind the pending commit to the active feature/stage, pre-commit `HEAD`, staged tree identity, change-set identity, authorizing stage-proof identities, gate-proof identity, commit message digest, and expected completion record shape.
- `FR-090`: If `git commit` fails or no commit is created, the system MUST NOT record completed-cycle state and MUST leave the active cycle recoverable for a later retry after the refusal reason is fixed.
- `FR-091`: If `git commit` succeeds and completion-state persistence succeeds, `doti cycle status`, `doti cycle check --stage commit`, and a repeated `doti cycle commit` MUST report the completed cycle without requiring fresh pre-commit proofs or attempting another commit.
- `FR-092`: If `git commit` succeeds but the process crashes or completion-state persistence fails before the completed-cycle record is written, the next `doti cycle status`, `doti cycle check --stage commit`, `doti cycle stamp`, or `doti cycle commit` MUST detect the recoverable completed commit and repair or report the missing completion state without invoking `git commit` again.
- `FR-093`: Completed-cycle recovery MUST be idempotent: running recovery-capable commands repeatedly after the same successful commit MUST produce the same completed-cycle verdict and commit SHA, with no additional commits and no alternating stale/fresh loop.
- `FR-094`: Completed-cycle recovery MUST fail closed as ambiguous when `HEAD`, the commit parent, staged tree identity, commit trailer, or authorizing proof identity no longer matches the recoverable intent closely enough to prove that the current `HEAD` is the sanctioned commit.
- `FR-095`: Ambiguous recovery MUST return a structured diagnostic with concrete next actions and MUST NOT ask the operator or agent to blindly rerun `doti cycle commit`, restamp prior stages, reset the repo, or rerun `gate run` as the primary recovery path.
- `FR-096`: If new uncommitted changes appear after a sanctioned commit but before recovery, recovery MAY complete the prior cycle only when the created commit is still provably the current completed commit; the new changes MUST be reported separately as a new unstamped change set.
- `FR-097`: If the completed-cycle record exists but the working tree later changes, `status` MUST distinguish "previous cycle completed at <sha>; new unstamped changes exist" from "previous cycle proof is stale", so completed cycles do not become a misleading stale prerequisite wall.
- `FR-098`: If `.doti/cycle-state.json` is missing, truncated, or invalid after a commit-completion failure, the recovery path MUST either reconstruct the completed-cycle record from the recoverable intent and Git evidence or fail closed with an explicit corrupt/missing state diagnostic.
- `FR-099`: Completed-cycle recovery MUST tolerate process restarts and MUST NOT depend on in-memory state, process IDs, wall-clock timing, or terminal session continuity.
- `FR-100`: Concurrent or overlapping `doti cycle commit` attempts MUST fail closed or serialize through an explicit lock/transaction boundary so multiple pending completion intents cannot produce duplicate commits or contradictory recovery verdicts.

### M. Older-updater handoff and executable self-unlock

- `FR-101`: If the running `hx` is older than the target repo's recorded scaffold version or otherwise cannot safely update the target's installed `hx` because the running executable may be locked, the running process MUST enter handoff mode before any target mutation.
- `FR-102`: Handoff mode MUST resolve the latest compatible speckit-doti GitHub release, select the host platform asset, and use the same temporary-cache and integrity-verification rules as normal `hx update`.
- `FR-103`: If a verified latest compatible asset is already present in the temporary cache, handoff mode MUST use that cached extracted `hx` rather than downloading again.
- `FR-104`: The delegated updater MUST be launched from the verified temporary release payload, not from the target repository's installed `hx` path.
- `FR-105`: The delegated updater invocation MUST pass the resolved target repository through `--repo <target>` so it does not depend on the original current working directory.
- `FR-106`: The delegated updater MUST receive the operator's relevant update options, including `--dry-run`, `--force`, `--noworktree`, JSON/plain rendering controls, and help-mode controls, except for internal recursion-prevention metadata.
- `FR-107`: The delegated updater MUST perform the actual target mutation, worktree backup decision, canonical-hash checks, live-configuration preservation, and version-stamp write; the older parent updater MUST NOT write target files directly.
- `FR-108`: The handoff result MUST be reported through the same `CliResult` envelope and include handoff metadata: parent updater version, delegated updater version, target repo, cache entry, and whether the child process performed mutation.
- `FR-109`: If the latest compatible release cannot be reached, downloaded, verified, extracted, or executed, handoff MUST fail closed before target mutation and report remediation that identifies the failed handoff phase.
- `FR-110`: Handoff recursion MUST be bounded: a delegated temporary `hx` MUST detect that it is already the handoff runner and MUST NOT repeatedly relaunch the same version; it may only re-delegate if it verifies a strictly newer compatible release.
- `FR-111`: Temporary-cache pruning MUST NOT delete or replace the release payload that contains the delegated updater while it is running; cleanup of that payload must be deferred until after the delegated process exits.
- `FR-112`: Handoff MUST preserve target-root containment and MUST NOT execute an unverified temporary binary, even when `--force` is used.

### N. Review-driven update and recovery safeguards

- `FR-113`: `doti cycle stamp` MUST run the same completed-cycle recovery evaluator before prerequisite freshness checks; after a completed cycle, stamping an old stage MUST return a completed-cycle or no-active-cycle diagnostic, while stamping a new specify stage MUST start a new cycle/change set.
- `FR-114`: If the selected compatible release is newer than the running `hx`, the parent updater MUST delegate mutation to the verified temporary `hx` from that release before managed-asset reconciliation, unless the release manifest explicitly declares compatibility with the parent updater version.
- `FR-115`: Before executing a cached or newly extracted delegated `hx`, handoff MUST verify the archive, extraction manifest, and delegated executable identity/hash at use time; a stale, missing, or tampered extracted payload MUST be discarded or fail closed before execution.
- `FR-116`: Every path in the managed asset manifest MUST have an ownership category, canonical identity policy, and update conflict policy; doti source assets, tool manifests, and version stamps MUST either be hash-checked before replacement or explicitly classified as generated/replaced metadata with rationale in the update report.
- `FR-117`: A version-stamped target with missing, corrupt, or unsupported canonical hash metadata MUST fail closed before update mutation and MUST NOT be silently treated as legacy pre-versioned unless an explicit repair or migration mode is selected.
- `FR-118`: Any separately named dirty-path override MUST report staged, unstaged, untracked, and ignored planned-write collisions before mutation and MUST be unavailable through `--force`.

### O. Workflow bypass hardening and proof provenance

- `FR-119`: Direct `dotnet test`, direct `Hx.Impact.Cli plan`, direct hygiene scans, and other leaf diagnostic commands MAY be used for investigation, but they MUST NOT write or update a commit-acceptable `GateProof`; only `gate run` may mint a proof accepted by `doti cycle commit`.
- `FR-120`: A persisted `GateProof` MUST include producer provenance: command surface (`gate run`), runner version/source identity, lane/profile, base ref, head ref, change-set identity, started/finished timestamps or run id, and proof schema version.
- `FR-121`: A persisted `GateProof` MUST include a canonical proof digest over its accepted fields, and `doti cycle commit` MUST recompute that digest before relying on the proof.
- `FR-122`: The affected-test proof MUST include per-test execution artifact identity, including the selected test project, command, working directory, allowed environment summary, test assembly/output identity, stdout/stderr digest or transcript digest, exit code, and outcome.
- `FR-123`: `doti cycle commit` MUST recompute and compare the current affected-test planner hash, selected test-scope hash, execution hash, staged-tree identity, change-set identity, proof digest, and execution artifact identities before invoking `git commit`.
- `FR-124`: A proof produced by an unsupported runner version, unsupported proof schema, weaker-than-required lane, unsupported RID, missing test artifact identity, missing provenance, or mismatched staged-tree identity MUST be refused before Git mutation.
- `FR-125`: The commit-completion intent and final commit trailer MUST include the authorizing gate proof digest, change-set identity, staged-tree identity, runner version/source identity, feature, and stage so a post-commit recovery can distinguish a sanctioned commit from a bare Git commit with similar text.
- `FR-126`: The hook sentinel environment variable MUST only let the local insurance hook avoid blocking the `git commit` subprocess launched by `doti cycle commit`; the sentinel alone MUST NOT be treated as proof that a commit is sanctioned.
- `FR-127`: `doti cycle status`, `doti cycle check`, and completed-cycle recovery MUST detect an external or bypass commit when `HEAD` advances without a matching commit-completion intent, proof digest, and Doti trailers; they MUST report a bypass/external-commit verdict rather than a generic stale-prerequisite loop.
- `FR-128`: `doti install-hooks` MUST be idempotent and the local cycle status/check surfaces MUST report whether the expected pre-commit hook is missing, modified, or installed at the expected hash, with remediation to reinstall it.
- `FR-129`: A missing or modified local hook MUST NOT make `doti cycle commit` itself unsafe, but it MUST be visible in human and JSON output so agents cannot assume bare commits are guarded in that clone.
- `FR-130`: A clean-checkout verification path, such as CI or the release gate, MUST rerun the command-backed gate from source and MUST NOT accept a developer-machine `.doti/gate-proof.json` as the sole proof for publication or merge.
- `FR-131`: The CLI capability model and diagnostics MUST document which commands are diagnostic-only and which commands mint accepted proofs, so agents can discover that direct `dotnet test` is advisory and `gate run` plus `doti cycle commit` is the sanctioned path.

### P. Trusted prerequisite manifest and preflight

- `FR-132`: The standalone scaffold executable MUST expose a prerequisite check surface in human help and `describe --json` so an agent or no-coder can discover which external tools are required before running `new`, `update`, or generated-repo validation.
- `FR-133`: `hx new` MUST run prerequisite preflight before template packing, `dotnet new`, tool-store population, Git initialization, or output-directory mutation.
- `FR-134`: `hx update` MUST run prerequisite preflight before Git worktree creation, cache pruning, delegated updater launch, managed-asset reconciliation, or target mutation.
- `FR-135`: Generated repositories MUST carry the release-defined prerequisite policy needed to build, test, gate, update, and recover the repository, and scaffold-owned validation commands MUST evaluate that policy before attempting work that depends on external tools.
- `FR-136`: Prerequisite checks MUST be manifest-driven: the trusted manifest declares required tool identifiers, command probes, minimum/maximum supported versions when applicable, platform/RID applicability, whether a tool is required or advisory for a command, and remediation text.
- `FR-137`: The implementation MAY contain the schema, built-in trust anchors, and bootstrap logic required to find and verify the trusted manifest, but it MUST NOT hard-code the complete per-command prerequisite list in command bodies.
- `FR-138`: At minimum, the release-defined prerequisite policy MUST treat a compatible .NET SDK and Git as hard requirements for creating a new solution and for running generated-repo development gates; update MUST treat Git as a hard requirement for Git-backed target mutation and worktree backup.
- `FR-139`: A missing, outdated, unsupported, or unverifiable hard prerequisite MUST fail the command before side effects and MUST return stable diagnostics that identify the prerequisite, command that needs it, detected value if any, required version/range, and affected operation.
- `FR-140`: Prerequisite output MUST include human-readable and JSON `nextActions` that offer trusted installation guidance, including official vendor/package-manager instructions where available, but MUST NOT execute installers or package-manager commands unless a future explicitly named operator-approved install command is specified.
- `FR-141`: Installation guidance MUST come only from the trusted release-defined prerequisite manifest or from immutable built-in trust anchors; target-repo manifests, cache manifests, environment variables, and user-editable config MUST NOT be able to replace official source locations or inject executable download URLs.
- `FR-142`: The trusted prerequisite manifest MUST be integrity protected as part of the release payload and managed asset set; if it is missing, schema-invalid, hash-mismatched, signed by an untrusted source, or otherwise unverifiable, prerequisite evaluation MUST fail closed before using any install guidance.
- `FR-143`: A repository-local prerequisite extension MAY add project-specific checks or textual instructions, but it MUST NOT weaken release-defined requirements, reclassify hard prerequisites as advisory, override official install sources, or add executable download URLs accepted by `hx`.
- `FR-144`: `hx new` MUST validate required directories before mutation, including output path availability, parent directory existence or creatability, write permission, temporary/cache directory availability, payload-root readability, and shared tool-store accessibility.
- `FR-145`: `hx update` MUST validate required directories before mutation, including target repository containment, `.git`/worktree accessibility, backup-worktree parent availability when worktree backup is enabled, temporary-cache containment, payload extraction directory safety, and write permission for every planned managed path.
- `FR-146`: Directory and prerequisite failures MUST leave the target repository or output directory unchanged except for documented diagnostic/cache probes that are explicitly safe to discard.
- `FR-147`: Prerequisite reports MUST be no-coder-friendly and agent-actionable: they MUST distinguish "missing", "found but unsupported", "found but not on PATH", "permission denied", "manifest cannot be trusted", and "directory not writable", with concrete next actions and links/instructions suitable for an operator to approve.
- `FR-148`: The repo-aware version report MUST include prerequisite health when run against a target repo, but it MUST remain read-only: no installs, downloads, cache pruning, worktree creation, or target mutation.
- `FR-149`: Automatic prerequisite installation MUST be offered when supported only on Windows, only through winget, only for prerequisites with trusted release-defined winget package metadata, and only after explicit operator permission for the exact install plan.
- `FR-150`: The CLI capability model MUST identify which commands require prerequisite preflight, which prerequisites are hard versus advisory, and whether a failure occurs before or after any possible side effects.
- `FR-151`: The prerequisite install surface MUST be separately discoverable from ordinary `new`, `update`, and `version` execution, and the exact command/option shape MUST make operator-approved installation intentional rather than implied by `--force`, `--dry-run`, JSON mode, or an agent retry.
- `FR-152`: Before any winget install is executed, `hx` MUST present and record an install plan containing prerequisite id, reason, trusted package identifier, trusted source identifier, expected version/range, winget command intent, whether elevation or user interaction may be needed, and the command that will be retried or unblocked after install.
- `FR-153`: The operator permission MUST apply only to the presented install plan; if the package id, source, prerequisite set, target command, or manifest identity changes, `hx` MUST require fresh permission before executing winget.
- `FR-154`: `hx` MUST verify winget availability and identity before using it; if winget is missing, unsupported, blocked by policy, or cannot be executed safely, automatic install MUST be unavailable and the command MUST fall back to trusted instructions-only remediation.
- `FR-155`: `hx` MUST use only winget package identifiers, source identifiers, and installer metadata from the verified release-defined prerequisite manifest or immutable trust anchors; repository-local prerequisite extensions MUST NOT add or override winget install actions.
- `FR-156`: After a winget install completes, `hx` MUST rerun the prerequisite probes and MUST treat the install as unsuccessful unless the required prerequisite is detected at a supported version.
- `FR-157`: Failed, cancelled, or partially completed winget installs MUST stop before scaffold/update mutation and MUST report which prerequisite remains unsatisfied, the winget exit status when available, and safe next actions.
- `FR-158`: The JSON report for automatic installs MUST include provenance for the manifest identity, package/source selected, operator permission, winget execution result, and post-install probe result; it MUST NOT persist secrets, private package feeds, or raw environment dumps.
- `FR-159`: Non-Windows hosts MUST reject any automatic-install option or command with a platform-unsupported diagnostic and MUST provide trusted manual remediation instructions without invoking a package manager.
- `FR-160`: If a prerequisite is missing but the trusted manifest has no Windows winget package for it, `hx` MUST NOT attempt a fallback executable download or arbitrary command; it MUST fail with instructions-only remediation.

## Success Criteria

- `SC-001`: Immediately after a successful sanctioned `doti cycle commit`, `doti cycle status --json` reports the cycle as completed with the commit SHA instead of reporting all prior stages stale due only to `HEAD` movement.
- `SC-002`: After a completed cycle, attempting a second commit without a new change set and fresh cycle proofs is refused with a clear no-active-commit-scope or completed-cycle diagnostic.
- `SC-003`: Running `hx update --repo <existing-windows-repo> --dry-run --json` reports the target repository, latest release, selected platform asset, cache action, update worktree plan, and planned managed-file changes without modifying the repo.
- `SC-004`: Running `hx update --json` from inside a doti-enabled repository updates that repository to the latest released speckit-doti managed assets, then a second run reports no changes.
- `SC-005`: When the latest platform asset is already present and verified in the temporary cache, `hx update` performs zero network asset downloads and uses the cached asset.
- `SC-006`: When GitHub reports a newer release than the cached one, `hx update` verifies the new asset before pruning old cached versions, and old cache entries remain if verification fails.
- `SC-007`: A target repository with dirty managed paths is not mutated by default and receives a clear remediation message.
- `SC-008`: A target repository stamped with a newer speckit-doti version is not downgraded or mutated directly by the older running `hx`; update either delegates to a verified latest temporary `hx` or fails closed before target mutation.
- `SC-009`: A failed worktree creation leaves the target repository unchanged and returns a structured diagnostic.
- `SC-010`: A successful default update report includes the created backup worktree path/ref and reversal instructions.
- `SC-011`: `hx update --noworktree` reports that no recovery worktree was created before no-backup direct replacement in the original target checkout.
- `SC-012`: `hx --version`, `describe --json`, and `hx update --dry-run --json` report the same normalized scaffold version identity, and that identity compares correctly against the latest GitHub release tag.
- `SC-013`: A target repo with a modified workflow template fails update before mutation with a diagnostic that identifies workflow-template modification and the changed path.
- `SC-014`: A target repo with a modified rendered doti skill fails update before mutation with a diagnostic that identifies skill/generated-instruction modification and the changed path.
- `SC-015`: Re-running the same update with `--force` replaces the modified managed workflow/skill assets and reports the replaced paths.
- `SC-016`: `describe --json` and rich/plain help list the new `update` command and its repository, dry-run, force, cache, `--noworktree`, version, and JSON controls.
- `SC-017`: Running the repo-aware version report inside a clean doti-enabled repo reports running `hx` version, target repo scaffold version, and clean workflow-template and skill/generated-instruction hash status.
- `SC-018`: Running the version report with `--repo <existing-repo>` reports the same target repo version and managed-asset hash status as running from inside that repo.
- `SC-019`: After modifying one workflow template and one rendered doti skill, the version report lists both exact repo-relative paths in separate workflow-template and skill/generated-instruction sections.
- `SC-020`: The version report does not create a worktree, download a release asset, mutate files, or prune cache entries.
- `SC-021`: A target repo with an existing Sentrux baseline keeps that baseline byte-for-byte unchanged after `hx update`, including when `--force` is used.
- `SC-022`: If a target repo's live Sentrux or gate configuration differs from the latest scaffold recommendation, `hx update` reports advisory guidance but does not replace the file.
- `SC-023`: The update report distinguishes preserved live configuration/baseline paths from managed Doti template and skill paths.
- `SC-024`: Release-readiness output for this feature identifies the release as minor before any publication step.
- `SC-025`: The eventual release uses the sanctioned minor version bump and annotated release tag path, not a major bump, patch-only bump, manual tag, or hand-edited version stamp.
- `SC-026`: Reformatting a managed YAML workflow template without changing its parsed content model does not cause `hx update --dry-run --json` or the repo-aware version report to classify that file as modified.
- `SC-027`: Changing the parsed content model of the same managed YAML file, including a scalar value, mapping entry, sequence order, or indentation that changes structure, causes the canonical content hash to differ and produces the existing category-specific modification diagnostic before update mutation.
- `SC-028`: A successful default `hx update` mutates the original target checkout and leaves the backup worktree at the pre-update `HEAD`, with both paths identified separately in the update report.
- `SC-029`: A JSON managed-content hash produced from semantically identical JSON documents with different whitespace or property ordering matches under the chosen RFC 8785-compatible canonicalization path.
- `SC-030`: A malformed YAML or unsupported semantic-hash profile fails closed with a structured diagnostic instead of silently falling back to raw byte hashing or accepting an unverifiable hash.
- `SC-031`: Updating a pre-versioned speckit-doti repo with no version stamp and no canonical hash baseline replaces only current release-managed files that are missing or differ, leaves target-owned live configuration unchanged, leaves unknown old template files untouched, and records the new version stamp plus canonical hashes afterward.
- `SC-032`: A legacy pre-versioned dry run reports the exact create/replace write set, the missing historical hash baseline, untouched possible-orphan legacy files, preserved live configuration files, and the default backup worktree plan.
- `SC-033`: A successful legacy pre-versioned update report includes a follow-up instruction for an LLM agent to review untouched possible-orphan files and propose or make final repo-specific adjustments through the normal doti workflow.
- `SC-034`: A normal successful `doti cycle commit` records a completed-cycle record, and a repeated `doti cycle commit --message <same-or-different>` reports the existing completed commit without creating a second commit.
- `SC-035`: If the process is terminated after `git commit` succeeds but before the completion record is written, the next `doti cycle status --json` recovers or reports the completed commit SHA and does not report every prerequisite as stale due only to `HEAD` movement.
- `SC-036`: If completion-state persistence fails after `git commit` succeeds while the process can still return output, the result identifies that the Git commit was created and that completion-state recovery is required, rather than reporting a plain failed commit.
- `SC-037`: If `git commit` fails before a commit object is created, no completed-cycle record is written and a later retry can proceed after the original refusal reason is fixed.
- `SC-038`: If `HEAD` is moved, amended, reset, or replaced after a commit-completion crash and before recovery, recovery fails closed with an ambiguous-recovery diagnostic and does not create, amend, reset, or delete any commit.
- `SC-039`: New uncommitted edits made after a recovered completed commit are reported as a new unstamped change set while the prior cycle remains completed at its recorded SHA.
- `SC-040`: Repeated `status`, `check --stage commit`, and `commit` calls after a recovered completion record produce stable completed-cycle output and do not alternate between stale, missing, and completed verdicts.
- `SC-041`: When an older `hx` starts `hx update --repo <target>` against a target stamped newer than the running executable and GitHub has a verified latest compatible release, the parent process downloads or reuses the verified cache entry, launches that temporary `hx` with `--repo <target>`, and does not mutate the target directly.
- `SC-042`: If the temporary latest `hx` cannot be verified or launched during handoff, no target files change and the result reports an older-updater handoff failure with the failed phase.
- `SC-043`: A delegated temporary `hx` can replace or update the target repo's installed `hx` and managed assets without a Windows executable-lock conflict with the older parent process.
- `SC-044`: A delegated update preserves `--dry-run`, `--force`, `--noworktree`, JSON/plain output mode, and help-mode intent in the child invocation, and the final report includes parent/delegated version metadata.
- `SC-045`: Running handoff from an already-current temporary `hx` does not recurse indefinitely and produces one final update/dry-run result.
- `SC-046`: After a recovered completed commit, `doti cycle stamp` against an old stage reports completed or no-active-cycle state instead of creating a stale-proof loop, while a new specify stamp starts a fresh change set.
- `SC-047`: When the selected latest compatible release is newer than the parent `hx`, the parent delegates to the verified temporary release unless a manifest-backed compatibility rule allows the parent to proceed.
- `SC-048`: Tampering with an extracted cached delegated `hx` after archive download causes handoff to discard or fail closed before execution and before target mutation.
- `SC-049`: The managed asset manifest classifies doti source assets, tool manifests, and version stamps with explicit identity and conflict policies, and the update report shows any generated/replaced metadata paths separately from customized templates or skills.
- `SC-050`: A version-stamped repo with missing or corrupt canonical hash metadata fails update before mutation and is not treated as legacy pre-versioned by default.
- `SC-051`: `hx update --force` refuses dirty managed paths; any future dirty-path override uses a distinct option and reports staged, unstaged, untracked, and ignored planned-write collisions before mutation.
- `SC-052`: Running direct `dotnet test` successfully, without a fresh `gate run` proof, still causes `doti cycle commit` to refuse with a diagnostic that only gate-minted affected-test proof can satisfy the commit contract.
- `SC-053`: Hand-editing `.doti/gate-proof.json` after a passing gate run causes `doti cycle commit` to reject the proof because the canonical proof digest, provenance, execution artifact identity, or recomputed affected-test hashes no longer match.
- `SC-054`: Changing the staged scope after a passing `gate run` causes `doti cycle commit` to refuse before Git mutation with a staged-tree or change-set mismatch.
- `SC-055`: Deleting or modifying built test outputs after `gate run` causes `doti cycle commit` to reject the affected-test proof because the recorded execution artifact identities no longer verify.
- `SC-056`: A bare `git commit --no-verify` or a commit made with a forged hook sentinel is reported by `doti cycle status --json` and `doti cycle check --stage commit --json` as an external/bypass commit, not as a completed sanctioned cycle and not as a vague stale-prerequisite loop.
- `SC-057`: If the pre-commit hook is missing or modified, cycle status/check output includes a hook-health warning and `doti install-hooks` restores the expected hook content idempotently.
- `SC-058`: A release or merge validation run from a clean checkout reruns the command-backed gate and does not trust a developer-machine `.doti/gate-proof.json` as publication or merge proof.
- `SC-059`: `describe --json` exposes the proof-minting boundaries: direct diagnostic commands are advisory, `gate run` mints the accepted `GateProof`, and `doti cycle commit` is the only sanctioned local commit path.
- `SC-060`: Running `hx update --repo <absolute-path-outside-current-checkout> --dry-run --json` proves target-root containment by reporting all planned writes as repo-relative paths under the resolved target root and by refusing any path escape before mutation.
- `SC-061`: A managed JSON content hash fails closed with a structured diagnostic for duplicate object names, unsupported JSON-with-comments or trailing commas, unsupported encodings or byte-order marks, invalid JSON, or numeric values that cannot be represented deterministically by the selected JCS/I-JSON-compatible profile.
- `SC-062`: When the original target checkout has staged, unstaged, untracked, or ignored non-managed edits, a default `hx update` report states that the backup worktree preserves committed `HEAD` only and identifies those edits separately from the recoverable backup state.
- `SC-063`: A doti-shaped directory without Git worktree support is not mutated by default; `hx update --json` returns a no-Git-recovery diagnostic that distinguishes it from an ordinary invalid path and from a Git-backed legacy pre-versioned target.
- `SC-064`: On a machine without a compatible .NET SDK, `hx new --json` fails before creating or mutating the output directory and reports the missing SDK, required version/range, trusted installation instructions, and no side effects.
- `SC-065`: On a machine without Git on PATH, `hx update --repo <target> --json` fails before creating a backup worktree or mutating files and reports the missing Git prerequisite with trusted installation instructions.
- `SC-066`: If a trusted prerequisite manifest in the release payload is tampered with, missing, or schema-invalid, `hx new`, `hx update`, and the prerequisite check surface fail closed with a manifest-trust diagnostic instead of using install guidance from that manifest.
- `SC-067`: A repo-local prerequisite extension that attempts to override the .NET or Git official install source, downgrade a hard requirement, or add an executable download URL is refused with a structured diagnostic and does not alter the release-defined prerequisite result.
- `SC-068`: `hx new --json` against an unavailable or non-writable output parent reports the directory failure before template generation and leaves no partial solution behind.
- `SC-069`: `hx update --dry-run --json` includes prerequisite health, directory readiness, and trusted-manifest identity while remaining read-only and performing no installs, downloads for prerequisites, cache pruning, worktree creation, or target mutation.
- `SC-070`: `describe --json` and rich/plain help expose the prerequisite check surface, the command-to-prerequisite mapping, and whether missing prerequisites fail before side effects.
- `SC-071`: On Windows with winget available, when a hard prerequisite is missing and the trusted manifest declares a supported winget package, the automatic-install flow presents the exact install plan, waits for explicit operator permission, executes winget only after approval, reruns the probe, and proceeds only when the prerequisite verifies.
- `SC-072`: On Windows with winget unavailable or blocked, the automatic-install flow refuses before mutation and reports trusted manual installation guidance rather than attempting another installer mechanism.
- `SC-073`: On Linux or macOS, requesting automatic prerequisite installation returns a platform-unsupported diagnostic with manual guidance and does not invoke any package manager.
- `SC-074`: A repo-local prerequisite extension that attempts to add or override winget package/source metadata is refused and the release-defined prerequisite result remains unchanged.
- `SC-075`: If winget installation exits unsuccessfully, is cancelled, or completes without satisfying the post-install probe, `hx new`/`hx update` does not mutate output/target files and reports the failed prerequisite plus winget/probe result.
- `SC-076`: A dry-run or preview of prerequisite installation reports the exact winget package/source plan and manifest identity without executing winget.

## Completed-Cycle Recovery Scenario Analysis

| Scenario | Expected behavior |
| --- | --- |
| `commit` is invoked with no cycle state | Refuse before Git mutation; no recovery transaction is created. |
| `commit` is invoked with a blank message | Refuse before Git mutation; no recovery transaction is created. |
| A prerequisite is missing, stale, or invalid before commit | Refuse before Git mutation and keep the active cycle available for correction. |
| Gate proof is missing, failing, stale, or affected-test hashes do not recompute | Refuse before Git mutation; no completed-cycle record is written. |
| Direct `dotnet test` passes but no fresh `gate run` proof exists | Refuse before Git mutation; report that diagnostic test output cannot satisfy commit proof. |
| `.doti/gate-proof.json` is hand-edited after a passing gate | Refuse before Git mutation when the proof digest, provenance, execution artifact identity, or recomputed hashes do not match. |
| Staged files change after `gate run` but before `doti cycle commit` | Refuse before Git mutation because staged-tree and/or change-set identity no longer matches the proof. |
| Test assemblies or output files recorded by the affected-test proof are deleted or modified after `gate run` | Refuse before Git mutation because execution artifact identities no longer verify. |
| Nothing is staged or tracked unstaged changes are present | Refuse before Git mutation; no completed-cycle record is written. |
| Recovery intent cannot be persisted before `git commit` | Refuse before Git mutation; do not rely on an unrecoverable commit window. |
| Recovery intent is persisted and `git commit` fails without creating a commit | Mark or report the attempt as not completed, keep the active cycle retryable, and do not record completion. |
| Recovery intent is persisted, `git commit` succeeds, and completion state writes successfully | Report completed at the new commit SHA; later status/check/commit calls are idempotent. |
| `git commit` succeeds but completion-state write throws before output is returned | Return a partial-success diagnostic if possible: commit created, completion record missing, recovery required. |
| Process crashes or is killed after `git commit` succeeds but before completion-state write | On next status/check/commit, recover from intent plus Git evidence; do not create a second commit. |
| Process crashes after recovery intent is written but before invoking `git commit` | Detect no matching commit was created; keep the active cycle retryable and clear/report the stale intent. |
| `.doti/cycle-state.json` is truncated or unreadable after the commit succeeded | Recover from the commit-completion intent when provable; otherwise fail closed with corrupt-state recovery steps. |
| `.doti/gate-proof.json` is deleted after a successful commit but before recovery | Recovery uses the identity captured in the intent/completed commit evidence; it does not require rerunning the pre-commit gate for the already-created commit. |
| User reruns `doti cycle commit` after a successful commit but before recovery has been recorded | Treat it as a recovery/idempotence path, not a request for another Git commit. |
| User runs `doti cycle status` after the successful commit but before recovery has been recorded | Report completed/recoverable completion rather than a wall of stale prerequisite proofs. |
| User creates unstaged edits after the commit succeeds but before recovery | Recover the completed commit if Git evidence still matches, and report the new edits separately as not part of the completed cycle. |
| User stages new edits after the commit succeeds but before recovery | Recover only if the committed SHA still matches the intent; report staged edits as a new unstamped scope. |
| User amends, rebases, resets, or checks out a different `HEAD` before recovery | Fail closed as ambiguous; do not auto-complete or auto-rewrite Git history. |
| A bare `git commit` bypass somehow creates a commit without the sanctioned intent/trailer | Do not mark a completed Doti cycle; report bypass/ambiguous state through status/check. |
| A commit is created with a forged hook sentinel but no matching commit-completion intent and gate proof digest | Do not mark it sanctioned; report external/bypass commit and require a clean recovery path rather than trusting the sentinel. |
| Multiple pending commit intents are found | Fail closed with a concurrency/transaction diagnostic; do not choose one heuristically. |
| Two agents invoke `doti cycle commit` concurrently | Serialize through the transaction boundary or allow only one to proceed; the loser observes completed/recoverable state or a lock diagnostic. |
| Completed-cycle record exists and matches the recorded commit SHA | Status reports completed; check/commit do not revalidate stale pre-commit proofs as if the cycle were still active. |
| Completed-cycle record exists but the commit SHA is no longer reachable | Report completed-record inconsistency with recovery steps; do not silently start a new cycle. |
| A new specify stamp is created after a completed cycle | Treat this as a new cycle/change set; old completed proof does not authorize another commit. |
| `doti cycle stamp` is run for an old non-initial stage after a completed cycle | Report completed/no-active-cycle state before prerequisite freshness checks; do not restamp old proofs against the post-commit diff. |
| `doti cycle stamp --stage specify` is run after a completed cycle with a new change set | Treat it as the start of a fresh cycle and bind subsequent prerequisites to the new diff. |
| Repeated recovery-capable commands are run after any recoverable state | Output remains stable and converges to completed, retryable-active, or ambiguous; it must not oscillate. |

## Key Entities

- **Completed cycle record** - persisted evidence that a cycle was successfully committed, including feature, final stage, authorizing proof identity, and commit SHA.
- **Commit-completion intent** - a recoverable pre-commit transaction record that binds the active cycle, staged tree, authorizing proofs, gate proof, message digest, and pre-commit `HEAD` to the Git commit attempt.
- **Completed-cycle recovery verdict** - the status/check/commit result that classifies a post-commit interruption as completed, retryable-active, or ambiguous without creating a second commit.
- **Gate proof provenance** - the gate-minted identity metadata that proves which command, runner version, lane, source identity, change set, and run id produced a commit-acceptable gate proof.
- **Gate proof digest** - the canonical digest over accepted gate proof fields that `doti cycle commit` recomputes before using the proof.
- **Staged-tree identity** - the canonical identity of the exact Git index tree that `doti cycle commit` is about to commit, separate from diagnostic working-tree output.
- **Execution artifact identity** - the recorded hash identity of test assemblies, output files, and execution transcript digests used to prove that the affected-test proof corresponds to the gate-run test execution.
- **Hook health verdict** - the local status of the untracked insurance pre-commit hook: missing, modified, or installed at the expected hash.
- **External/bypass commit verdict** - a recovery classification for commits that advanced `HEAD` without matching sanctioned commit-completion intent, proof digest, and Doti trailers.
- **Scaffold version identity** - the canonical comparable speckit-doti product version: SemVer, release tag, source commit, build metadata, and release asset identity.
- **Version report** - a read-only human/JSON report that combines running `hx` version, target repo scaffold version, GitHub-comparable status, and exact managed-asset modification state.
- **Target repository** - the Git repository or doti-enabled directory selected by `hx update`, defaulting to the current working directory or supplied through `--repo`; this original checkout is the update mutation target.
- **Update worktree** - a dedicated backup Git worktree created from the target repository before mutation, used as the recoverability anchor for the update and not used as the mutation target.
- **Managed asset set** - scaffold-owned/doti-owned paths that `hx update` is allowed to reconcile, including doti source assets, rendered agent skills, root agent entrypoints, manifests, and version stamps.
- **Managed asset manifest** - the release-provided inventory of managed paths, ownership categories, canonical identity policies, update conflict policies, and generated/replaced metadata classifications.
- **Canonical content hash** - a deterministic textual content-piece hash computed through a named parser-backed semantic profile when available, used for managed textual content identity and drift reporting.
- **Canonical managed-asset hash set** - recorded canonical content hashes of the workflow templates, doti source assets, rendered skills, and root entrypoints as they were originally installed or last force-updated.
- **Dirty-path override** - a future explicitly named unsafe option, separate from `--force`, that would be required before updating through uncommitted scaffold-owned/doti-owned path collisions.
- **Live configuration/baseline set** - target-repo-owned configuration, baseline, and runtime gate state that `hx update` may inspect or report but must preserve exactly.
- **Legacy pre-versioned target** - a repository with recognizable older speckit-doti assets but no trusted scaffold version stamp or canonical managed-asset hash baseline.
- **Possible-orphan legacy file** - an old scaffold/doti-looking path that is not in the current release's managed asset manifest and therefore is reported for review but left untouched by `hx update`.
- **LLM-agent follow-up sweep** - an operator-visible post-update review instruction for an agent to inspect untouched possible-orphan files and make any final adjustments through the normal doti workflow.
- **Release descriptor** - the GitHub latest-release data used to choose version, asset name, asset URL, and integrity metadata.
- **Update cache entry** - a verified temporary copy of a platform-specific speckit-doti release asset and its extracted payload.
- **Older-updater handoff** - the fail-closed delegation path where an older parent `hx` downloads or reuses a verified latest temporary release and asks that temporary `hx` to update the target repo.
- **Delegated updater** - the verified temporary `hx` process that receives `--repo <target>` and performs the actual update mutation when the parent `hx` is too old or locked.
- **Update report** - the machine-readable result that records target repo, current version, latest version, cache decision, changed files, skipped files, and follow-up checks.
- **Prerequisite manifest** - the trusted release-defined inventory of external system requirements, probes, version policies, command applicability, and operator-facing install guidance.
- **Prerequisite check result** - the machine-readable status for each prerequisite: found/missing/unsupported/untrusted/advisory, detected version/path, required policy, command impact, and safe next actions.
- **Trusted installation instruction** - operator-facing remediation text or official source reference that originates from the verified release-defined prerequisite manifest or immutable trust anchors, never from target-repo mutable configuration.
- **Repository-local prerequisite extension** - optional target-owned additive prerequisite metadata for project-specific checks; it can add checks but cannot weaken release-defined requirements or provide executable download URLs accepted by `hx`.
- **Windows prerequisite install plan** - an operator-approved plan for installing one or more missing prerequisites through winget on Windows, derived only from trusted release-defined prerequisite metadata.
- **Winget package mapping** - the trusted package/source metadata in the prerequisite manifest that allows `hx` to install a specific prerequisite through winget; target repositories cannot provide or override this mapping.

## Deterministic Surfaces

- Existing cycle surfaces: `doti cycle commit`, `doti cycle status`, `doti cycle check`, `.doti/cycle-state.json`, `.doti/gate-proof.json`, and the `CliResult` envelope.
- Existing proof-hardening surfaces: `GateProofValidator`, `AffectedTestProof`, `AffectedTestProofHasher`, `GateProofStore`, `CommitScopeInspector`, `PrecommitGuard`, and `doti install-hooks`.
- Implemented in the current working tree, pending final command proof: completed-cycle record fields, commit-completion intent, completed-cycle recovery verdicts, completed-cycle JSON status shape, ambiguous-recovery diagnostics, idempotent post-commit status/check/commit behavior, staged-tree identity, and external/bypass commit refusal through the sanctioned commit path.
- Still advisory unless separately implemented and proven: broader gate proof provenance, gate proof digest identity, execution artifact identity, hook-health reporting beyond the existing install/guard path, external commit classification beyond local refusal, and clean-checkout merge/release proof.
- Existing scaffold surfaces: `Hx.Scaffold.Cli new`, `Hx.Scaffold.Cli describe --json`, shared rich/plain help renderer, and release archive naming in `packaging/PUBLISHING.md`.
- Implemented in the current working tree, pending final command proof: canonical scaffold version identity, target-repo version stamp, repo-aware version report, `Hx.Scaffold.Cli update` / standalone `hx update`, update dry-run, update cache, GitHub latest-release client, release asset checksum verification, managed-asset manifest, managed-asset reconciliation report, older-updater handoff/delegated `hx --repo <target>`, delegated executable at-use verification, and target repo compatibility checks.
- Implemented in the current working tree, pending final command proof: canonical managed-asset hash recording, parser-backed semantic content-hash canonicalization for YAML/JSON, workflow-template customization detection, doti skill/generated-instruction customization detection, live configuration/baseline classification, dirty planned-write boundary, `--force` replacement, backup worktree creation, `--noworktree` no-backup replacement, cache/worktree containment policy, and reversal data in the update report.
- Implemented in the current working tree, pending final command proof: legacy pre-versioned target detection, impacted managed-asset write-set planning, possible-orphan legacy file reporting, and LLM-agent follow-up sweep instructions.
- Existing parser/canonicalization surfaces that may be reused by the plan: YamlDotNet is already pinned in `Directory.Packages.props` and referenced by `Hx.Cycle.Core`; `System.Text.Json` is already used broadly for deterministic JSON contracts; RFC 8785-compatible JSON canonicalization should use an existing library or verified implementation when practical.
- Existing versioning surfaces: `version calculate` and `version bump --major|--minor`; this feature is specified as an eventual minor release, with the actual bump/tag deferred to the release workflow.
- Existing install/render surfaces: `doti install`, `doti render-skills`, rendered `.agents`/`.claude` skills, `.doti/agent-context.md`, `AGENTS.md`, `CLAUDE.md`, and doti source assets.
- Existing shared-store surfaces that may be reused by the plan: `ToolStore`, `ToolStoreIndex`, `StorePopulator`, release manifests, and platform/RID classification.
- Implemented in the current working tree, pending final command proof: trusted prerequisite manifest in the release payload and generated repos, `hx prereq check`, automatic preflight inside `hx new` and `hx update`, prerequisite health in repo-aware version reports, `CliResult` diagnostics/next actions for missing or untrusted prerequisites, and generated `.doti/prerequisites.json` carriage.
- Implemented in the current working tree, pending final command proof: explicit operator-approved `hx prereq install` for trusted manifest-backed Windows winget package mappings; unsupported on non-Windows and unavailable when winget or trusted package metadata is absent.

## Architecture Impact

- Completed-cycle semantics belong in the cycle core, not the CLI command body.
- Commit-completion recovery belongs in a shared cycle-core evaluator used by `status`, `check`, `stamp`, and `commit`, so each command reaches the same completed/retryable/ambiguous verdict.
- Commit-completion state writes must be atomic and recoverable enough that a process crash cannot produce an infinite loop of "commit succeeded, proofs stale, please commit again."
- Recovery must prefer convergence over convenience: complete when Git evidence proves the sanctioned commit, leave the active cycle retryable when no commit was created, and fail closed when evidence is ambiguous.
- Proof provenance, staged-tree identity, execution artifact identity, hook health, and external/bypass commit detection belong in the cycle/gate core, not in generated skills or narrative instructions.
- Direct diagnostic commands must remain useful for RCA, but only proof-minting commands can update the durable proof ledger accepted by commit.
- The local insurance hook is a usability and accident-prevention layer; deliberate bypass requires tamper-evident local diagnostics plus clean-checkout gate validation, not trust in hook presence.
- Update orchestration belongs in scaffold core or a shared update core; the CLI remains a thin adapter that parses options, delegates, and renders the report.
- GitHub/network access for update is explicit and isolated from offline gate paths.
- Version identity construction and comparison must be centralized so `hx --version`, `describe`, release lookup, repo stamps, and update reports cannot disagree.
- The repo-aware version report must reuse the same version and canonical-hash classifier as update, but through a read-only path with no cache, worktree, or file mutation effects.
- Older-updater handoff belongs before managed-asset reconciliation and before backup-worktree creation; the parent process may resolve/download/verify and launch the delegated updater, but only the delegated updater may mutate the target.
- The delegated updater contract must be explicit and bounded so parent/child output, recursion prevention, option forwarding, and process-exit propagation cannot diverge from normal `CliResult` behavior.
- Cached delegated updater execution must verify both release integrity and extracted executable identity at use time; a verified archive alone is not enough if an extracted cache entry can later be tampered with.
- Worktree creation and reversal logic must use Git-backed operations and must run before file mutation unless the operator explicitly disables it; the original target checkout remains the mutation target and the worktree remains the backup copy.
- Managed-asset reconciliation must reuse existing install/render ownership boundaries so generated skill files remain rendered artifacts, not direct edit targets.
- Canonical hash verification must classify managed file drift by category before mutation so template customization and skill/generated-instruction customization have different diagnostics.
- The managed asset manifest must be the single source for update ownership, identity policy, and conflict policy so doti source, tool manifests, and version stamps are not implicit write paths.
- Legacy pre-versioned reconciliation must be manifest-driven and conservative: only current managed asset paths are created/replaced, unknown old files are reported but left in place, and the new version/hash baseline is established only after a successful update.
- Semantic content-hash canonicalization must be centralized so parse, normalization, canonical serialization, and hash-profile metadata cannot diverge between install, update, version reporting, and drift checks.
- Live configuration and baseline preservation must be encoded as an allow/deny boundary, not as a best-effort convention.
- Prerequisite policy loading belongs in scaffold core or a shared prerequisite core; CLI commands stay thin and ask the core for preflight results before side effects.
- The release-defined prerequisite manifest is a trust boundary: target repositories may add project-specific checks, but cannot override source locations, weaken hard requirements, or make an agent run arbitrary installers.
- Windows winget installation belongs behind the same prerequisite core and must be modeled as a permissioned remediation step with post-install probe verification, not as a side effect hidden inside normal command execution.
- The plan must decide the exact persisted version-stamp format and completed-cycle JSON shape, and must keep `describe --json` stable except for the additive `update` command.

## Sentrux And Hygiene Impact

- Temporary update cache entries and extracted release payloads MUST live outside the repository and MUST NOT be scanned or committed.
- The temporary payload currently hosting a delegated updater MUST NOT be pruned while the delegated process is running.
- The update implementation must not add large binaries to the repo; downloaded assets are cache/runtime artifacts.
- Public hygiene risk includes GitHub URLs, local cache paths, and target repo paths in logs. JSON output may report paths, but docs and committed fixtures must not contain private local paths except as explicit examples.
- Updating managed generated assets must continue to pass skill drift checks.
- Canonical content hashes must not include secrets or machine-local absolute paths; they are path-relative hashes of parser-backed semantic managed content when a semantic profile exists.
- Presentation-only formatting changes to parser-backed structured managed content are not hygiene findings and must not force an update conflict by themselves; malformed or unsupported content fails closed.
- Sentrux baselines and other target-owned gate state are live configuration, not managed scaffold payload, and must remain byte-for-byte untouched by update.
- Legacy pre-versioned updates may leave old scaffold/doti-looking files in place; this is intentional safety behavior and must be reported rather than hidden.
- Cache pruning must be constrained to the speckit-doti update cache root, never arbitrary temp directories.
- Worktree cleanup must be explicit and constrained to the worktree path recorded by the update report.
- Commit-completion intent and recovery records are local workflow state under `.doti/`; they must not leak secrets, machine-local absolute paths beyond repo-relative diagnostics, or stale gate payloads into committed fixtures.
- Gate proof provenance, execution transcript digests, and hook-health diagnostics must not persist raw secrets, full environment dumps, or machine-local absolute paths beyond what is needed for local diagnostics.
- Clean-checkout release or merge validation must regenerate proof from source rather than importing developer-local proof state, because `.doti/gate-proof.json` and hook files are local workflow artifacts.
- Prerequisite manifests and committed fixtures must not contain machine-local absolute paths, private package feeds, personal access tokens, or mutable untrusted download URLs.
- Prerequisite failures should be concise in human output but complete in JSON so no-coder operators can approve remediation while agents can stop safely.
- Winget install reporting must persist package/source/probe provenance and exit status only; it must not persist full environment dumps, machine-specific package cache paths, or private winget source credentials.

## Assumptions

- "Latest" means the latest non-prerelease GitHub Release unless the operator later asks for prerelease support.
- The canonical product version should be SemVer-compatible with GitHub release tags so normal update comparisons do not depend on assembly build metadata alone.
- The quick scalar `hx --version` can remain a terse executable-version output; the repo-aware version report is the richer surface that accepts `--repo <path>` and reports managed Doti modification state.
- The default target repo is the current working directory because the user asked for "the repo the hx is being run" and existing `new` already runs from arbitrary directories.
- The temporary cache is a speckit-doti-owned subdirectory under the OS temporary directory, with any override deferred to `/doti-plan`.
- The running executable path may be locked by the operating system, especially on Windows, so self-update and newer-target update are modeled as delegation from the old parent process to a verified temporary `hx`.
- A verified latest compatible temporary `hx` is trusted to update a target repo when its release integrity checks pass and its version is not older than the target repo's recorded scaffold version.
- When the selected latest compatible release is newer than the running parent `hx`, delegation is the default because the newer release may introduce manifest, hash-profile, or reconciliation semantics the parent should not interpret unless compatibility is explicitly declared.
- The update worktree location and naming convention are deferred to `/doti-plan`, but the switch name is fixed: create a backup worktree by default before mutating the original target checkout, allow `--noworktree` for no-backup direct replacement in that same checkout, and record enough data to reverse when a worktree exists.
- A sanctioned commit can be identified after interruption by a combination of pre-commit `HEAD`, commit parent/tree evidence, the `Doti-Cycle` trailer, staged tree identity, message digest, and stored authorizing proof identities; `/doti-plan` must choose the exact durable schema.
- Once Git has created the sanctioned commit, completed-cycle recovery should not require rerunning gate proof or restamping old stages merely to acknowledge that already-created commit.
- Direct diagnostic command output is intentionally not enough to prove a commit. The accepted local proof is a gate-minted, change-set-bound proof checked again by `doti cycle commit`; the accepted release/merge proof is a clean-checkout gate run.
- A local agent with arbitrary filesystem and Git access can bypass local hooks; the feature's local responsibility is to make bypass non-silent and non-sanctioned, while clean-checkout validation is the trust boundary for merge or release.
- Structured textual content-piece hashes use parser-backed semantic canonicalization where an accepted standard/library exists; archive, executable, and release-integrity hashes remain byte-exact because they protect downloads and binaries, not textual customization state.
- `--force` means "replace modified managed Doti assets after reporting them"; it does not mean "ignore integrity, containment, version, or user-code protections."
- `--force` also does not mean "replace live target configuration"; baselines and repo-local gate state remain target-owned.
- Integrity verification requires a release-provided checksum or manifest; if the current release package does not publish one, the release pipeline must add it before `hx update` can be considered command-backed.
- `hx update` is allowed to contact GitHub and is not part of the offline gate.
- Existing repos may have local doti customizations; the update report should surface conflicts instead of overwriting ambiguous user edits.
- Legacy pre-versioned repositories may not have enough metadata to prove whether old Doti files were customized. For those targets, replacing only current release-managed paths and leaving unknown old files untouched is safer than deleting or attempting a full cleanup.
- A versioned repository with broken hash metadata is different from a legacy pre-versioned repository; it should fail closed by default because the installer claimed a managed baseline exists but it cannot be trusted.
- LLM-agent follow-up sweeps are expected for legacy pre-versioned migrations that may leave orphaned old files, but those sweeps are advisory workflow work and not part of deterministic `hx update` mutation.
- The first release-defined hard prerequisites are expected to include a compatible .NET SDK and Git; exact minimum versions, per-platform package-manager wording, and any optional shell requirements are plan-level decisions captured in the prerequisite manifest.
- Missing prerequisites are a preflight failure, not an implicit install request. Windows offers an explicit operator-approved winget install flow when trusted package metadata exists; `--force`, `--dry-run`, JSON mode, and ordinary agent retries do not imply install permission.
- Official install guidance can name vendor/package-manager sources, but executable URLs must be controlled by the verified release-defined manifest or immutable trust anchors and cannot be supplied by target repos or update caches.
- Exact winget package identifiers and minimum supported versions are plan-level decisions; they must be verified against the trusted release-defined manifest before implementation and should not be guessed in the spec.
- The operator has classified the eventual release containing this feature as a minor version update; no release, tag, or version bump is performed during specify.

## Acceptance

- **Command-backed today:** source inspection; existing `doti cycle commit` / `status` / `check`; existing affected-test proof recomputation; existing commit scope inspection; existing pre-commit hook installer/guard; existing `Hx.Scaffold.Cli --version`; existing `Hx.Scaffold.Cli describe --json`; existing release archive naming docs; existing doti install/render surfaces; existing shared-store primitives.
- **Implemented in current working tree, pending final full-gate proof:** completed-cycle persisted status, commit-completion intent, completed-cycle recovery verdicts for `status`/`check`/`stamp`/`commit`, explicit post-commit completion-write failure reporting, staged-tree identity, gate-proof digest and runner identity in commit intent/trailers, canonical scaffold version identity with release-asset fields, target-repo version stamp, repo-aware version report, target-to-latest update relation, managed asset manifest with source-format/canonicalizer/identity-policy/conflict-policy metadata, canonical managed-asset hash set, parser-backed YAML/JSON canonicalization, safer normalized text hashing for formats without a parser-backed semantic profile, template/skill customization detection, generated/replaced metadata classification, missing/corrupt hash-metadata refusal, live configuration/baseline preservation, dirty planned-write boundary, `--force` replacement semantics, `hx update`, older-updater handoff/delegated temporary `hx`, delegated option forwarding, delegated executable at-use verification, GitHub latest-release resolution, update cache reuse/prune behavior, backup worktree creation/reversal reporting, `--noworktree` no-backup replacement, existing-repo managed-asset reconciliation, legacy pre-versioned migration mode, possible-orphan legacy reporting, LLM-agent follow-up sweep instructions, update dry-run, update proof/report tests, and release checksum metadata.
- **Still advisory unless separately implemented and proven:** full appended error-code registry coverage for every update/version/cycle diagnostic, broader persisted `GateProof` producer provenance and proof digest beyond the current commit-intent/trailer binding, per-test execution artifact identity, hook-health reporting beyond the existing hook installer/guard path, external/bypass commit classification beyond local refusal, clean-checkout merge/release proof, local review artifact packaging, broader repo-local prerequisite extension handling beyond refusing executable install metadata, and the final minor-release proof.

## Research Basis

- YAML 1.2.2 separates representation content from presentation details, with complete representations enabling equality testing. The hash design should therefore target the YAML representation model, not raw indentation text.
- RFC 8785 JSON Canonicalization Scheme is the accepted JSON canonicalization reference for deterministic JSON bytes before hashing/signing and lists a verified .NET/C# implementation.
- YamlDotNet is already available in this repository and provides YAML parsing, emitting, and object-model support; semantic YAML hashing should prefer that existing parser dependency unless planning proves it inadequate.
- Plain Markdown/text has no accepted repository-pinned semantic canonicalization standard in this design. The current local `normalized-text/v2` profile therefore normalizes whitespace runs and end-of-line presentation while preserving token boundaries; it is a documented local profile, not a YAML/JSON-style semantic parser.

## Clarifications

### 2026-06-23
- No blocking clarification markers were left. The request specified the key product behavior: fix the post-commit workflow confusion, add `hx update`, default to the current repo, accept a target directory, discover the latest `heurexai/speckit-doti` release, cache the right platform version in temp, reuse the cached latest version, prune older cached versions only after a newer version is downloaded, and create a reversible backup Git worktree from the target repo before mutating managed files in the original target checkout.
- The spec was refined to require a canonical comparable scaffold version identity, target-repo version stamps, canonical hashes for installed workflow templates and doti skills, hard-fail customization detection with category-specific diagnostics, `--force` replacement of customized managed Doti assets, and `hx update --noworktree` to disable the default backup worktree for no-backup replacement in the original target checkout.
- The version behavior was refined so a repo-aware version report can run from inside a target repo or with `--repo <path>`, report the target repo's installed scaffold version, and identify exact modified workflow-template paths separately from exact modified doti skill/generated-instruction paths.
- The update boundary was refined so live target configuration and baselines, including Sentrux baselines, are preserved and never replaced by `hx update` or `hx update --force`.
- The operator classified the eventual release containing this feature as a minor version update; no release tag, publication step, or version bump is performed during specify.
- Canonical content hashes were refined from whitespace stripping to parser-backed semantic hashing: YAML and JSON should ignore presentation-only changes through accepted parser/canonicalization paths, but structural or scalar-content changes still alter the hash; release/download/binary integrity hashes remain byte-exact.
- The default update flow was clarified: `hx update` mutates the original target checkout, while the default worktree is a backup copy created first; `--noworktree` skips that backup but does not change the mutation target.
- Legacy pre-versioned speckit-doti repositories were refined to use a conservative migration mode: replace/create only current release-managed impacted files, preserve live configuration and baselines, leave unknown old template/generated files untouched even if they become possible orphans, then instruct an LLM agent to perform a thorough follow-up sweep through the normal doti workflow.
- Completed-cycle recovery was expanded with a scenario analysis for the crash window between successful `git commit` and completion-state persistence; recovery must be idempotent, must not create a second commit, and must converge to completed, retryable-active, or ambiguous without stale-proof loops.
- The older-running-`hx` update wrinkle was refined into a handoff flow: when the process that starts update is older than the target repo or would lock the installed executable, it must resolve/download/verify the latest platform release into the temporary cache and rerun that temporary `hx` with `--repo <target>` so the delegated process performs the update.
- A subagent edge-case review tightened the spec around `doti cycle stamp` recovery, selected-latest-newer-than-parent delegation, at-use verification of cached delegated executables, full managed-asset manifest conflict policy, `--force` versus dirty managed paths, the explicit legacy pre-versioned exception to customization blocking, and fail-closed behavior for versioned repos with broken canonical hash metadata.
- The remaining medium workflow-bypass risks were turned into a working solution: direct diagnostic commands cannot mint accepted proof, gate proofs carry provenance and canonical digests, affected-test proof includes execution artifact identity, `doti cycle commit` binds proof to the staged tree and final trailers, forged hook sentinels do not prove sanction, missing hooks are reported, external/bypass commits are classified explicitly, and clean-checkout gate validation is the merge/release trust boundary.
- A follow-up Codex subagent review found no blocker or high-severity gaps, then tightened medium/low edges around dirty planned-write collisions, JSON semantic-hash fail-closed cases, the committed-`HEAD` limit of backup worktrees, external absolute-path dry-run reporting, and no-Git doti-shaped target diagnostics.
- A `/doti-clarify` pass after those updates found no remaining blocking operator questions. Remaining open choices, such as temporary-cache location details, update-worktree naming, persisted completed-cycle JSON shape, and exact hash-profile schema, are plan-level design decisions already marked for `/doti-plan` rather than scope clarifications.
- A second Codex subagent review found implementation/status mismatches around older-updater handoff forwarding, delegated executable verification, update report thickness, completion-write failure reporting, hash metadata, version release-asset identity, and plan/task status language. The current working tree and this spec were updated so delegated update forwards `--dry-run`/force/worktree/json intent, verifies the delegated executable against the verified archive at use time, reports structured worktree/delegation/file-plan/diagnostic metadata, returns an explicit recovery-needed result when completion persistence fails after Git commit, records hash source-format/canonicalizer/identity/conflict policy metadata, records release asset identity in version stamps, and distinguishes implemented current-tree behavior from still-advisory proof-provenance/error-code/release work.
- The prerequisite wrinkle was added: `hx` must use a trusted manifest-backed preflight for `new`, `update`, repo-aware version reporting, and generated-repo validation; missing .NET/Git or invalid directories fail hard before mutation; remediation is no-coder-friendly but does not silently install tools; and target-repo/cache-local configuration cannot inject executable download locations or weaken release-defined prerequisite policy.
- The Windows automation refinement was added: missing prerequisites may be installed automatically only on Windows, only through winget, only from trusted release-defined package/source metadata, and only after explicit operator permission for the exact install plan; non-Windows remains instructions-only.
