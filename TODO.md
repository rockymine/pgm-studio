# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**One programme, and it is ordered.** A `spawn` or `wool-room` piece is doing four jobs at once — the ground,
the protection region, the building raised on it, and the bounds every marker on it is held to — and the
entries below are the one split that separates them, in the order each phase leaves the next one small. The
board runs at the soft cap on purpose and takes nothing new until the split lands; anything found while
working goes to `BACKLOG.md`. The library programme is pushed back down for the duration.

**Three numbers are the author's and are not to be re-derived.** A protection region is at most **20×30**
blocks, a building footprint at most **20×20**, and the smallest room with no building over it is **4×4**.

## The piece that does four jobs: the region, the marker, and the building on it

- [ ] **TN11 — The footprint is drawn on the canvas but never reaches the sketch.**
  `PlanCompiler.AppendStructuralShape` projects a role piece into the sketch as one locked annotation, and it
  is the piece rect that goes — so the building the author now states and drags is invisible downstream.
  Project **both**: the region as the ground annotation it is today, and the footprint as a second tagged
  rectangle. That second shape is what `B145` hangs a theme scope on, and what would let the levelling fill
  under a room read a material rather than defaulting to stone.
