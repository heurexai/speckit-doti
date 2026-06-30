Confirmed: `hx new` exposes exactly `--name/--company/--output/--profile/--agents/--json` (no `--framework`, `--license`, version, repo URL, or publish flags). The scaffold template ships `GitVersion.yml` but **no** `.github/`, no hx.config.json, no LICENSE/CONTRIBUTING/README, and no CI/release/dco workflows. All load-bearing claims in the raw data are verified against source. I have what I need to synthesize.

---

# Doti / hx New-Project Setup — Configurable Inventory & Proposed Config Design

## 1. Executive summary

Standing up a releasable doti/scaffold project today is a **guessing game** because the value surface is split across three disconnected places — (a) a handful of `hx new` / `hx doti install` CLI flags, (b) a dozen tracked repo-owned config files (`.doti/release.json`, `rules/*.json`, `.sentrux/rules.toml`, `GitVersion.yml`, `Directory.Build.props`, `.doti/memory/constitution.md`), and (c) machine-local + GitHub-side state that ships in *no file at all* (executable-adjacent `hx.config.json`, the `DOTI_RELEASE_ROOT` env var, the `dev`/`main` branch model, the three `.github/workflows`, NuGet Trusted-Publishing OIDC policy, branch protection, the DCO sign-off hook). `hx new` parameterizes only **two literal tokens** (`--name`→`HxScaffoldSample`, `--company`→`ACME_COMPANY`) and `hx doti install` exposes only **four** flags (`--repo/--agents/--force/--json`) — *verified at `ScaffoldCommandFactory.cs:98-102` and `RunnerCommandFactory.Doti.cs:216-219`*. Everything else is hardcoded, classification-derived, or a hand-edit the operator must *know* to perform. There is no single input that captures intent.

**Counts** (configurables that are real, distinct settings — overlaps de-duplicated):
- **Configurables: ~48 distinct** — of which **~21 Required**, **~6 Recommended**, **~21 Optional**.
- **Set today by:** ~12 CLI flags · ~22 config-file fields · ~3 env vars · ~6 manual edits / git ops · ~5 hardcoded / non-tunable.
- **Manual post-scaffold steps (the guessing game): 30** — *zero* of which `hx new`/`doti install` performs for you beyond the auto-armed pre-commit hook + `git init`/`git add`.

---

## 2. Configurable inventory

> De-duplication note: `--agents`, the five `.doti/release.json` fields, and the `localReleaseOutput.*` / env-var release-root chain each appeared under multiple agents' surfaces; they are listed **once** below at their canonical home. "Set today by" is the *real* mechanism, not the aspirational one.

### 2.1 Identity & metadata

| Key | Controls | Where it lives | Default | Set today by | Req/Rec/Opt | Audience |
|---|---|---|---|---|---|---|
| `--name` (`hx new`) | sourceName token `HxScaffoldSample` → every project/namespace/file name; drives PackageId suffix, ToolCommandName, RootNamespace, and all five `.doti/release.json` names + the constitution title | `ScaffoldCommandFactory.cs:98`; `template.json:8`; `ScaffoldReleaseTargetWriter.cs:7-13` | required (New fails Usage if blank) | **cli-flag** | Required | both |
| `--company` (`hx new`) | `company` token `ACME_COMPANY` → `<Company>`, `<Authors>`, PackageId **prefix**, and (transitively) `<Copyright>` holder | `ScaffoldCommandFactory.cs:99`; `template.json:16-21`; `Directory.Build.props:8-12` | `Heurex` | **cli-flag** | Recommended | both |
| `--output` (`hx new`) | output dir (`dotnet new -o`); location only, not substituted | `ScaffoldCommandFactory.cs:100` | `""` (required) | **cli-flag** | Required | both |
| `Copyright` | `Copyright © $(UtcNow.Year) $(Company)` — auto-year + company | `Directory.Build.props:12` | computed | **derived** (from `--company`) | Recommended | both |
| `RootNamespace` (CLI) | `<name>.Cli` | CLI `.csproj:4` | `<name>.Cli` | **derived** (from `--name`) | Optional | both |
| `Authors` (CLI) | `<Authors>` = company token (no separate authors param) | CLI `.csproj:15` | `= --company` | **derived** | Recommended | both |
| `name` (`.doti/integration.json`) | integration display name + constitution seed name on the `install` path | `DotiInstaller.cs:231` (= target dir leaf on install; = `--name` on `hx new`) | target folder name | **derived** (no `--name` on `install`) | Required | both |
| `{PROJECT_NAME}` (constitution title) | only machine-derived value in the constitution doc | `constitution-template.md:1`; `ProjectNameResolver.cs:11-38` (explicit `--name` > single top-level `.slnx`/`.sln` base name > repo dir) | `"scaffold-dotnet"` (this repo) | **derived** (settable via `hx new --name`) | Required | both |
| `RepositoryUrl` / `PackageProjectUrl` | source/repo links on the NuGet package | **absent everywhere** in the template | — (none) | **manual-edit** | Recommended | human |

### 2.2 Versioning

| Key | Controls | Where it lives | Default | Set today by | Req/Rec/Opt | Audience |
|---|---|---|---|---|---|---|
| GitVersion `next-version` seed | pins the series START → first `hx release` tags v0.1.0 (a floor GitVersion ignores once tags exceed it) | scaffold `GitVersion.yml:11` (auto-generated by `hx new`); **absent** from this repo's `GitVersion.yml` (past bootstrap) | `0.1.0` | **config-file-field** (no CLI flag) | Recommended | both |
| GitVersion `workflow` / `+semver` bump regexes | trunk-based GitHubFlow/v1: `main` bumps PATCH by default; `+semver: minor\|major` in the squash body opts up | `GitVersion.yml:5-11` + scaffold `GitVersion.yml:10` | `GitHubFlow/v1`; patch | **config-file-field** | Required | both |
| `doti cycle stamp --release-intent` | records `major\|minor\|patch` in the drift-review→release transition commit as a `+semver:` signal **before** `hx release` validates | `RunnerCommandFactory.Cycle.cs:79,85,90` | blank → minor (feature cycle) | **cli-flag** | Recommended | agent |
| `hx release --major\|--minor\|--patch` | explicit intent override, validated against the GitVersion-calculated bump | `LocalReleaseService.cs:594-615`; `LocalReleaseRequest.ReleaseIntent` | blank → cycle-type-aware | **cli-flag** | Optional | both |
| GitVersion tool version (CI) | pins CI GitVersion to 6.7.0 for parity with `hx version calculate` | `release.yml:50-57` | `6.7.0` | **hardcoded** (re-pin in lockstep) | Required | agent |
| Doc-version / Sync Impact Report | spec-kit constitutions carry these; doti deliberately **omits** them | `constitution-template.md:3` | intentionally absent | **hardcoded** (must NOT add) | Optional | both |

### 2.3 Release & publish

| Key | Controls | Where it lives | Default | Set today by | Req/Rec/Opt | Audience |
|---|---|---|---|---|---|---|
| `release.json schemaVersion` | manifest guard; load fails closed unless `== 1` | `release.json:2`; `ReleaseTargetManifest.cs:34-38` | `1` | **hardcoded** | Required | agent |
| `release.json productName` | product/display name in release identity; FS-safe project segment | `release.json:3`; written `ScaffoldReleaseTargetWriter.cs:10` | `<name>` (this repo: `speckit-doti`) | **config-file-field** (seeded from `--name`) | Required | both |
| `release.json packageName` | FS-safe package/project name (local release subdir); `SafeName(productName)` if unset. **NOT the NuGet id** | `release.json:4`; `ReleaseTargetManifest.cs:73-80` | `SafeName(<name>)` | **config-file-field** | Required | agent |
| `release.json publishProject` | rel-path `.csproj` `hx release` packs; validated repo-relative, no `..`, ends `.csproj`, must exist | `release.json:5`; `ReleaseTargetManifest.cs:108-129` | `src/<name>.Cli/<name>.Cli.csproj` (this repo overrides to `tools/...`) | **config-file-field** | Required | both |
| `release.json publishedExecutableName` | assembly/exe name `hx release` locates in publish output | `release.json:6`; `ReleaseTargetManifest.cs:43` | `<name>.Cli` (this repo: `Hx.Scaffold.Cli`) | **config-file-field** | Required | both |
| `release.json executableName` | final CLI command name (the `hx` alias) the smoke invokes | `release.json:7`; `ReleaseTargetManifest.cs:44` | `<name>` (this repo: `hx`) | **config-file-field** | Required | both |
| `release.json defaultReleaseRootEnvironmentVariable` | env-var NAME read for the local release root when `hx.config.json` gives no directory/override | `release.json:8`; `LocalReleaseRootResolver.cs:8` | `DOTI_RELEASE_ROOT` | **config-file-field** | Required | both |
| `PackageId` (NuGet) | `<company>.<name>` global-tool id | CLI `.csproj:14` | `Heurex.<name>` | **derived** (both tokens) | Required | both |
| `ToolCommandName` | `dotnet tool` command name | CLI `.csproj:13` | `<name>` | **derived** | Optional | both |
| `Description` (CLI pkg) | leading name substituted; rest fixed boilerplate "…a sample agent-first .NET CLI generated by the Heurex scaffold." | CLI `.csproj:16` | fixed sentence | **hardcoded** (lib project has none) | Optional | human |
| `PackageLicenseExpression` | SPDX license | CLI `.csproj:17` | `MIT` | **hardcoded** (no flag) | Recommended | both |
| `IsPackable` | nothing packs by default; CLI opts back in (`PackAsTool=true`) | `Directory.Build.props:14`; CLI `.csproj:11-12` | false repo-wide / true CLI | **hardcoded** | Optional | both |
| `hx.config.json schemaVersion` | guard; `LoadRequired` throws unless `== 1` | `hx.config.json:2`; `HxLocalConfiguration.cs:79-83` | `1` | **hardcoded** | Required | agent |
| `hx.config.json localReleaseOutput.enabled` | master switch; false skips local copy (tag+pack proofs still run) | `HxLocalConfiguration.cs:28`; `LocalReleaseService.cs:249-259` | `true` | **config-file-field** (executable-adjacent; **not** shipped to scaffold) | Optional | both |
| `hx.config.json localReleaseOutput.directory` | explicit absolute root; **wins** over env var; must be absolute & outside the repo | `HxLocalConfiguration.cs:37`; safety `LocalReleaseService.cs:795-804` | null | **config-file-field** | Optional | agent/human |
| `hx.config.json localReleaseOutput.environmentVariable` | machine-local override of the env-var NAME (e.g. `HEUREX_RELEASE_ROOT`); ignored when `directory` set | `HxLocalConfiguration.cs:45` | null → release.json default → `DOTI_RELEASE_ROOT` | **config-file-field** | Optional | agent/human |
| `DOTI_RELEASE_ROOT` (or overridden name) env var | the actual local dir artifacts copy to; unset → local copy silently skipped | `LocalReleaseRootResolver.cs:45`; `LocalReleaseService.cs:264` | unset | **env-var** | Optional | both |
| `hx.config.json llmModelRoot` / `HEUREX_LLM_ROOT` | local LLM root for the **advisory** semantic drift finder; config wins; never gating | `HxLocalConfiguration.cs:19` | null (finder skips) | **config-file-field** | Optional | agent/human |
| `NUGET_USER` (repo secret) | nuget.org username for the OIDC login action (Trusted Publishing); **no fallback API key** | `release.yml:138-139` | absent | **manual-edit** (GitHub secret) | Required | human |

> **Resolution precedence** (single coded chain, `LocalReleaseService.cs:241-245` + `LocalReleaseRootResolver.Resolve:16-66`): `hx.config.json` `directory` ▸ env var named by `hx.config.json` `environmentVariable` ▸ `release.json` `defaultReleaseRootEnvironmentVariable` ▸ `DOTI_RELEASE_ROOT` ▸ **skip the local copy** (tag + pack still run).

### 2.4 Agents & integration

| Key | Controls | Where it lives | Default | Set today by | Req/Rec/Opt | Audience |
|---|---|---|---|---|---|---|
| `--agents` (`hx new` + `doti install`) | which agent skill trees + root entrypoints render (`claude`→`.claude/skills`+`CLAUDE.md`; `codex`→`.agents/skills`+`AGENTS.md`); written to both manifests' `agents[]` | `ScaffoldCommandFactory.cs:102`; `RunnerCommandFactory.Doti.cs:217`; `DotiAgentTarget.cs:33-52` | `codex,claude` | **cli-flag** (only `claude`/`codex` valid; others rejected — `DotiAgentTarget.cs:42`) | Optional | both |
| `--profile` (`hx new`) | template selector on `ScaffoldRequest.Profile`; does **not** alter csproj/metadata; only `dotnet-cli` dir exists | `ScaffoldCommandFactory.cs:101` (ignored by `TemplateGenerator.cs:53`) | `dotnet-cli` | **cli-flag** (effectively single-valued) | Optional | both |
| `profile` / tier (`.doti/integration.json` + `init-options.json`) | the doti tier → gate posture (`dotnet-cli`/`-heurex` enforce; `dotnet-lib`/`workflow-only` skip Sentrux/ArchUnit). **The single biggest install-time behavior knob, and the caller cannot set it** | `DotiInstaller.cs:248-261` (derived: new/empty→`dotnet-cli`; existing-non-Doti→`workflow-only`; upgrade→prior) | `dotnet-cli` (new) | **derived** (no `--profile`/`--tier` on `install`) | Required | agent |
| `maturity` (both manifests) | workflow maturity band | `DotiInstaller.cs:233,238` | `command-aware-advisory` | **hardcoded** | Required | agent |
| `generatedBy` (`integration.json`) | provenance stamp | `DotiInstaller.cs:234` | `{phase:8, mode:"scaffold-cli-new"}` | **hardcoded** | Required | agent |
| `context`/`workflow`/`constitution` paths (`integration.json`) | fixed-layout pointers | `DotiInstaller.cs:233-234,239` | `.doti/agent-context.md` etc. | **hardcoded** | Required | agent |
| `implementedCommands` map (`profiles/dotnet-cli/profile.json`) | canonical command-string catalogue the skills reference | `profile.json:13-72` | fixed | **hardcoded** (payload-fixed) | Required | agent |
| payload source resolution | tree the payload reconciles FROM (runner: nearest ancestor with `.doti/core/skills.json`; scaffold: beside `hx.exe`) | `RunnerCommands.Doti.cs:19-33`; `ScaffoldCommands.DotiInstall.cs:89-95` | derived | **derived** (no flag; known CWD-vs-bundled divergence) | Required | agent |
| `prerequisites.json` copy | copies bundled `.doti/core/prerequisites.json` → `.doti/prerequisites.json` (best-effort) | `DotiInstaller.cs:282-293` | copied when present | **hardcoded** | Optional | agent |

### 2.5 Architecture & gates

| Key | Controls | Where it lives | Default | Set today by | Req/Rec/Opt | Audience |
|---|---|---|---|---|---|---|
| `profile gates` map (`profiles/<tier>/profile.json`) | `sentrux-verify`/`sentrux-check`/`architecture-test` → `enforced\|advisory\|skip`; **undeclared step defaults to Enforced** | `profile.json:7-11` per tier; `GateRunner.cs:54` | `dotnet-cli`: all enforced; `workflow-only`/`dotnet-lib`: all skip | **config-file-field** (payload-fixed tier defs; you select a tier, not edit the map) | Required | agent |
| `--force` (`doti install`) | overwrite operator-modified/unbaselined managed assets vs preserve (.new sidecar / blocked) + prune orphans/legacy `doti/` | `RunnerCommandFactory.Doti.cs:218`; branch points `DotiInstaller.cs:574,587,593,452,459` | `false` (preserve) | **cli-flag** | Optional | agent |
| `--json` (`doti install`) | JSON proof envelope vs human render; presentation only | `RunnerCommandFactory.Doti.cs:219` | false | **cli-flag** | Optional | both |
| `Directory.Build.props` analyzer/audit policy | build-as-SAST: `TreatWarningsAsErrors`, `AnalysisModeSecurity=All`, `NuGetAudit*=all/low`, `net10.0`; CA3xxx/CA5xxx → build errors | `Directory.Build.props:1-26` (shipped) | net10.0 / warnings-as-errors / All / all / low | **config-file-field** (tunable; mostly opinionated) | Required | agent |
| `Nullable`/`ImplicitUsings`/`LangVersion`/`TreatWarningsAsErrors` | language/build defaults | `Directory.Build.props:4-7` | enable/enable/latest/true | **hardcoded** | Optional | both |
| `TargetFramework` | TFM for every project; template *declares* a `framework` choice symbol (sole choice net10.0) but `hx new` never forwards it | `Directory.Build.props:3`; `template.json:23-32` | `net10.0` | **hardcoded via `hx new`** (raw `dotnet new -f` only) | Required | both |
| `includeArchitectureTests` | emit the ArchUnitNET test project (drives slnx `#if` + source modifier); `hx new` never passes it | `template.json:33-38`; `dotnetcli.host.json:12-15` | `true` (always, via `hx new`) | **hardcoded via `hx new`** | Optional | both |
| `.sentrux/rules.toml max_cycles` | max dependency cycles in production code (0 = acyclic) | `.sentrux/rules.toml:8`; scaffold `:6` | `0` | **config-file-field** | Required | agent |
| `.sentrux/rules.toml max_cc` | max cyclomatic complexity per function | `.sentrux/rules.toml:9`; scaffold `:7` | `25` | **config-file-field** | Required | agent |
| `.sentrux/rules.toml max_fn_lines` | max function length (the /08 offender) | `.sentrux/rules.toml:10`; scaffold `:8` | `120` | **config-file-field** | Required | agent |
| `.sentrux/rules.toml no_god_files` | flag god-files (currently off) | `.sentrux/rules.toml:11`; scaffold `:9` | `false` | **config-file-field** | Optional | agent |
| `.sentrux/rules.toml [[layers]]`/`[[boundaries]]` | layered dependency-direction contract + forbidden edges; **project-specific paths** | `.sentrux/rules.toml:14-64`; scaffold `:12-26` | scaffold: library(0)→cli(1) over `src/HxScaffoldSample` | **config-file-field** (must hand-edit paths) | Required | agent |
| `rules/sentrux.json` | gate wiring: enabled, baselinePath, rulesConfigPath, signalToleranceBand(100), requiredFeatures, requiredGrammars(csharp) | `rules/sentrux.json:1-11` (shipped) | enabled, band 100, csharp | **config-file-field** | Required | agent |
| `rules/architecture.json` families | human-readable ArchUnit family contract (enforced in test code, **not** by this file) | `rules/architecture.json` (shipped, 9 families + layers) | scaffold: 9 families | **config-file-field** (edit namespaces) | Required | agent |
| `rules/hygiene.json` | Gitleaks gate: gitleaksEnabled, defaultCommitScope, fullScanRequiredForRelease, changedFileThreshold(2000), excludePaths, allowedUrlPrefixes…; re-renders the native Gitleaks config | `rules/hygiene.json:1-61`; `ToolVendor.cs:14,45` | enabled; changed; full@release | **config-file-field** | Required | agent |
| `rules/security.json` | `security scan` auditFloor + SCA suppressions | `rules/security.json:1-6` (shipped) | `auditFloor=low`, `suppressions=[]` | **config-file-field** | Optional | agent |
| `prerequisites.json` mins | `.NET SDK ≥10.0.0`, `Git ≥2.40.0`, winget ids (`Microsoft.DotNet.SDK.10`, `Git.Git`) | `.doti/core/prerequisites.json:1-43` | as above | **config-file-field** (effectively payload-fixed) | Required | agent |

### 2.6 Constitution & project standards

> §2 is read as **one verbatim text slice** (`ConstitutionService.ExtractSection2`, `ConstitutionService.cs:58-84`, anchor `## §2` → EOF). There is **no per-section parser and no config-file seam** to seed any §2 content. `hx doti constitution` is **read-only** (`RunnerCommands.Doti.Constitution.cs:16-32`).

| Key | Controls | Where it lives | Default | Set today by | Req/Rec/Opt | Audience |
|---|---|---|---|---|---|---|
| §2 Domain principles | what the project IS + domain invariants /03 & /04 evaluate against | `constitution-template.md:29` `[DOMAIN_PRINCIPLES]` | literal placeholder | **manual-edit** (via /doti-constitution) | Required | both |
| §2 Tech stack (beyond .NET 10) | chosen libs/tools above the §1 baseline | `constitution-template.md:33` `[TECH_STACK]` | literal placeholder | **manual-edit** | Required | both |
| §2 Coding style | code-organization conventions beyond §1 | `constitution-template.md:37` `[CODING_STYLE]` | literal placeholder | **manual-edit** | Required | both |
| §2 Security & compliance | posture beyond §1 hygiene/SAST | `constitution-template.md:41` `[SECURITY_COMPLIANCE]` | literal placeholder | **manual-edit** | Required | both |
| §2 Performance | performance conventions | `constitution-template.md:45` `[PERFORMANCE]` | literal placeholder | **manual-edit** | Required | both |
| §2 extra headings (e.g. self-hosting) | free-form — author may add headings beyond the five (this repo's `### Self-hosting defect handling`) | this repo `constitution.md:53-55`; **not** in template | none | **manual-edit** | Optional | agent |
| §1 Inherited invariants | the ~10 codified givens (Deterministic Ownership, Thin CLI+CliResult, GitVersion, Sentrux/analyzers, Codified Cycle…) | `constitution-template.md:5-21` | fixed reference | **hardcoded** — **NONE are toggle-able by explicit design** (`:21`) | Required | both |

### 2.7 Git, branch & CI

| Key | Controls | Where it lives | Default | Set today by | Req/Rec/Opt | Audience |
|---|---|---|---|---|---|---|
| insurance pre-commit hook | auto-armed untracked `.git/hooks/pre-commit` blocking bare commits (skipped if non-git; refuses to overwrite a foreign hook) | `HookInstaller.cs:26-32`; arming `RunnerCommands.Doti.Install.cs:53-62` | armed for git repos | **hardcoded** (no opt-out flag; `doti install-hooks` re-arms per clone) | Required | agent |
| `.gitignore` runtime-state entries | ensures 4 ignore entries (`cycle-state.json`, `gate-proof.json`, `sentrux-optimization-log.json`, `templates/`) | `DotiGitIgnore.cs:17-24` | the 4 fixed entries | **hardcoded** (additive) | Required | agent |
| baseline commit | `hx new` runs `git init`+`git add -A` but **commits nothing**; GitVersion needs a commit; the hook blocks bare commits | `FirstSmokeRunner.cs:106-110` | not made | **git-operation** | Required | both |
| `dev`/`main` two-branch model | release path assumes it (`doti cycle stamp --base` defaults to "dev if it resolves, else HEAD"; release on dev→main squash-merge); a fresh `git init` has neither | `RunnerCommandFactory.Cycle.cs:78`; `release.yml:3-6` | only `master`/`main` exists | **git-operation** | Required | both |
| `.github/workflows/{release,ci,dco}.yml` | tag/pack/publish + CI + DCO automation; **the scaffold ships no `.github/`** | exist only in this repo | absent in generated repo | **manual-edit** | Required | both |
| `production` GitHub Environment | gates OIDC issuance on the publish job; name MUST equal the nuget.org policy's Environment field; add a required reviewer | `release.yml:121` | absent | **manual-edit** (GitHub-side) | Required | human |
| NuGet Trusted-Publishing OIDC policy | nuget.org policy (owner/repo/workflow/environment); OIDC-only, no fallback key | `release.yml:19-21,136-143` | absent | **manual-edit** (nuget.org-side) | Required | human |
| `main` branch protection | `required_linear_history` MUST be **OFF** (dev→main is a merge commit), + required status checks, enforce_admins | `release.yml:3-6` + MEMORY | absent | **manual-edit** (GitHub-side; agent is blocked from setting) | Required | human |
| DCO sign-off | every non-merge PR commit needs `Signed-off-by`; satisfied by untracked `.git/hooks/prepare-commit-msg` (no-reply email) **or** `git commit -s` | `dco.yml:42-51`; hook `prepare-commit-msg:14-21` | enforced in CI; hook hand-installed | **git-operation** (per clone) | Required | human |

---

## 3. The guessing game (manual steps today)

Every step below is something a human must currently know to do *after* `hx new`/`doti install` — none is automated beyond the auto-armed pre-commit hook and `git init`/`git add`. Each lists the evidence and the **config field that would eliminate it** under the proposed design (§4).

### A. Project metadata & files (`hx new` path)
1. **Author a README** — template ships none (`Glob …/README* = no files`). → `identity.description` + a generated `README.md` from a template.
2. **Set `RepositoryUrl`/`PackageProjectUrl`** on the CLI csproj — absent everywhere (`CLI .csproj:1-18`). → `identity.repositoryUrl`.
3. **Edit the CLI `<Description>`** (fixed "sample…Heurex scaffold" boilerplate) and add a `<Description>` to the empty library project (`CLI .csproj:16`; `lib .csproj:1-2`). → `identity.description`.
4. **Change the license** if not MIT (hardcoded `MIT`, `CLI .csproj:17`). → `publish.license`.
5. **Set `<Authors>` independently of Company** (`Authors`=company token, `CLI .csproj:15`). → `identity.authors`.
6. **Pick TFM ≠ net10.0 / omit arch-tests** — only via raw `dotnet new -f/-a`; `hx new` forwards neither (`TemplateGenerator.cs:53`; `template.json:23-38`). → `gates.framework`, `gates.architectureTests`.
7. **Decide the version series** — no version flag; `next-version` hardcoded `0.1.0` (`GitVersion.yml:11`). → `versioning.nextVersion`.
8. **Verify/override the auto-derived constitution title** — filled once, never overwritten (`ConstitutionInitializer.cs:28-37`; `ProjectNameResolver.cs:11-38`). → `identity.name` (already feeds it via `--name`).

### B. Constitution authoring
9. **Author all five §2 sections** as free prose, replacing each `[PLACEHOLDER]` (`constitution-template.md:29,33,37,41,45`); §2 has no config seam (`ConstitutionService.cs:58-84`). → `constitution.{domainPrinciples,techStack,codingStyle,securityCompliance,performance}` (free-text fields written into §2).
10. **Run `/doti-constitution`** to drive the authoring + verify zero placeholders remain (`hx doti constitution` is read-only, `:16-32`). → the wizard/`--config` writes §2 directly; the skill stays the iterative path.
11. **Do NOT add** a SemVer doc-version, Sync Impact Report, or any versioning/CLI-shape/quality-gate placeholder; do NOT re-declare §1 (`constitution-template.md:3,21`; `doti-constitution.md:10-11`). → not a field; documented guardrail in the config schema.

### C. Gate config localization (repo-owned files)
12. **Hand-edit `.sentrux/rules.toml` `[[layers]]`/`[[boundaries]]` paths** to the project's real assemblies (scaffold ships `src/HxScaffoldSample[.Cli]/*`; mid-segment globs unconfirmed, `rules.toml:4-6,12-26`). → `gates.sentruxLayers[]` (the generator renders the toml from the project layout).
13. **Hand-edit `rules/architecture.json` `layers{}` namespaces + families** (hardcoded `HxScaffoldSample` namespaces, `architecture.json:4-13`); the ArchUnit **test code** must also change. → `gates.architectureNamespaces` (render the json + a test stub from the layout).
14. **Tune `rules/{security,hygiene}.json`** per project (e.g. `allowedUrlPrefixes`, `excludePaths`, `suppressions`). → `gates.security.suppressions`, `gates.hygiene.allowedUrlPrefixes`.

### D. Tier selection (no flag exists)
15. **Choose a non-default tier** (Tier-2 `dotnet-lib`, or force Tier-3 `dotnet-cli-heurex` on a foreign repo) by hand-editing **both** `.doti/integration.json` and `.doti/init-options.json` `profile` (`DotiInstaller.cs:248-261`; gate posture `profile.json:7-12`). → `agents.tier` (or `gates.tier`).
16. **Set a repo/integration `name` ≠ folder** on the `install` path (no `--name` wired; `DotiInstaller.cs:231`). → `identity.name` (the unified config supplies it to `install` too).
17. **Add a 3rd agent flavor** — requires a **code** change, not config (`DotiAgentTarget.cs:18-44`). → *cannot be config* (see §6).

### E. `doti install` reconciliation hygiene
18. **Resolve a pre-existing non-Doti pre-commit hook** before installing (fail-closes `VerdictExternal`, `HookInstaller.cs:85-88`; `RunnerCommands.Doti.Install.cs:29-42`). → no field; operator decision (move/delete, re-run).
19. **Reconcile `.new` sidecars / `blocked` assets** after install over a customized repo, or re-run `--force` (`DotiInstaller.cs:574-614,315-317`). → no field; `--force` already exists.
20. **Re-arm the insurance hook in every clone/worktree** via `doti install-hooks` (git-local, `HookInstaller.cs:7-14`; `RunnerCommandFactory.Doti.cs:304-315`). → no field; per-clone op (could be a `git config core.hooksPath` improvement — see §6).
21. **Provide an executable-adjacent `hx.config.json`** before the *scaffold* `hx doti install` (wrapped in `WithRequiredConfiguration`, `ScaffoldCommandFactory.cs:226-233`). → ships with the global tool, not per-repo.

### F. Release & publish enablement
22. **Make the first (baseline) commit** — `hx new` stages but doesn't commit; the hook blocks bare commits (`FirstSmokeRunner.cs:106-110`; `HookInstaller.cs:26-32`). → `git.baselineCommit: true` (hx makes a sanctioned commit).
23. **Create the `dev`/`main` two-branch model** (`RunnerCommandFactory.Cycle.cs:78`; `release.yml:3-6`). → `git.branches.{working,release}` (hx creates them).
24. **Add the three `.github/workflows`** (release/ci/dco) — scaffold ships no `.github/`. → `git.ci.workflows: true` (hx emits them, parameterized by publish target).
25. **Set up NuGet Trusted Publishing**: OIDC policy + `NUGET_USER` secret + `production` Environment with reviewer (`release.yml:19-21,121,136-143`). → `publish.nuget.{owner,repo,workflow,environment}` documents intent; **the nuget.org policy + secret + Environment stay operator-only** (see §6).
26. **Configure `main` branch protection** (`required_linear_history` OFF, status checks, enforce_admins) — agent is blocked (`release.yml:3-6`; MEMORY). → `git.branchProtection.*` documents intent; **execution is operator-only** (GitHub-side).
27. **Install the DCO auto-signoff hook or `git commit -s`** (`dco.yml:42-51`; `prepare-commit-msg:1-21`; `CONTRIBUTING.md:20-29`). → `git.dco.signoffHook: true` (hx writes the prepare-commit-msg hook).
28. **Provide `hx.config.json` and/or set `DOTI_RELEASE_ROOT`** to enable the local release copy (skipped silently when unset, `LocalReleaseService.cs:244-267`). → `release.localOutput.{enabled,environmentVariable,directory}` (machine-local; hx can scaffold a template).
29. **Tune `.doti/release.json` paths** if the layout differs from `src/<Name>.Cli` (written once, `ScaffoldReleaseTargetWriter.cs:7-14`; this repo hand-changed to `tools/…`). → `release.{publishProject,publishedExecutableName,executableName,packageName}`.
30. **Push the `v*` tag** to trigger publishing — `hx release` creates/verifies the tag but never pushes (`LocalReleaseService.cs:662-669`; `README.md:250`); `/09-doti-release` owns it. → no field; `/09` op.

---

## 4. Proposed unified config file — `doti-setup.json`

A single agent-facing input consumed by `hx new --config` (and `hx doti install --config`). Required fields are marked `// REQUIRED`; everything else has a realistic default and may be omitted. Nesting mirrors §2's categories. The generator projects these into the existing template tokens, `.doti/*` manifests, `rules/*`, `GitVersion.yml`, `Directory.Build.props`, the constitution §2, and the `.github/` + git scaffolding.

```jsonc
{
  "schemaVersion": 1,                       // REQUIRED — guard, must be 1

  "identity": {
    "name": "Acme.Widgets",                 // REQUIRED — sourceName + release names + constitution title
    "company": "Acme Corp",                 // recommended — ACME_COMPANY token; default "Heurex"
    "authors": "Acme Corp",                 // optional — <Authors>; default = company
    "output": "./out/acme-widgets",         // REQUIRED on `hx new` — dotnet new -o (omit for `install`)
    "description": "Acme Widgets CLI.",     // recommended — replaces the fixed sample boilerplate
    "license": "MIT",                       // recommended — PackageLicenseExpression; default MIT
    "repositoryUrl": "https://github.com/acme/widgets",  // recommended — RepositoryUrl/PackageProjectUrl (absent today)
    "generateReadme": true                  // optional — emit a README from a template (none ships today); default false
  },

  "versioning": {
    "nextVersion": "0.1.0",                 // recommended — GitVersion next-version seed; default 0.1.0
    "workflow": "GitHubFlow/v1"             // optional — GitVersion workflow; default GitHubFlow/v1 (do NOT drop)
  },

  "release": {                              // → .doti/release.json (tracked, repo-owned)
    "packageName": "Acme.Widgets",          // optional — FS-safe local-release name; default SafeName(name). NOT the NuGet id
    "publishProject": "src/Acme.Widgets.Cli/Acme.Widgets.Cli.csproj", // REQUIRED-derived; override if layout differs
    "publishedExecutableName": "Acme.Widgets.Cli",  // REQUIRED-derived from name
    "executableName": "widgets",            // REQUIRED-derived — the on-disk command name (defaults to name)
    "defaultReleaseRootEnvironmentVariable": "DOTI_RELEASE_ROOT",  // optional; default DOTI_RELEASE_ROOT
    "localOutput": {                        // → executable-adjacent hx.config.json (machine-local; scaffolds a template only)
      "enabled": true,                      // optional; default true
      "environmentVariable": "DOTI_RELEASE_ROOT", // optional — env-var NAME for the root
      "directory": null                     // optional — absolute path; WINS over env. NEVER commit a machine path
    }
  },

  "publish": {                              // documents intent; nuget.org/GitHub execution stays operator-only (see §6)
    "target": "nuget",                      // optional — "nuget" | "none"; default "none"
    "nuget": {
      "owner": "acme",                      // REQUIRED if target=nuget — OIDC policy owner
      "repo": "widgets",                    // REQUIRED if target=nuget
      "workflow": "release.yml",            // optional; default release.yml
      "environment": "production",          // optional; default production (Environment name MUST equal the policy field)
      "userSecretName": "NUGET_USER"        // optional; default NUGET_USER (the repo secret to create — operator-only)
    }
  },

  "agents": {
    "flavors": ["codex", "claude"],         // optional — only codex/claude valid; default both
    "tier": "dotnet-cli"                    // REQUIRED-effective — dotnet-cli | dotnet-cli-heurex | dotnet-lib | workflow-only
                                            //   (no flag exists today; this is the single biggest behavior knob)
  },

  "gates": {                                // repo-owned: Directory.Build.props, .sentrux, rules/*
    "framework": "net10.0",                 // optional — TFM; default net10.0 (only choice today)
    "architectureTests": true,              // optional — emit ArchUnitNET project; default true
    "treatWarningsAsErrors": true,          // optional; default true
    "sentrux": {                            // → .sentrux/rules.toml + rules/sentrux.json
      "maxCycles": 0,                       // optional; default 0
      "maxCyclomaticComplexity": 25,        // optional; default 25
      "maxFunctionLines": 120,              // optional; default 120
      "noGodFiles": false,                  // optional; default false
      "layers": [                           // REQUIRED for Tier-3 — rendered from the project layout (placeholder paths today)
        { "name": "library", "order": 0, "paths": ["src/Acme.Widgets/*"] },
        { "name": "cli",     "order": 1, "paths": ["src/Acme.Widgets.Cli/*"] }
      ]
    },
    "architecture": {                       // → rules/architecture.json (+ a generated ArchUnit test stub)
      "rootNamespace": "Acme.Widgets"       // optional — replaces HxScaffoldSample namespaces; default = name
    },
    "hygiene": {                            // → rules/hygiene.json (re-renders the Gitleaks native config)
      "allowedUrlPrefixes": [],             // optional; default []
      "excludePaths": []                    // optional; default []
    },
    "security": {                           // → rules/security.json
      "auditFloor": "low",                  // optional; default low
      "suppressions": []                    // optional; default []
    }
  },

  "constitution": {                         // → §2 of .doti/memory/constitution.md (free prose, written verbatim)
    "domainPrinciples": "…",                // REQUIRED — replaces [DOMAIN_PRINCIPLES]
    "techStack": "…",                       // REQUIRED — replaces [TECH_STACK] (deltas ABOVE the .NET 10 baseline)
    "codingStyle": "…",                     // REQUIRED — replaces [CODING_STYLE] (do NOT re-declare §1 rules)
    "securityCompliance": "…",              // REQUIRED — replaces [SECURITY_COMPLIANCE]
    "performance": "…",                     // REQUIRED — replaces [PERFORMANCE]
    "extraSections": []                     // optional — [{ "heading": "Self-hosting", "body": "…" }]; §2 is free-form
  },

  "git": {
    "baselineCommit": true,                 // optional — make the first sanctioned commit; default true on `hx new`
    "branches": {                           // optional — create the two-branch model the release path assumes
      "working": "dev",                     // default dev
      "release": "main"                     // default main
    },
    "ci": { "workflows": true },            // optional — emit .github/workflows/{release,ci,dco}.yml; default false today, true under this design
    "dco": { "signoffHook": true },         // optional — write the prepare-commit-msg auto-signoff hook; default true
    "branchProtection": {                   // DOCUMENTS intent only — execution is GitHub-side / operator-only
      "requiredLinearHistory": false,       // MUST be false (dev→main is a merge commit)
      "requiredStatusChecks": true,
      "enforceAdmins": true
    }
  }
}
```

---

## 5. Proposed interactive flow (`hx new --interactive`)

Human-facing wizard. Each prompt maps 1:1 to a `doti-setup.json` field, so the two paths are interchangeable. Order = least-to-most optional; conditional branches gated on earlier answers. All prompts asked in plain prose (no dialog widget), one at a time.

**Group 1 — Identity** *(always)*
1. *Project name?* → `identity.name` · **validate:** non-empty; safe `dotnet new -n` token · no default.
2. *Company / owner?* → `identity.company` · default `Heurex` · validate: non-empty.
3. *Output directory?* → `identity.output` *(skipped on `install`)* · validate: writable path · no default.
4. *One-line package description?* → `identity.description` · default the leading-name sample sentence (warn it's boilerplate).
5. *License (SPDX)?* → `publish`/`identity.license` · default `MIT`.
6. *Repository URL (for the NuGet package)?* → `identity.repositoryUrl` · default empty (warn: package will have no source link).
7. *Generate a starter README?* → `identity.generateReadme` · default `yes`.

**Group 2 — Tier & agents** *(always; the load-bearing one)*
8. *Which doti tier?* `dotnet-cli` (CLI, structural gates enforced) / `dotnet-cli-heurex` / `dotnet-lib` (library, gates skipped) / `workflow-only` (no Sentrux/ArchUnit) → `agents.tier` · default `dotnet-cli` · **explain the gate consequence inline.**
9. *Which agent toolchains?* `codex` / `claude` (multi-select) → `agents.flavors` · default both · validate: subset of {codex, claude}.

**Group 3 — Versioning** *(always)*
10. *Starting version series?* → `versioning.nextVersion` · default `0.1.0` · validate: SemVer.

**Group 4 — Gates** *(conditional: only if tier ∈ {dotnet-cli, dotnet-cli-heurex})*
11. *Emit the ArchUnitNET architecture-test project?* → `gates.architectureTests` · default `yes`.
12. *Confirm Sentrux limits* (max function lines 120 / complexity 25 / cycles 0) — accept or override → `gates.sentrux.*` · defaults as shown.
13. *Root namespace for layer/architecture rules?* → `gates.architecture.rootNamespace` + `gates.sentrux.layers[].paths` · default = `identity.name` (auto-renders `src/<Name>[.Cli]` layers).

**Group 5 — Git & CI** *(always)*
14. *Make the baseline commit now?* → `git.baselineCommit` · default `yes`.
15. *Create the dev/main two-branch model?* → `git.branches` · default `yes` (`dev`/`main`).
16. *Add the CI/release/DCO GitHub workflows?* → `git.ci.workflows` · default `yes`.
17. *Install the DCO auto-signoff hook?* → `git.dco.signoffHook` · default `yes`.

**Group 6 — Publish** *(conditional: branch on Q18)*
18. *Publish to NuGet via Trusted Publishing?* → `publish.target` · default `no`.
&nbsp;&nbsp;**if yes →**
&nbsp;&nbsp;19. *GitHub owner?* → `publish.nuget.owner` · validate non-empty.
&nbsp;&nbsp;20. *GitHub repo?* → `publish.nuget.repo`.
&nbsp;&nbsp;21. *Release workflow file?* → `publish.nuget.workflow` · default `release.yml`.
&nbsp;&nbsp;22. *OIDC Environment name?* → `publish.nuget.environment` · default `production` (note: must equal the nuget.org policy field).
&nbsp;&nbsp;23. **Print an operator checklist** (not a prompt) — "Create the nuget.org Trusted-Publishing policy, the `NUGET_USER` secret, the `production` Environment with a reviewer, and set `main` branch protection with `required_linear_history` OFF" → maps to `publish.nuget.*` + `git.branchProtection.*` as *documented, non-executed* intent.

**Group 7 — Local release** *(conditional: only if Q18 = no, or as an extra)*
24. *Configure a local release output directory?* → `release.localOutput.*` · default `no` (env-var `DOTI_RELEASE_ROOT` fallback).

**Group 8 — Constitution** *(always, last — heaviest authoring)*
25–29. *Author §2:* Domain principles / Tech stack (beyond .NET 10) / Coding style / Security & compliance / Performance → `constitution.*` · each free-text, **no default**, validate: non-empty + warn if it re-declares a §1 invariant. *(May defer to `/doti-constitution` — Q25 offers "fill now / defer to the skill".)*

---

## 6. Design recommendation

**Two entry shapes, one schema.** Add `--config <path-to-doti-setup.json>` (agent path) and `--interactive` (human wizard, §5) to **both** `hx new` and `hx doti install`. They populate the *same* `doti-setup.json` object; the wizard simply writes one and then runs the `--config` codepath, so behavior is identical and the file is reproducible. Precedence: explicit individual flags (`--name`, `--company`, `--agents`, `--output`, `--profile`, `--force`) **override** the matching `--config` field, so existing scripts keep working and the config is a base layer. The wizard refuses to start if `--config` is also passed (ambiguous).

**Existing flags survive, unchanged.** `--name/--company/--output/--profile/--agents/--json/--force` remain exactly as today (`ScaffoldCommandFactory.cs:98-102`; `RunnerCommandFactory.Doti.cs:216-219`). They become the high-priority overrides above. No breaking change.

**Where the config file lands.** `hx new` writes the resolved config to **`.doti/setup.json`** (tracked, repo-owned) so re-running `hx doti install` / upgrades read the same intent, and so the project is regenerable. This also gives the install path the `name` and `tier` it cannot get from a flag today (eliminating manual steps #15, #16). Make it the canonical input the `install` upgrade path consults *first*, then falls back to the current `integration.json` `profile`-preservation behavior (`DotiInstaller.cs:263-280`).

**Idempotency.** The generator must be re-runnable: on a second `hx new`/`install`, fields that match the current on-disk state are no-ops; changed fields re-render their target file under the existing managed-asset reconciliation (preserve operator edits, `.new` sidecar, `--force` to overwrite — `DotiInstaller.cs:574-614`). The constitution §2 stays write-once-when-absent (never clobber an authored file, `ConstitutionInitializer.cs:28-37`); a `--config` re-run with new §2 content surfaces a `.new` sidecar rather than overwriting.

**hx SHOULD automate (currently manual):**
- Constitution §2 authoring from `constitution.*` (steps #9–10) — write the verbatim slice; the skill stays the iterative refiner.
- `RepositoryUrl`/`PackageProjectUrl`/`Description`/`license`/`authors` token expansion (steps #2–5) and a generated README (#1).
- Sentrux/architecture rule **localization** from the project layout (steps #12–13) — render `.sentrux/rules.toml` layers, `rules/architecture.json` namespaces, **and** an ArchUnit test stub keyed off `identity.name`.
- Tier selection into **both** manifests (step #15) and a non-folder integration name (#16).
- The baseline sanctioned commit (#22), `dev`/`main` creation (#23), the three `.github/workflows` (#24), the DCO prepare-commit-msg hook (#27), and a starter machine-local `hx.config.json` template (#28).
- `GitVersion.yml next-version` from `versioning.nextVersion` (#7).

**Must stay operator-only (cannot be automated — and why):**
- **NuGet Trusted-Publishing OIDC policy + `NUGET_USER` secret + `production` Environment + reviewer** (#25): these live on nuget.org and GitHub, require the owner's authenticated session, and minting an API key is exactly the trust boundary OIDC exists to protect. hx can only *emit the workflow and print the checklist*.
- **`main` branch protection** (#26): GitHub-side; the agent classifier explicitly blocks changing branch protection / force-pushing `dev` (MEMORY). hx documents the required settings (`required_linear_history` OFF) but does not apply them.
- **Pushing the `v*` tag** (#30): `hx release` deliberately does not push (`LocalReleaseService.cs:662-669`); `/09-doti-release` + the operator own the remote push that triggers publishing.
- **Resolving a foreign pre-existing pre-commit hook** (#18): fail-closed by design (`HookInstaller.cs:85-88`); a destructive auto-fix would defeat the safety guard.
- **Adding a third agent flavor** (#17): the two `DotiAgentTarget` records are static + the `FromKey` switch is exhaustive (`DotiAgentTarget.cs:18-44`); a new agent needs a render template + code, so it is a **schema-validated rejection**, not a config field. (Improvement worth noting: per-clone hook re-arming, #20, could be eliminated by `git config core.hooksPath`, but that's a separate change.)

---

## 7. Completeness self-check — areas to verify before building

These are surfaces the raw data touched only partially; verify each before `/01-doti-specify` so nothing is silently dropped:

1. **`ReleaseTargetManifest.WriteDefault` overloads.** I confirmed `ScaffoldReleaseTargetWriter` calls it with 5 named args (`:8-14`) and that `packageName` defaults to `SafeName(productName)`. I did **not** open `ReleaseTargetManifest.cs` directly — confirm the full field set, every validation rule (the `productName`/`packageName`/`executableName` `SafeName` regexes), and whether any field beyond these is required, before declaring the `release.*` schema closed.
2. **`rules/architecture.json` ↔ ArchUnit test-code coupling.** The json is explicitly a *human-readable contract*, not the enforcement point — the real rules are in `test/*.Architecture.Tests`. Auto-rendering `rules/architecture.json` from `gates.architecture.rootNamespace` is only half the job; verify how much of the **test project** must change in lockstep and whether a generated stub can actually compile/pass, or whether this stays partly manual.
3. **`Directory.Build.props` parameterization surface.** I treated `framework`/`treatWarningsAsErrors` as config and the rest (analyzer modes, audit levels) as opinionated-fixed. Confirm the **full** props file (`:1-26`) to decide which additional properties (`EnforceCodeStyleInBuild`, `AnalysisLevel`, `NuGetAuditLevel`) deserve config fields vs. stay fixed.
4. **The four `profiles/<tier>/profile.json` files as the tier source of truth.** I verified `ResolveInstallProfile` and the gate-map shape, but not each tier file's *full* contents (especially `dotnet-cli-heurex` and `dotnet-lib`, which appear in code paths but weren't fully dumped). Confirm they exist on disk under both `.doti/profiles` and `.doti/core` and that selecting a tier is purely a manifest write (no other file diverges per tier).
5. **`hx.config.template.json` (the committed portable fallback).** Referenced as living at `tools/Hx.Scaffold.Cli/Hx.Scaffold.Cli.csproj:36-49` with `localReleaseOutput.enabled=true` and no directory. I did not open it. Verify its exact shape so the wizard's "scaffold a starter `hx.config.json`" output matches the sanctioned template (and that it's truly gitignored for a generated repo).
6. **`ci.yml` and `dco.yml` contents.** Only `release.yml` line ranges were cited in depth. Before auto-emitting all three workflows (#24), read `ci.yml` and `dco.yml` in full to confirm what they parameterize (e.g. whether `ci.yml`'s push-to-main rebuild is droppable per MEMORY, and `dco.yml`'s exemption rules) so the generated copies are correct, not stale.
7. **`hx new --interactive` vs the Operator-Question Protocol.** The §1 constitution mandates the single-sourced Operator-Question Protocol for blocking questions (`constitution-template.md:18`). Verify the wizard's prompt format is allowed to be a lighter setup flow (it asks *configuration*, not *blocking decisions*) or whether it must route through `OperatorQuestionValidator` / `doti question check` — this affects how prompts are rendered and validated.
8. **Cross-CLI reach.** I verified flags on `Hx.Scaffold.Cli` (`hx new`) and `Hx.Runner.Cli` (`hx doti install`) separately. Confirm which binary a *new project's operator* actually invokes for each path (the scaffold CLI requires an executable-adjacent `hx.config.json` via `WithRequiredConfiguration`, `ScaffoldCommandFactory.cs:226-233`; the runner does not), so `--config`/`--interactive` are wired onto the right command in the right assembly.

---

## 8. Effective-config display with provenance (operator requirement R2)

*Added after synthesis, at operator request.* In **interactive mode and after any setup run**, `hx` must display every configured item with its provenance — **default vs custom** — renderable as **JSON** (agent) or a **human table**. Same two-audience split as the input paths (R1).

**Design implication — provenance-tracked resolution.** The config layer cannot collapse to a flat resolved blob; resolution must yield an object that records, per key:

- `value` — the effective value,
- `source` — one of `default | config-file | interactive | flag | derived`,
- `default` — the value it would take absent any input (so *custom* ⇔ `source != default`).

This means the generator resolves in two stages: (1) layer the inputs (flags ▸ `--config`/interactive ▸ derived ▸ defaults — the precedence from §6), recording each key's winning source; (2) project the resolved-with-provenance object into the target files. The provenance is a first-class part of the model, not reconstructed after the fact.

**One surface, two renders — `hx doti config show [--json]`:**

- **JSON** (agent): the full resolved object, `{ key: { value, source, default } }` per field, grouped as in §4.
- **Human**: the §2 grouped tables plus a provenance column — e.g. `· default` vs `✓ custom (interactive)` / `✓ custom (config-file)` — so the operator sees at a glance what they changed vs inherited. A footer count (`N custom · M default`) mirrors the §1 summary style.

**Where it is shown:**

1. **Automatically at the end of interactive setup** — a final **Group 9 — Review** step: render the effective config (human table) and require confirmation *before* hx writes files / makes the baseline commit. The operator can jump back to any group to change a value, which flips that key's `source` to `interactive`.
2. **Standalone, anytime** — `hx doti config show` re-derives the effective config from the persisted `.doti/setup.json` (the recorded intent) and, with `--live`, diffs it against the actual on-disk repo files (so it doubles as a drift check: a field whose repo file was hand-edited away from `setup.json` shows as `drifted`). *(Resolve in spec: persisted-only vs `--live` diff; recommend both, persisted as the default.)*

**1:1 with the input paths.** Every `--config` field and every interactive question is the *same key* the summary echoes back with its source — so the post-setup display is a faithful, auditable mirror of exactly what was set vs defaulted. This closes the loop the executive summary opens: there is now a single place to *see* the entire configured surface, not three disconnected ones.


---

## 9. Scoping decision — the operator-configurable subset (operator-confirmed)

*Refinement after review. Narrows the §2 universe (48) to what a project owner actually sets; everything else is derived, auto-set by code, payload-fixed, or GitHub/nuget-side.*

**Sentrux is NOT operator config.** The scaffold first-smoke run sets the baseline automatically (`FirstSmokeRunner.cs:65-68` → `SentruxBaselineRunner.Save`); the gate never creates one (`SentruxChecker.Execution.cs:42` only advises). Layers/architecture namespaces auto-render from the project name/layout; thresholds (`max_fn_lines` 120 / `max_cc` 25 / `max_cycles` 0) are payload defaults. **No operator input required for Sentrux.**

**The genuine operator-configurable set:**

| Knob | Default | Target | Class |
|------|---------|--------|-------|
| name | — (given) | sourceName everywhere | required-at-scaffold |
| output dir | — (given) | `dotnet new -o` (`hx new` only) | required-at-scaffold |
| company | `Heurex` | Company/Authors/Copyright/PackageId prefix | preference |
| description | sample boilerplate | `<Description>` | preference |
| repositoryUrl | absent today | RepositoryUrl/PackageProjectUrl | preference |
| license | `MIT` | PackageLicenseExpression | preference |
| authors | = company | `<Authors>` | preference |
| version seed | `0.1.0` | `GitVersion.yml next-version` | preference |
| agents | `codex,claude` | rendered skill trees | preference |
| release local dir | unset | `DOTI_RELEASE_ROOT` / `hx.config.json` | **machine-local (not committed)** |
| publish → nuget {owner,repo,workflow,environment} | `none` | generated `release.yml` params | publish-intent (execution operator-manual) |
| constitution §2 (domain/techStack/codingStyle/security/performance) | placeholders | `.doti/memory/constitution.md` | authored content |

**Explicitly NOT operator config:**
- **Derived** from name+company+layout: Copyright, PackageId, RootNamespace, ToolCommandName, the five `release.json` names, Sentrux layers, architecture namespaces.
- **Auto-done by hx** (on/off defaults, not values): Sentrux baseline (smoke), baseline commit, dev/main branches, `.github/{release,ci,dco}.yml` emission, DCO prepare-commit-msg hook.
- **Payload-fixed**: profile/tier gate maps, `prerequisites.json`, all `schemaVersion`s, the §1 inherited invariants.
- **Operator-only, GitHub/nuget-side** (documented as intent, executed by the operator): `main` branch protection (`required_linear_history` OFF), the NuGet Trusted-Publishing OIDC policy, the `NUGET_USER` secret + `production` Environment, the `v*` tag push.

**Documentation requirement (operator-confirmed).** Independent of the `--config`/wizard feature, the README must carry a **"Configuration reference"** section documenting the full universe (this doc's §2), with the operator subset above called out as "what you set" and the remainder grouped as derived / auto / payload-fixed / operator-manual — so the config surface is on the record, not folklore.
