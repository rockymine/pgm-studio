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

## The layout vocabulary: one word for each thing the composer builds
The composer is described by cells, pieces, parts, roles, compounds, forms, vacancies, rooms, markers, boxes,
kinds, joints, layouts and lane widths, and these steps bring its types down to those words. Each leaves the
composer's fingerprints unchanged except the last, which removes a random draw.

- [ ] **G299 — The composer keeps only what it reads.** Delete `PublishPolicy`, `ClosureAnalysis`, `EdgeSpan`,
  `BoxLayout.Structural`, `DockingGate.DockingEdges`/`CanDock` and `BoxEdgeInterface.TouchesRoom`/`HasTerrain`,
  and drop *approach* as a code word for a wool compound. `model.md` and `vocabulary.md` are restated in the
  plan's words. `docs/generator/model.md`.
- [ ] **G300 — Offers lose their grouping.** `OfferGrouping`, `EdgeOffer.GroupId` and the front line's coin flip
  that sets them go; only tests and `unit-fingerprint.cs` read them. Removing the draw moves boards, so the
  fingerprints are re-recorded and the reproduction gate reruns. `docs/generator/model.md` §7.
