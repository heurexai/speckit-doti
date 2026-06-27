# 010 — Single-Source the Constitution

## Goal

Remove the vestigial `.doti/core/memory/constitution.md` twin so the constitution is single-sourced at `.doti/memory/constitution.md` — the one path the tooling reads (`ConstitutionService.RelativePath`) and where every doti-installed repo's constitution lives. The twin is a leftover from the pre-009 reconcile model: after 009 made the constitution operator-authored and excluded it from the shipped payload, the `core/memory` copy no longer ships, is read by no code, and is not a render source — it is pure redundancy.

## Scope

In: delete `.doti/core/memory/constitution.md`; drop the now-redundant `.doti/core/memory/constitution.md` segment from the `_HxDoti` payload-exclusion glob (the file no longer exists to exclude); add a regression test pinning the single-source invariant; CHANGELOG + README note. Out: no behavior change to `ConstitutionService`/`ConstitutionInitializer`/the template/this repo's actual constitution content; `.doti/memory/constitution.md` is untouched.

## Functional Requirements

- `FR-001`: This repo's constitution MUST be single-sourced at `.doti/memory/constitution.md`; `.doti/core/memory/constitution.md` MUST NOT exist. The active constitution content is unchanged.
- `FR-002`: The payload-exclusion glob MUST NOT carry a stale exclusion for the removed `.doti/core/memory/constitution.md`; `.doti/memory/constitution.md` stays excluded (still must not ship). `doti payload check` + `render-skills --check` stay clean.

## Success Criteria

- `SC-001`: `.doti/core/memory/constitution.md` does not exist; `.doti/memory/constitution.md` exists and is byte-identical to its pre-010 content; `hx doti constitution` still emits the §2.
- `SC-002`: `gate run --profile normal` is green; payload parity passes with the twin removed; a `hx new`-generated repo is unaffected (it never had the twin).

## Key Entities

- **Constitution** — unchanged single artifact at `.doti/memory/constitution.md` (009 Key Entities); this cycle only removes the redundant source-layer copy.

## Architecture Impact

- Doti asset removal + a one-segment csproj glob edit + one test. No `*.Core` logic change, no contract change, no rule-family change.

## Assumptions

- Verified: no code reads `.doti/core/memory/constitution.md` (grep); the two files are byte-identical; the parity checker tolerates an empty `.doti/core/memory/` (it iterates the source dir, which becomes empty).

## Acceptance

- Command-backed: `gate run`, `doti payload check`, `doti render-skills --check`, `hx doti constitution` — all exist and gate this change.
