# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
When this board drains, pull the next theme up from `BACKLOG.md`. Board rules live in `CLAUDE.md`
(§ "Status & task board").

Task ids are a section letter + number (`S13`, `B10`, `G15`) — **globally unique and stable** across all
three files. Moving a task between files never changes its id; never renumber or reuse.

## Current focus — (board drained)

The **Generated XML conventions (B)** theme has shipped in full — the serializer conventions and the
CTW-standards batch (team-id suffix, spawn protection, build blocks → `<itemkeep>`, kit item slots) are all
in `FEATURES.md`. Generated `map.xml` now reads like a real PGM map.

Pull the next theme up from `BACKLOG.md` — candidates: the rest of the backend/generation tail (`B`/`G`),
Configure/authoring polish (`N`), or the parked Sketch polish (`S12`/`S16`).
