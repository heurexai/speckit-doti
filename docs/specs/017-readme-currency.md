# 017 — README currency

## Goal

Bring `README.md` current with what `main` actually ships. The README is accurate through its skill docs + the per-cycle list (updated to 016), but two spots lag:

1. **The "Proofs, gates, and recovery" intro (line ~165) still says "the main branch now includes the 007–011 work"** — main now carries 012–016: gate & affected-test visibility (`gate run --stream`), the `/doti-auto` hands-off cycle driver, the ArchUnit/Sentrux structural-offender detail, Sentrux's production-only scope, and cross-platform tool provisioning (per-RID fetch + exec bit).
2. **The CLI-map descriptions for `gate run` and `sentrux verify/check` don't mention** the live `--stream` trace (012) or the offender detail (014) — a reader of the command table sees a pre-012 description.

A docs-only currency pass so the README does not under-describe shipped capability.

## User Scenarios & Testing

**Priority Mode** — docs change: truth-first (accuracy of what ships). README must describe only implemented behavior, as implemented.

### Work Item 1 — README describes the 012–016 capabilities (Priority: P1)

A reader of the README's capability sections sees the gate-visibility, doti-auto, structural-offender, and cross-platform-provisioning work that `main` ships.

- **Why this priority:** the README is the front door; an under-stated capability list misrepresents the product to a reader who never opens the CHANGELOG.
- **Independent Test:** read the "Proofs, gates, and recovery" intro + the CLI map and confirm they reference the 012–016 work (`gate run --stream`/trace, the structural-offender detail) rather than stopping at 007–011.
- **Acceptance Scenarios:**
  1. **Given** the README, **When** a reader reads the proofs/gates intro, **Then** it states main includes the 007–016 work and names the gate-visibility, doti-auto, and structural-offender capabilities.
  2. **Given** the CLI map, **When** a reader reads the `gate run` + `sentrux` rows, **Then** they mention the `--stream` live trace and the offender detail surfaced on a structural failure.

### Edge Cases

- The README stays an overview — it MUST NOT duplicate the rendered skill bodies or the CHANGELOG; it describes capability, links the detail.
- Only implemented behavior is described as implemented (no planned-as-shipped claims).

## Scope

Included: update `README.md` — the proofs/gates intro line + the `gate run`/`sentrux` CLI-map rows — to reflect 012–016.

Excluded: no code, no skill/template change, no other doc. No new capability — purely describing what ships.

## Functional Requirements

- `FR-001`: `README.md`'s "Proofs, gates, and recovery" intro MUST reference the 007–**016** work and name the gate & affected-test visibility (`gate run --stream`), the `/doti-auto` driver, and the ArchUnit/Sentrux structural-offender detail. `[WI1]`
- `FR-002`: The CLI-map `gate run` and `sentrux verify/check` rows MUST mention the live `--stream` trace and the structural-offender detail, respectively. `[WI1]`
- `FR-003`: The README MUST remain an overview that describes only implemented behavior (no duplicated skill bodies, no planned-as-shipped claims). `[WI1]`

## Success Criteria

- `SC-001`: The proofs/gates intro + CLI map describe the 012–016 capabilities; a reader is not left with a pre-012 picture.
- `SC-002`: No code/skill/template/proof change; `gate run` stays green over a docs-only change set.

## Key Entities

- **README capability sections** — the "Proofs, gates, and recovery" intro + the CLI map rows that describe gate/Sentrux behavior.

## Deterministic Surfaces

- `README.md` — the human overview (the only file this feature edits).

## Architecture Impact

- Docs-only. No `*.Core` code, no contract, no rule/Sentrux/ArchUnit/proof surface, no skill/template source.

## Sentrux And Hygiene Impact

- None — `README.md` is `*.md` (Sentrux source-excluded). Prose currency only.

## Assumptions

- The 012–016 capabilities are shipped on `main` (verified — released locally as v0.12.0–v0.12.2; the CHANGELOG + per-cycle list already document them).

## Acceptance

- Command-backed: `gate run` (docs-only lane), `doti render-skills --check`, `doti payload check` (unaffected — README is not a rendered asset).
