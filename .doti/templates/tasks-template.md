# Tasks Template

> Dependency-ordered, file-pathed, executable tasks. Each requirement (`FR-###` / `SC-###`) maps to at least one task so `/doti-analyze` can confirm coverage. Where the change includes tests, the test task precedes the implementation it covers.

## Tasks

Ordered so prerequisites come first; note tasks that can proceed independently.

- [ ] T001 — <concrete action> — `path/to/file` — [covers FR-00X]
- [ ] T002 — Update installed bootstrap files / re-render when the change touches doti assets
- [ ] T003 — Update README and the docs when behavior changes
- [ ] T004 — Run the command-backed checks (`gate run --profile normal`); treat any failure as blocking
- [ ] T005 — Advisory checks ONLY where no command exists yet — labelled advisory, never reported as gate proof

## Dependencies

Which tasks block which (prerequisites first); call out independently-executable tasks.

## Gate Notes

Manual review is not deterministic gate proof. Mark every unavailable planned command as a gap until the command exists.
