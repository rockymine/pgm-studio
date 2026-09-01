# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**One programme, and its foundation has landed.** A room's building and a dressed building are the same thing
— a footprint and a shell — and the first is a **special case** of the second (`WE71`, `FEATURES.md`): one
least span, one ink on all three canvases, and the prose that said otherwise corrected. What is left is the
interaction that shape makes possible. Anything found while working goes to `BACKLOG.md`.

**A building's ceiling stays two numbers, for now (author).** A dressed prop is capped at 192 covered cells
(`HP3`) and a room's building at 20×20 by `ST9`; they measure the same concept since `WE71`, and holding them
apart is a deliberate not-yet rather than an oversight. Nothing below depends on it.

**Three numbers are the author's and are not to be re-derived.** A protection region is at most **20×30**
blocks (`ST10`), a building footprint at most **20×20** (`ST9`), and the smallest room with no building over
it is **4×4** (`WX2`).

## A building is a footprint and a shell, wherever it came from

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
