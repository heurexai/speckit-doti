# 010 — Single-Source the Constitution — Plan

## Summary

Remove the redundant `.doti/core/memory/constitution.md` twin; keep `.doti/memory/constitution.md` as the single source. A Doti-asset removal + a one-segment csproj glob edit + one regression test. The simplest correct change: delete the file, drop its stale exclusion segment, pin the invariant with a test.

## Constitution Check (gate)

Verdict against `.doti/memory/constitution.md` §1/§2 (fetched via `hx doti constitution`): **PASS**. Public Hygiene + Template Boundary favour single-sourcing (no duplicate tracked copy); Deterministic Ownership/Channel Independence untouched (no `*.Core` change); the constitution content (§2) is unchanged, so no §2 convention is bent.

## Research (resolve unknowns)

- **Decision:** delete `.doti/core/memory/constitution.md` (not `.doti/memory/`). **Rationale:** the tooling reads `.doti/memory/constitution.md` (`ConstitutionService.RelativePath`); installed repos only ever have that path; the `core/memory` copy is non-shipped (009 payload exclusion), read by no code (verified by grep), and not a render source. **Alternatives rejected:** keep both in sync (the status quo redundancy); make `core/memory` canonical (would diverge this repo from every installed repo and require pointing the tooling at a non-standard path).

## Design

- Delete `.doti/core/memory/constitution.md`.
- `Hx.Scaffold.Cli.csproj` `_HxDoti` `Exclude`: drop the `;$(RepoRoot).doti/core/memory/constitution.md` segment (the file is gone; the glob can't include it). Keep the `.doti/memory/constitution.md` exclusion (that file stays and must not ship).
- A regression test asserting `.doti/core/memory/constitution.md` does NOT exist and the single source at `.doti/memory/constitution.md` does — `test/Hx.Doti.Tests/ConstitutionTests.cs`.
- **Architecture delta:** none — no project/namespace/layer change, no ArchUnit family change, no Sentrux boundary change. Pure asset removal + glob hygiene.

## Command Availability

| Area | Command | Status |
| --- | --- | --- |
| Payload parity | `doti payload check`, `doti render-skills --check` | implemented — gate the removal |
| Gate | `hx gate run --profile normal` | implemented |

No planned gate downgraded.

## Risks

- The parity checker iterates the source's `.doti/core/memory/**`; with the file gone the dir is empty → nothing to compare (verified-safe). The install reconcile of `core/memory` similarly copies nothing. Low risk; `payload check` confirms.
