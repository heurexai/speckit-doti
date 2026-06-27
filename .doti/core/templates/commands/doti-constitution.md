# doti-constitution

Purpose: author or amend the project **constitution** — the **§2 project declarations** (domain, tech stack, coding style, security, performance) that `/03-doti-plan` and `/06-doti-arch-review` re-inject and evaluate against. The constitution is a **project-level artifact**, authored once and amended occasionally — NOT a per-feature cycle stamp, NOT a numbered `/0N` stage. Runs anytime, outside the numbered cycle.

## Behavior

1. Read `.doti/agent-context.md`, then the current constitution via `hx doti constitution --section full --repo .` — it reports absence with an actionable note (surface-and-proceed; never blocks).
2. **If none exists,** initialize from `.doti/core/templates/constitution-template.md` (the §1/§2 template). The title carries the project name **auto-derived** by the scaffold/solution (FR-015) — `hx new` and `doti install` already fill it; never invent or hand-place a name placeholder.
3. **Edit §2 ONLY** — the five operator-authored sections: *Domain principles*, *Tech stack* (beyond the .NET 10 baseline), *Coding style*, *Security & compliance*, *Performance*. Replace each `[PLACEHOLDER]` with the project's real conventions, in the project's own words.
4. **Never touch §1** (Inherited doti invariants). §1 is codified — deterministic gates, library-first/pure-core, thin CLI + `CliResult`, GitVersion versioning, Sentrux complexity, cross-platform, hygiene/SAST, the codified cycle, engineering discipline, operator decisions. Do NOT re-declare, weaken, or add a placeholder for any §1 item — in particular no versioning policy (GitVersion owns it), no CLI/output-shape declaration (the scaffold + Channel Independence own it), and no quality-gate/workflow rules (the codified cycle owns them).
5. Write the result to `.doti/memory/constitution.md`. Amendments are tracked by the **doti cycle + git history** — do NOT add a SemVer doc-version line or a Sync Impact Report (doti codifies versioning).
6. Verify **zero** unfilled `[PLACEHOLDER]` tokens remain in §2 and the title carries the project name. Confirm with `hx doti constitution --repo .` that the §2 the stages will read is what you intended.

Expected output: an authored/amended `.doti/memory/constitution.md` with a real, placeholder-free §2.

## Next

Resume your active cycle stage; the next `/03-doti-plan` and `/06-doti-arch-review` evaluate against the fresh §2 automatically.
