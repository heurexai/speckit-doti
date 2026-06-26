# Tier composition — deferred beyond 007

> Decision record for **T008 / FR-031** (feature 007). Records what 007 ships for tier resolution and why the
> layered composition algebra is deliberately deferred.

## Decision

**007 ships the minimal tier resolution: a flat `gates:{ "<step>": "enforced" | "advisory" | "skip" }` map per
tier.** The layered composition stack (overrides → profile → core with prepend/append/wrap) that FR-031
originally contemplated is **deferred beyond 007.**

What is implemented (T006/T007, this cycle):

- A repo's declared **tier** (`.doti/integration.json` → `.doti/profiles/<name>/profile.json`) carries a flat
  `gates` map; `GateLadderResolver` reads it; `GateRunner` applies the mode to each opinionated step; an
  undeclared step defaults to **Enforced** (today's behavior). (`tools/Hx.Cycle.Core/GateLadder.cs`)
- Three concrete curated tier files — `workflow-only` (T1), `dotnet-lib` (T2), `dotnet-cli-heurex` (T3) — each
  a hand-authored `profile.json`. No merge, no layering.
- Bypass-safety + downgrade refusal (FR-030/FR-029) ride on this flat model.

## Why composition is deferred

The arch-review's *simpler-alternative* lens flagged the composition algebra as **premature generality**:

- 007 ships exactly **three fixed tiers**. A three-element set does not need a merge algebra — three
  hand-authored files are clearer and have no edge cases.
- Composition (prepend/append/wrap over a layer stack) is the kind of generality you add when **external
  authors extend your tiers** without copy-paste. 007 has **no second consumer** for that — there is no fourth
  profile, and no caller asks to compose.
- Building it now is infrastructure with one (synthetic) user, which the plan's own Complexity-Tracking
  discipline argues against.

## Future trigger (when to build it)

Add the layered composition stack only when a **real fourth profile must extend a third without copy-paste** —
i.e. a concrete governance layer needs to ride a base tier and override a few gates rather than restate the
whole ladder. At that point: introduce the overrides → profile → core resolution with prepend/append/wrap,
justified through the plan's **Complexity Tracking** (the same bar 007 applied to the Living-Spec relaxation),
and migrate the three flat tiers onto it. The upstream reference for the shape is Spec Kit's `presets/`
(`presets/README.md`); doti's version must remain the *enforced* twin (the gate reads the resolved ladder),
not honor-system prose.

## Status

FR-031 is satisfied in its minimal form for 007 (flat `gates` map + three concrete tiers). Composition is
recorded here as deferred, not dropped.
