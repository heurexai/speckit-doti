# Arch Review — 029-scaffold-setup-config

**Stage:** /04-doti-arch-review · **Verdict:** the 2 BLOCKERs + 7 HIGHs are **resolved in the revised plan** (`docs/plans/029-scaffold-setup-config-plan.md`); no open BLOCKER → `/07-implement` unblocked.

**Triage:** a CODE/tooling change (new `Hx.Tooling.Contracts` types, a projector, two CLI surfaces, `Hx.Doti.Core` writers). Code lenses run; no `scaffold/templates/**` generated-code change, no Doti-prose-only path. Run via an 8-lens clean-context sub-agent panel against the spec + plan + current code. 7/8 lenses returned (the **testability** lens hit the structured-output cap — covered manually below). 24 findings; all evidence verified at `file:line`.

## Findings & resolutions

### BLOCKER

- **B1 (modularity) — Single-source projector+table in `Hx.Scaffold.Core` is unreachable from the install path (forbidden edge / `max_cycles=0` violation).** `Scaffold.Core → Doti.Core` already exists, so a `Doti.Core → Scaffold.Core` call to reuse the projector cycles. **→ Resolved D3:** projection **orchestration + table move to `Hx.Tooling.Contracts`**; targets are **injected `ISetupTargetWriter`s**; `.doti`-asset writers home in `Hx.Doti.Core`. No cycle.
- **B2 (edge-case) — SC-006 "no partial repo" unsatisfiable for failures after `dotnet new` (projection writes into the live `OutputPath`, no rollback).** **→ Resolved D5:** ALL validation runs in the CLI **before** `ScaffoldRequest`/`TemplateGenerator`, so an *invalid config* never starts generation (SC-006 holds as written). Atomicity is scoped to validation; a non-validation projection failure follows the *existing* finish-phase model (`ToolVendor`/`DotiInstall` already throw on a partial repo) — not a spec relaxation, a precise reading of SC-006.

### HIGH

- **H1 (fit) — install-side projection unbuildable (writers in Scaffold.Core, unreachable from Doti.Core/Runner.Cli).** → D3 (writers re-homed to Doti.Core).
- **H2 (modularity) — constitution §2 verbatim projection needs a Doti.Core writer no Scaffold.Core projector can own.** → D3: new `ConstitutionSection2Writer` in Doti.Core (beside `ConstitutionInitializer`, reusing `ConstitutionService.Section2Anchor`, write-once-when-placeholder).
- **H3 (edge-case) — no path-traversal containment for `--config` path / `identity.output`.** → D5+D9: reuse the `ValidatePublishProject` discipline (reject `..`, `GetFullPath`+containment) before generation.
- **H4 (simpler) — D3 projector-in-Scaffold.Core requires a forbidden reverse edge.** → D3 (same resolution as B1).
- **H5 (blast-radius) — install has no plumbing seam; `ScaffoldRequest.Setup` never reaches `hx doti install`.** → D8: add additive `DotiInstallBootstrapRequest.Setup`; both records carry the config.
- **H6 (security) — operator values projected into `.csproj` XML with no encoding (MSBuild-injection / build-corruption).** → D9: XML-encode every projected value (`SecurityElement.Escape`/`XElement`) + reject XML-metachar/control chars in free-text at validation.
- **H7 (security) — no-partial-repo hinges on validating BEFORE `TemplateGenerator`, but the plan validated only before projection.** → D5 (validate in the CLI before the request is built).

### MEDIUM (resolved in the revised plan)

- M1 config-show store/formatter in Scaffold.Core forces a `Runner.Cli→Scaffold.Core` edge → **D7** (store+formatter in Doti.Core). · M2 §2 ownership split across two cores → **D3** (single Doti.Core writer). · M3 wizard CLI IO-surface sprawl → **D4** (dumb iterator over pure prompt-definitions; `EnabledWhen` is data). · M4 SemVer/SPDX under-specified → **D5** (3-part numeric SemVer core + SPDX charset, one shared parser). · M5 `config show` with no `.doti/setup.json` undefined → **D7** (render all-default, not error). · M6 `ScaffoldRequest.Setup` is hx-new-only → **D8** (both records). · M7 SC-007 byte-identical only fenced for `hx new` → **D10** (no-op fence + regression for install). · M8 tracked `.doti/setup.json` admits a machine-local absolute `localOutput.directory` → **D6** (machine-local fields never written to the tracked file).

### LOW (resolved/noted)

- L1 wizard CLI type must avoid forbidden `cliSurfaceConfinement` suffixes → **D4** (neutral name, delegates). · L2 grouping re-derived in 3 renders → **D1** (`Group` tag on the key model). · L3 §2 write-once-skip silently drops config §2 → **D6** (preserved/ignored effect). · L4 `ScaffoldProof.Request.Setup` is an additive JSON field → **D8** (noted; SC-007 JSON shape asserted). · L5 §2 free-prose is a stored prompt-injection carrier → **D9** (anchor-integrity guard + documented trust assumption). · (data-contract lens returned one LOW, folded into D1's additive-evolution + `Group` tag.)

## Testability lens (manual — the panel agent hit the output cap)

Verified against the revised plan: the resolver/schema/projection-table are pure and unit-testable git-free (Contracts is IO-free, confirmed). The one real seam is the **wizard console IO** — resolved by **D4's injectable `IConsole`/reader**, so SC-004 (`--interactive` == `--config`) runs on scripted input with no TTY. SC-001..SC-008 each map to a concrete deterministic test in the plan's Test strategy, plus the golden `setup.json`/`config show` fixture and the `SetupConfigDefaults == docs/configuration.md` parity assertion. No testability BLOCKER.

## Conclusion

The design's foundation was sound (pure model in Contracts; additive `Setup` field; fail-closed schema), but the **mutating-unit placement** would not build (a cycle/edge inversion) and the **validation ordering** under-delivered SC-006. The revised plan re-homes orchestration to Contracts (injected writers) and the `.doti`-asset writers to Doti.Core (zero new edges, zero cycle), moves validation ahead of generation, and adds the encode/containment/no-op-fence guards. All BLOCKERs closed in design; `/07-implement` proceeds against the revised plan.
