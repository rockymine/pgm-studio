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

## Current focus — Sketch tool (S)

The depth pass on the shipped Sketch tool (`/maps/{slug}/sketch`): rotation, polygon split, snapping +
ruler feedback, the Floor/Height redefinition, and the sidebar tidy — all additive to
`SketchShape`/`SketchLayout`/`SketchRasterizer` and their JS twins. Full design — data-model diffs,
rasterizer/artifact changes, open decisions — in `docs/contracts/sketch-tool-improvements.md`. The
parked sketch slices (`S2`, `S9b`, `S10`) and the sketch world-export (`P9`) live in `BACKLOG.md`.

- [ ] **S12 — Finish P0#1: pin the Islands tree to the top of the sketch sidebar.** After `S11` removes
  Setup, the residual weight above **Islands** is the **Layers** panel + the 12-tile **Library** palette.
  Collapse both behind `<details>` accordions (Library default-collapsed once the map has shapes), or move
  the Library to a toolbar popover (it's a "reach for a primitive" action, not persistent state). Depends on
  `S11`. (`docs/sketch-tool-ux-review.md` P0#1; `docs/contracts/sketch-creation-flow.md` follow-on.)
- [ ] **S16 — Resize library primitives on placement.** Library primitives (n-gons, polyominoes, composites)
  instantiate at a fixed default cell size (`geometry/shape-library.js` `instantiate`) and can't be resized — and
  since they come in as polygons they lack the rectangle's 8-handle resize. Add a scale affordance: drag-to-size
  during placement and/or a uniform resize handle on a placed polyomino group. Relates to `S10` (the parked
  polygon resize-handles decision) and `S8` (the library). Needed for the polyomino-based generation (`G15`).
