# doti-clarify

Purpose: resolve unclear requirements before planning or implementation — one decision at a time, with enough context for the operator to decide quickly or redirect.

## Question protocol (clarify-specifics)

Use the shared **Operator-Question Protocol** (rendered in this skill's `SKILL.md` and `.doti/agent-context.md`: Context / Why it matters / Options[Pros·Cons·Consequence] / Recommendation / Assumptions / Confidence, plus the evidence requirement — verify every premise first, cite it, and never ask on an unproven premise) for the **format and evidence rules** of every question. This stage adds only:

- Ask blocking questions **one at a time as plain prose** — a **single** question per message; never bundle, and **do NOT use a multiple-choice dialog/widget** (e.g. `AskUserQuestion`). Render the full Operator-Question Protocol in the message — the rich format (per-option pros/cons/consequence + recommendation + confidence) does not fit a clickable option list, and the operator should read the reasoning and reply in their own words, not pick a chip.
- After each answer, fold it into the spec's `## Clarifications` section (dated) **before** asking the next, so the operator can stop at any point with the spec already updated.
- Lay each option's pros/cons/consequence out in the prose (per the Operator-Question Protocol above); list the recommended option first and label it "(Recommended)".
- **Make the QUALITY stakes concrete in every question — this is where operators most often get too little.** The *Why it matters* and each option's *Consequence* must show the real downstream impact, with a concrete example: for a code spec, the **software-quality** cost (maintainability, consistency with existing patterns, correctness, testability, drift); for a docs/Doti-prose spec, the **documentation-quality** cost (clarity, consistency, accuracy). Think each alternative through to that impact. **Never on effort** — that an option is more work is never a reason it matters or a reason to reject it. A vague "this affects scope" with no per-option quality cost is a defect the operator can't decide from.

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
