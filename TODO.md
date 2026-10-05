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

## Rules and findings: one place, one shape, one wording
**The author's next focus.** Every rule a finding cites comes from one place, every finding message is
written the same way, and both say only the problem, where it is and how to fix it (the approved format:
the *Rule text audit* doc, *Format draft* tab). The order is the work's own: the declarations move first so
nothing is reworded twice, the message helpers next so the rewording lands once per helper, then the text.
Every rule fires as before; the tests and `--goldens` say so.

- [ ] **RP119 — A plan check's fix as an edit.** Four plan checks state a fix a caller could apply without
  deciding anything, and carry no `Edit`: `G2` (widen a build region's `rect` to 10 blocks across,
  `PlanValidator.LintG2`), `SP9` (lengthen the piece or build region ahead of a spawn's door to 15 blocks,
  `LintSp9`), `ST9` (set a room's `footprint` to at most 20 by 20, `LintSt9`) and `ST10` (shrink a room piece's
  `rect` to at most 20 by 30, `LintSt10`). Each gains the `DocumentEdit` the way `RampEdit` gives `EL1` and `SP8`
  theirs, and the plan tool's Checks panel offers it. `docs/refusals.md`.
- [ ] **RP116 — A category that names the action its fix takes.** The category is what a caller branches on,
  and these name another action than their fix: `HS3`, `HS5`, `HS13` and `HS16` are filed `conflict` and are a
  value of the wrong kind (`malformed`); `PT3` is `unsatisfiable` and its fix is a field value; `DR-COPY` is
  `forbidden` and answers 400 (`PropStyleLibrary.cs:44`); `WX14` is `forbidden` and refuses nothing; `OB25` is
  `unsatisfiable` and its fix copies the built location. Each `[Rule]` attribute moves to the category its fix
  is, and `?category=` answers move with it. `docs/refusals.md`.
- [ ] **RP120 — The readers' faults in the message shape.** A document that will not read is refused with
  the exception's text as its message, at 30 sites that pass `fault.Message` on, and those texts are written as
  sentences of their own: `HouseStyleJson` (5 throws), `TerrainThemeJson` (5), `SketchRelief` (2), `SeedFolder`
  (2), `DressingJson`, `CellRectJsonConverter`, `RectListJsonConverter`, `PlanModel.Unreadable`, `EyeAim.Read`,
  `MapParser`'s `UnsupportedMapException` and System.Text.Json's own `JsonException`. Each is rewritten to the
  shape in `docs/refusals.md` ("`` `floor` `` is not a number"), the JSON one wrapped where it is caught. `IntentWrite.RefuseNames` drops which check
  `AuthorNames.Refuse` failed, so a name 40 characters long reads the same as one with a symbol in it; the
  message states the one that failed, with the length against `AuthorNames.MaxLength`. `docs/refusals.md`.

