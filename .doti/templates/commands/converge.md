# converge

Purpose: brownfield/drift reconciliation — assess the codebase against the spec/plan/tasks and append the remaining unbuilt work as new tasks, so the ledger catches up with reality (the codebase and the spec converge).

Command-backed behavior:

1. Read `.doti/agent-context.md`.
2. Find the requirement coverage gap deterministically: `hx doti converge --spec docs/specs/<NNN-slug>.md --tasks docs/tasks/<NNN-slug>-tasks.md`. It reports every `FR-###`/`SC-###` the spec defines that NO task covers (via a `covers …` marker) — the candidate unbuilt work. It fails closed (`converge-input`) if the spec or tasks file is missing.
3. For each uncovered requirement, ASSESS it against the codebase: is it genuinely unbuilt, partially built, or already satisfied by code that no task ever claimed? Read/reproduce to decide — do not assume; an FR with no task may still be implemented.
4. Append the genuinely-missing work as new tasks in the right phase of the tasks ledger, mapping each to the requirement it covers (`[covers FR-/SC-]`). Respect the tasks' existing phase order (the spec's declared Priority Mode — MVP-first for code, truth-first for docs, safety-first for workflow) and the ordered-task gate — this command never rewrites the ledger for you; you append, and the gate keeps the order honest.
5. This ties to drift-review: `converge` reconciles the spec→tasks gap; `/08-doti-drift-review` reconciles the source→installed-asset gap. Run converge when you suspect the tasks ledger has fallen behind the spec or the code.

Expected output: the coverage-gap report (the `CliResult` data: spec / covered / uncovered requirements) plus the new tasks you appended for the genuinely-unbuilt work.

## Next

Continue with `/07-doti-implement` to build the newly-appended tasks, or `/08-doti-drift-review` to reconcile installed assets.
