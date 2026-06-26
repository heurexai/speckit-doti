# Context-budget engineering for large features

> Concept record for **T035 / FR-036** (feature 007). How `implement` survives features too large for one context
> window — by riding the enforcement substrate (ordered-task completion, T003/FR-028) instead of relying on a single
> heroic run that holds the whole feature in one context.

## The problem

A non-trivial feature has more tasks than fit usefully in one agent context. A single run that tries to hold the
entire feature loses fidelity as the window fills — earlier decisions get summarized away, later edits drift from the
plan, and a mid-run interruption loses everything. So `implement` is engineered to be **scoped**, **resumable**, and
**parallelizable**, with the task ledger + its completion markers as the durable state — not the agent's context.

## 1. Scoped, resumable implement — the per-task loop

Work the **lowest-numbered unchecked task**, one at a time. Each task is a closed stretch:

implement → verify (command-backed build + the gate's affected tests) → check the box in the tasks ledger →
`doti cycle stamp` / `doti task-hash stamp` (the diff-bound, non-forgeable completion marker) → commit.

The completion marker is the durable state. A **fresh context resumes at the next unchecked task with zero handoff
prose** — the ledger and the per-task hashes *are* the memory; nothing about the previous run needs to be re-read.
The ordered-task gate (T003) refuses out-of-order completion, so a resumed run cannot accidentally skip, reorder, or
double-complete. This is why one feature can span many contexts (and many context compactions) without drift: each
stretch is small enough to do at full fidelity, and the substrate guarantees the boundaries.

## 2. `[P]` parallel tasks → sub-agents

A task tagged `[P]` is parallelizable: it touches different files and does not depend on another unchecked task in the
same phase (see the tasks template). Within a phase, `[P]` tasks may be **delegated to parallel sub-agents**, each in a
clean context, and reconciled when they return. The **phase boundary is the barrier**: the gate runs after a phase's
last task lands, so parallel work inside a phase is safe but a later phase never starts on unverified earlier work.
This trades the single context's serial budget for N clean sub-contexts without losing the ordering guarantee.

## 3. Spec of specs — decomposition

A feature too large even for the scoped loop is **decomposed into sub-features**, each its own spec → cycle with its
own proof, composed by a top-level "spec of specs". Each sub-feature is independently testable (Independent Test /
MVP-first phases) and ships its own gate-green release train; the parent spec tracks only the composition and the
cross-cutting contracts. This keeps every individual cycle inside one context budget while a large initiative spans
several.

## Why this rides the substrate (the teeth make it safe)

The context-budget methodology itself is shared (Spec Kit) — scope small, parallelize the independent, decompose the
huge. What makes it **safe under doti** is the enforcement layer beneath it: the completion markers are diff-bound and
non-forgeable, the order is machine-enforced (T003), and the gate is fail-closed. So "resume in a fresh context" and
"delegate to a sub-agent" cannot quietly forge completion, skip a gate, or land work out of order — the failure modes
that make naive multi-context implementation untrustworthy are exactly the ones the substrate closes.
