# doti-plan

Purpose: produce a narrow but rigorous implementation plan — resolve unknowns, decide the design (including the architecture rule deltas), and prove the plan honors the constitution before any code is written.

## Behavior

1. Read `.doti/agent-context.md`, the active spec, and relevant source assets. Map the work to the self-hosting maturity.
2. **Technical Context.** State the approach, stack, dependencies, and constraints relevant to scaffold-dotnet. Mark anything undecided as `[NEEDS CLARIFICATION]` — do not plan on an unproven premise; verify it or resolve it in research first.
3. **Constitution Check (gate — before design).** Check the intended approach against `.doti/memory/constitution.md` (Deterministic Ownership, Bootstrap Honesty, Template Boundary, Public Hygiene, Cross-Platform, Codified Cycle, Engineering Discipline). If the plan would bend a principle, STOP and either redesign or record it in Complexity Tracking with a justification. Do not proceed past an unjustified violation.
4. **Research (resolve unknowns).** For each `[NEEDS CLARIFICATION]`, dependency, or integration, decide it and record **Decision / Rationale / Alternatives rejected**. The plan must carry no unresolved unknowns into tasks.
5. **Design.** Describe the technical approach and name the files likely to change. State the **architecture delta** the design requires — which projects/namespaces/layers it adds or moves, and the exact rule changes that encode it: the affected ArchUnitNET family in `rules/architecture.json` and the Sentrux layer/boundary in `.sentrux/rules.toml`, kept mutually consistent. A design that changes structure but not the codified rules will drift — name the rule change here so the gate can hold it. (`/06-doti-arch-review` validates the two engines encode the same intent; do not re-derive that here, just specify the delta.) **CLI surface & error contract:** if the design adds or changes a command, declare its emitted error codes (→ `errorcodes/registry.json`), its exit class, its `describe` entry, and `CliResult` envelope conformance in the plan's *CLI surface & error contract* section — so the command is self-describing by design; omit when the feature adds no CLI surface.
6. **Deterministic surfaces + command availability.** Identify the planned `.NET` commands the behavior relies on and whether they exist; mark gaps advisory; never downgrade a planned gate.
7. **Constitution re-check (gate — after design).** Re-evaluate the drafted design against the principles; a design that introduced a violation must be fixed or justified.
8. **Complexity Tracking.** For each justified deviation, record: *Violation | Why needed | Simpler alternative rejected because*.

Expected output: a plan with Technical Context, both Constitution-Check verdicts, resolved research decisions (Decision/Rationale/Alternatives), a design that names its architecture rule deltas, command availability, and any Complexity-Tracking justifications.

## Next

Run `/04-doti-tasks` to break the plan into executable tasks.
