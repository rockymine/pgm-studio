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
- [ ] **RP115 — Split the rules whose text leaves a check out.** Eight rules state only part of what they
  refuse, because the rest did not fit 35 words; each check left out becomes its own constant and text, raised
  where it is checked. `HS3` (roof material, `HouseStyleValidation.cs:736`–`:748`, against slab and stair cut
  from it, `:667`–`:708`), `RQ5` (stale `If-Match` `DocumentWrite.cs:51`, slug taken `WorldFolderImport.cs:66`,
  entry bound `Refusals.cs:102`, nothing to hand over `NoteEndpoints.cs:236`, nothing to draw
  `WorldReadEndpoints.cs:262`), `RQ11` (a full queue, `BuildQueue.cs:64`), `SK1` (a posted terraform replaced,
  `SketchEndpoints.cs:231`), `SK4` (collinear polygon `SketchLayoutCheck.cs:631`, zero-side rectangle `:639`),
  `ED2` (a `rot_90` counterpart, `SymmetryAuthoring.cs:99`), `OB17` (a wool monument over void,
  `MapExportComposer.cs:590`), `DR-PASS` (two sides against the coast, `Passage.Clears`). `docs/refusals.md`.
- [ ] **RP116 — A category that names the action its fix takes.** The category is what a caller branches on,
  and these name another action than their fix: `HS3`, `HS5`, `HS13` and `HS16` are filed `conflict` and are a
  value of the wrong kind (`malformed`); `PT3` is `unsatisfiable` and its fix is a field value; `DR-COPY` is
  `forbidden` and answers 400 (`PropStyleLibrary.cs:44`); `WX14` is `forbidden` and refuses nothing; `OB25` is
  `unsatisfiable` and its fix copies the built location. Each `[Rule]` attribute moves to the category its fix
  is, and `?category=` answers move with it. `docs/refusals.md`.
- [ ] **RP117 — One check, one id; one rule, one name.** `ST9` (`PlanValidator.cs:970`, a plan) and `WX13`
  (`WorldBuilder.cs:1060`, the world) both refuse a room frame past `RoomFrames.FootprintCap`: one id is
  retired into the other and both sites cite it. Six constants are named in words the glossary retired or name
  nothing: `NoLand` (`PL1`), `WallWithoutInterface` (`PL11`), `NamesNoLibraryRow` (`SR6`), `RockOnAFace`
  (`DR-STEEP`), `RockInTheGroundsTone` (`DR-TONE`) and `DressingJson.Rule` (`DR-DOC`); each is renamed with every
  caller in one commit. `docs/refusals.md`.
- [ ] **RP114 — Reword the inline messages.** The 271 messages written at their raise sites, to the same
  format; the audit's per-site list is the worklist. `docs/refusals.md`.

