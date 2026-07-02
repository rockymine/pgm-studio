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

The **Sketch world-folder export (P9)** theme has shipped in full — `P9a`–`P9l` are all in `FEATURES.md`.
Sketch-originated maps now export a playable `{slug}/` world ZIP (`map.xml` + `level.dat` + `region/*.mca`)
from the Configure Export button; imported maps still export plain XML. Spec: `docs/contracts/sketch-world-export.md`.

Pull the next theme up from `BACKLOG.md` — candidates: Configure/authoring polish (`N`), layout generation
(`G`), shared editor/canvas infra (`C`/`CV`), or the rest of the backend/pipeline tail (`B`/`P`/`A`).
