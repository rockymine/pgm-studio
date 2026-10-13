# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**Three numbers are the author's and are not to be re-derived.** A protection region is at most **20×30**
blocks (`ST10`), a building footprint at most **20×20** (`ST9`), and the smallest room with no building over
it is **4×4** (`WX2`). A dressed prop's 192-cell ceiling (`HP3`) and a room building's 20×20 measure the same
concept since `WE71`, and holding them apart is a deliberate not-yet.

## Conquest, continued: a box that sends its scorer home, a zone, a touch point
**Conquest is played for control, and these are the shapes control takes besides a hill and a flag**, as
pgmvox writes them (`pgm-studio-mapgen/freeform/lib/pgmvox/play/objectives.py`, `play/rules.py`). A score
box and the portal home are authored; a zone and a touch point remain.

- [ ] **WE164 — A plan-built score box is air.** A box is entered on foot, so a box the terrain or a stamp fills
  is one nobody scores in. `WorldBuilder` clears every box's blocks to air after the stamps, fanned by team
  across the layout's symmetry as flag posts are before stamping, with the ground under it kept, the way
  `ControlPointStamper.ClearCaptureVolume` clears a capture volume. `docs/world-export/structures.md`.
- [ ] **PG27 — A zone, in general.** A region with rules of its own: no placing, no breaking, no harm, entry
  for one team or nobody, a kit given inside — what spawn protection, the build area and the wool room each
  state for one purpose. One `ZoneIntent` those three are written through, after pgmvox's `Zone`
  (`play/rules.py:490`), rather than a fourth special case. `docs/pgm/filter-region-wiring.md`.
- [ ] **PG28 — A touch point is a capture setting.** `ControlPointIntent` gains a capture word: `hold` (today:
  neutral state, capture time) or `touch` (taken at once by whoever enters, 0.1s, `capture-rule="lead"`, no
  neutral state, kept until another team touches it), as pgmvox's `TouchPoint` writes it
  (`play/objectives.py:706`). `ControlPointGenerator.cs:67`. `docs/pgm/control-points.md`.

## The programme: a board's source, every change to it, and the words it is stated in
**The author put this programme first.** The studio keeps the source a board is stated in and every change to
it, hands the author's hand edits to the agent's next round, and hands out the vocabulary the source is
written in. The source, its changes, the kit it is written in, coasts, pulls, basins, outlines and the fan
as statements, and `pgm-studio-mapgen` stated in all of them have shipped; what remains is the point edits
reaching every outline a board states. The layer word (`B264`, `WE28`, `TS64`) waits behind it.

### A point edit and a bend reach every outline

- [ ] **TS130 — Point edits and bends reach every outline.** `editShapes` and `bendShapes` address a shape's
  `vertices` alone (`SketchGeometryEdit.Outline`), where `outlines` already writes a relief `area` mark's and a
  push's `ring` and a stroke's, fluid's or flora's `points` (`Refinement.Outlined`). Both reach those three,
  each refused where an edit folds the ring, so a mark or a prop takes a pull or a bend instead of stated
  points. `docs/tools/flow.md`.

  *Evidence: 51 of the 79 seeded outlines on the 5b and 5c boards are relief rings or prop points, and so is
  Gypsum Reach's wash, a push whose ring splices six authored points into a lobed ellipse.*
