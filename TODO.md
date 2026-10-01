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
written in. The source, its changes, the kit it is written in, and coasts, pulls, basins, outlines and the
fan as statements have shipped; what remains is `pgm-studio-mapgen` moving onto all of it. The layer word (`B264`, `WE28`,
`TS64`) waits behind it.

### The authoring repository moves onto the studio's source and kit

- [~] **RP96 — The boards on the hand kits move onto the studio's words.** Sixteen `build-spec.py` import
  `specs/opus55_kit.py`, `specs/sonnet55_kit.py` or a copied `common.py`/`opus5c.py`, and those retire. Their
  materials, themes and props move onto `GET /api/kit.py`'s constructors unchanged, their rings onto `outlines`
  (`LobedOutline` is `ring()`'s own formula), and `coast_edits` onto `pulls` (`TS128`, the same arithmetic).
  The 5b/5c seeded outlines, Gypsum Reach's wash and Quarrymoot's rim are stated as their points, and Gypsum
  Reach's isle pulls are stated once and fanned. Every board's applied layout and intent stay what they were.
  The `@name` loader goes with `TL33`. `pgm-studio-mapgen/AUTHORING-BRIEF.md`.

- [~] **TL33 — The house styles `pgm-studio-mapgen` keeps become the library's.** The 29 styles live boards
  stamp are seeded into the room library under their own names. What remains is the authoring repository: every
  board naming one as `{"library": …}` where it loads `tools/styles/<name>.json` today — `drive.py`'s `@name`,
  the kits' `house_style()` and `shipped()`, eight boards' own `style()` and four scripts reading a file by path
  — with `beams` and `footing` changes stated beside the name, after which the `@name` loader and the folder go.
  `pgm-studio-mapgen/tools/README.md`.

- [ ] **TS130 — Point edits and bends reach every outline.** `editShapes` and `bendShapes` address a shape's
  `vertices` alone (`SketchGeometryEdit.Outline`), where `outlines` already writes a relief `area` mark's and a
  push's `ring` and a stroke's, fluid's or flora's `points` (`Refinement.Outlined`). Both reach those three,
  each refused where an edit folds the ring, so a mark or a prop takes a pull or a bend instead of stated
  points. `docs/tools/flow.md`.

  *Evidence: 51 of the 79 seeded outlines on the 5b and 5c boards are relief rings or prop points, and so is
  Gypsum Reach's wash, a push whose ring splices six authored points into a lobed ellipse.*
