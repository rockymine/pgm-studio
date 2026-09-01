# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**One programme, and its foundation comes first.** A room's building and a dressed building are the same
thing — a footprint and a shell — and the author's ruling is that the first is a **special case** of the
second. Today they are two models, two renderers, two selection paths and three different minimum spans, and
every entry below is smaller once that is one thing. `WE71` is that change; the three after it are what stop
being separate work when it lands. Anything found while working goes to `BACKLOG.md`.

**Three numbers are the author's and are not to be re-derived.** A protection region is at most **20×30**
blocks (`ST10`), a building footprint at most **20×20** (`ST9`), and the smallest room with no building over
it is **4×4** (`WX2`).

## A building is a footprint and a shell, wherever it came from

- [ ] **WE71 — A room's building and a dressed building are one concept under two models.** A room states one
  `Rect`; a `HouseProp` states `AuthoredWing[]` — so a room is the **single-wing case** and nothing says so.
  Three numbers disagree over one idea: a wing is refused under **3** across
  (`PlacedProp.cs:422`, whose remark reads *"at least as wide as a room"*), a room under **4** (`MinRoomSpan`)
  and a shelled room under **6** (`MinSpan(walled: true)`); the covered area is capped at **192** cells
  (`HouseProp.MaxFootprint`) against `ST9`'s 400. Two renderers draw them — `paintDressing` filled,
  ghosted and selectable, `paintStructural` dashed and locked — and only one can be picked up.

  Give a building one shape (wings, of which a room's is one), one span law, one cap, and one canvas layer, so
  a room's building is drawn, hit-tested and mirrored by the code that already does all three for a prop. The
  author's oracle settles the numbers: `HP2`'s 3 against `WX2`'s 4 is a question, not an arithmetic slip.

  *Three docstrings assert the opposite and are the tell: `HouseProp`'s (*"nothing else is shared, and the
  difference is worth stating"*), `MaxFootprint`'s (*"nothing a dressing limit has any business refusing"*),
  and `decoration.md` §8 (*"the two share a stamper and nothing else"*) — which opens by conceding that
  the stamper *"knows or cares"* nothing about where a footprint came from.*

- [ ] **B145 — A spawn or wool room's ground carries no theme.** A role piece reaches the sketch as a
  role-tagged annotation and `SketchRasterizer` skips it outright (`line 1027`), so it is never a shape a
  theme can be scoped to: the ground under a room is whatever its fused component paints, and there is no way
  to state a room floor. It is also what `StructureStamper.StampFoundation` levels in, so the material that
  fills a dip under a footprint is the same question. The shape to hang it on is there — a room projects a
  `building` annotation carrying its footprint. Wants a theme scope on it, and the levelling fill reading it.

  *re-probed on `marlstone-steps`: the column under the red wool at `(0, 85)` is raw `1:0` Stone from y24 down
  to y1, on a board whose `crest` theme is quartz. Reported independently by four runs.*

- [~] **B107 — A structural shape cannot be selected, so its stated height cannot be corrected.** The backend
  half is landed (`FEATURES.md`): a shape's stated height survives a recompile, marked per field and carried
  by `intentRef`. `sketch-canvas.js` keeps structural shapes render-only — never hit-tested, never selected —
  so nothing can write the `height_authored` flag a correction sets. Wants selection and an inspector row for
  the stated height. The `building` shape has no height of its own and takes no such row; the region does.

- [ ] **S25b — Make the surfaced spawn/wool shapes movable, writing the move back to the intent.** S25 landed
  them as **locked** read-only rectangles (`FEATURES.md`), and `TN11` added the second one. The next slice
  makes both draggable: moving the region writes `Protection`, moving the building writes `Footprint`, so the
  sketch and the intent cannot diverge. **Resize stays deferred** — a spawn/wool's `at` is a fractional offset
  into the region, so resizing shifts the marker and needs its own handling. Needs a write path
  (sketch → intent); the read projection already exists for both shapes. Then extend beyond spawn/wool to the
  other intent entities (build / monuments / iron) as they each earn a sketch surface.
