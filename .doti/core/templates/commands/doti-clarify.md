# doti-clarify

Purpose: resolve unclear requirements before planning or implementation — one decision at a time, with enough context for the operator to decide quickly or redirect.

## Question protocol (clarify-specifics)

Use the shared **Operator-Question Protocol** (rendered in this skill's `SKILL.md` and `.doti/agent-context.md`: Context / Why it matters / Options[Pros·Cons·Consequence] / Recommendation / Assumptions / Confidence, plus the evidence requirement — verify every premise first, cite it, and never ask on an unproven premise) for the **format and evidence rules** of every question. This stage adds only:

- Ask blocking questions **one at a time** via the `AskUserQuestion` tool — a **single** question per call; never bundle.
- After each answer, fold it into the spec's `## Clarifications` section (dated) **before** asking the next, so the operator can stop at any point with the spec already updated.
- Put concise per-option pros/cons/consequence into each `AskUserQuestion` choice description; list the recommended option first and label it "(Recommended)".

## Rules

1. Read `.doti/agent-context.md` and the active spec first.
2. Ask only blocking questions (at most 7 total; if more than 7 genuinely block, the feature is likely too large — say so and recommend splitting rather than running a long interrogation). For anything non-blocking, make a conservative assumption **aligned with the spec intent + agent context** (the plan does not exist yet — that is `/03`'s job; the assumption is verified or revised there) and record it instead of asking.
3. Separate product intent from implementation mechanism.
4. State whether each affected check is command-backed or advisory; never imply a planned command is implemented.
5. After each answer, write it into the spec `## Clarifications` (dated) and reflect any scope change before the next question.
6. The operator may stop at any question and revise instructions; honor that immediately and do not force the remaining questions.
7. Apply the shared Operator-Question Protocol's **evidence requirement** to every question, option, recommendation, and assumption. If you cannot back a premise with evidence, verify it first or do not ask it.

Expected output: each clarified decision recorded in the spec's `## Clarifications`, with any unresolved questions explicitly listed if the operator stops early.

## Next

Run `/03-doti-plan` to author the implementation plan.
