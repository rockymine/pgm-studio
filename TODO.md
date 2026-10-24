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

## The programme: bringing pgmvox into the studio
**The author put this programme first.** `docs/pgmvox-port.md` is the programme: pgmvox's capabilities move
into the studio one phase at a time, each proven against a pgmvox fixture, until a board built on pgmstudio.de
reaches the ceiling a pgmvox board does. Phase 0 has shipped, Phase 1 has closed with its two agent-facing
entries parked, and Phase 2 has shipped: shapes, eighteen ground operations in the modifier stack, and the water
they make. Phase 3, paint, lay and water, is open, and its entries run in the order below.

### Phase 3 — paint, lay and water

The ground's surface is chosen by what the ground is, not by which shape drew it: a steep face is rock because it
is steep, a river's bank is sand because it lies beside the river. Each entry is pgmvox's `surface/terrain.py`
ported into the painter (`src/PgmStudio.Minecraft/Painting/`), proven by the board it builds, and
`docs/world-export/terrain-painting.md` is the document each leaves correct.

- [ ] **WE170 — Paint rules, first match winning.** A theme's surface gains `rules[]`: a material and any of
  `slope` `(lo, hi]` degrees, `height` `[lo, hi]`, a noise range and `near`, a modifier id and a width that reads
  that modifier's line or outline, so a River's bank is sand by naming the River. The first rule a column meets
  paints it; none, and the surface material does. Rules sit ahead of the bucket's material in
  `TerrainPainter.ColumnBlocks`; the band axes stay as they are.

- [ ] **TS211 — A layer's underside.** A layer's shapes state an underside that tapers in from the rim, 4 blocks
  at the edge and 0.9 more per block inward, steeper beside a `sheer` side, and rest on the ground below where less
  than 4 blocks would stay clear — pgmvox `island_bottom` and `_level` (`surface/terrain.py:272`,
  `document/layers.py:1022`). Lands in `Geom` and sets each segment's floor in `SketchRasterizer`.
  `docs/tools/sketch.md`.

