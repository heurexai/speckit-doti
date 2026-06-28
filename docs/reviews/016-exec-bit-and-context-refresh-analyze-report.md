# Analyze Report — 016 Tool-fetch executable bit + agent-context refresh

**Stage:** `/05-doti-analyze`. Consistency across [spec](../specs/016-exec-bit-and-context-refresh.md) ↔ [plan](../plans/016-exec-bit-and-context-refresh-plan.md) ↔ [tasks](../tasks/016-exec-bit-and-context-refresh-tasks.md).

## Coverage

| Requirement | Tasks | Status |
|---|---|---|
| FR-001 (fetch path sets exec bit) | T001, T002 | covered |
| FR-002 (store path sets exec bit) | T002 | covered |
| FR-003 (Windows-guarded; executable only) | T001, T002 | covered |
| FR-004 (agent-context template describes latest) | T003 | covered |
| FR-005 (entrypoint note refreshed from source; re-render) | T004, T005 | covered |
| SC-001 (Linux binary runs; CI smoke passes) | T002, T006 | covered |
| SC-002 (Windows unchanged) | T001 | covered |
| SC-003 (context describes doti-auto/offenders/Sentrux scope) | T003, T004 | covered |
| SC-004/005 (parity clean; no proof/rule change) | T005, T006 | covered |

No FR/SC orphaned; no task without a requirement.

## Consistency checks

- **Spec ↔ plan ↔ tasks:** WI-1 (exec bit) → the shared `EnsureExecutable` helper at the two write points (T002) + test (T001); WI-2 (context) → edit the two prose sources (T003 template, T004 profile) then re-render + rebuild (T005). The plan's "edit the source, never the rendered output" matches FR-005 + T004's "do NOT hand-edit CLAUDE.md/AGENTS.md".
- **No proof/rule/hash change** anywhere — WI-1 sets only the file mode (bytes already hash-verified, untouched); WI-2 is prose. Consistent with SC-005.
- **Sources are correct:** `.doti/core/templates/agent-context-template.md` (→ `.doti/agent-context.md`) and `.doti/profiles/dotnet-cli/profile.json` `rootMaturityNote` (→ `CLAUDE.md`/`AGENTS.md`) — both verified as the render/static sources, not rendered files.

## Ambiguities / conflicts

- **None blocking.** The "Permission denied" → missing-exec-bit diagnosis is evidenced from the v0.12.1 CI log (3 tools fetched + hash-verified, then the binary failed to run). The only confirm-at-CI item (the Linux smoke pass) is observed on v0.12.2.

## Verdict

**Consistent and fully covered.** Proceed to `/06-doti-arch-review` (a small `*.Core` change + prose — the code lenses apply lightly).
