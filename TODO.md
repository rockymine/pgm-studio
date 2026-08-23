# pgm-studio — TODO (current focus)

The **Now & Next** board — only the *current focus theme* lives here. Everything not in the immediate
slice is in **`BACKLOG.md`** (the long tail); shipped capabilities are in **`FEATURES.md`** (the Done
column). The three move left → right: **`BACKLOG.md` → `TODO.md` → `FEATURES.md`**.

**Holds only open work:** `[ ]` to-do, `[~]` in progress — **never `[x]`.** When a task ships, a commit
lands (its message references the id), the task **leaves this file**, and a line is added to `FEATURES.md`.
Board rules live in `CLAUDE.md` (§ "Status & task board").

**Three entries, all about a stack of layers.** What is left of the group five authored maps
(`pgm-studio-mapgen/reports/opus5-*`) opened is the one question its other entries did not touch: a board
whose ground is stacked, where a hall runs under a terrace and every read projects the column to one cell.

## A stack of layers: what it writes, and what no read can say about it

- [ ] **TS21 — Write layers down, then make a stack readable.** `docs/tools/sketch.md` gives a layer five
  lines and closes with "an agent should author the ground layer only", which is advice rather than a
  description. Two halves, and the first is a day's prose against a feature that otherwise costs three
  probes and a source read to use: the **stone-only invariant** in `TerrainPainter.Paint` is the single
  line that lets an air gap survive between two slabs; `floor` is the underside, measured inside the layer;
  relief solves per layer and returns already shifted into world Y; and `TerrainBuilder.SurfaceTops` keeps
  the **maximum** top per cell, which is why a placement climbs onto the upper layer by itself, a path
  paves the deck, the covered ground is unpainted (`B144`'s cause, one tier up) and nothing can be dressed
  underneath.

  The second half is real work and builds on the walk: **no read can say whether a player can
  get under an overhang**, because `WorldColumns.Membership` projects a column to one cell and
  `relief/read` walks a height field. A layered board's correctness is exactly the question its reads
  cannot ask.

  *`opus5-undercroft` carries a nine-block hall under a terrace, entered at z 58 and open over a void court
  at z 40. `traversability` calls the board one component and says nothing about it; `coverage` counts its
  cells as the terrace's.*

- [ ] **WE22 — A slab over void should not lay bedrock at the bottom of the world.**
  `TerrainBuilder.Build` writes a bedrock course at y0 under every footprint cell it fills, per layer, so a
  bridge slab across a strait drops its own plate into the abyss and an overhanging deck does the same over
  whatever it hangs past. The theme's `bedrock` value does not reach it — the painter only overwrites
  stone. Condition the course on the cell having no lower segment.

  *`opus5-undercroft`: bedrock at y0 under **(−32..−12, −10..10)** and **(12..32, −10..10)**, under two
  bridges 14 blocks above it. Those columns also join the Y0 set a void filter reads.*

## What the front door still cannot say

- [~] **RP23 — `docs/tools/capabilities.md` is 707 lines answering "what can I ask for", which the API now
  answers itself.** The schema names every route, its body and its failure codes; `GET /api/rules` names
  every refusal with its fix; `GET /map/{slug}/layers` puts the allowed moves on the map's own response.
  What prose is good at and this file is not organised around is the other half: **how to make a good map** —
  what an objective needs around it, what the corpus does — as against **what the system can be asked for**.
  Split it on that line: the capability half goes, the craft half moves to where its subject lives under
  `docs/gameplay/`.

  The mapgen half landed: `pgm-studio-mapgen`'s six root documents became two, and `AUTHORING-BRIEF.md`
  points at the four self-describing reads instead of restating them. This entry is the studio's own side.

  *Five authored boards never opened it. What they read instead was `relief.md`, `decoration.md`,
  `terrain-painting.md` and the endpoint tables inside them — the split this entry proposes, observed.*
