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

- [ ] **RP112 — One message shape.** A finding message says the thing's kind and id, where, and the measured
  number against its limit, in one sentence. The helpers that write most of them take that shape first:
  `SoftTerm` (25 terms), `Decorator.Declined`, `PlanValidator.Lint`; a fix in prose becomes the finding's
  `Edit` where one can be stated. The producibility check's 11 kebab-case ids become catalogued rules, and
  its `Cites` of `G2`, `WL7`, `BZ9` and `ST1`, which measure something else, cite nothing or a rule of their
  own. `docs/refusals.md`.
- [ ] **RP113 — Reword the gate rules.** Every gate rule's meaning and fix to the format, at most 35 words
  each; a rule that still needs more is split or sent back to the author, never squeezed. The rules an
  authoring run can raise first. `docs/refusals.md`.
- [ ] **RP114 — Reword the inline messages.** The 271 messages written at their raise sites, to the same
  format; the audit's per-site list is the worklist. `docs/refusals.md`.

