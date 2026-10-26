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
they make. Phase 3 has shipped too: soil by slope, paint rules, beds that dip, fold and cycle, a water's own bed
and its fall off the edge, and a layer's underside. Phase 4, space in three dimensions, is open with one entry
left, and Phase 5, stamps and uploaded worlds, runs beside it on the author's call.

### Phase 4 — space in three dimensions

The ground stays in columns and the carves run after it in the block grid of the built world, so a cave keeps
the relief over it (`docs/pgmvox-port.md`, *The shape it lands in*). Each entry is pgmvox's `volume/` ported, and
`docs/tools/sketch.md` is the document each leaves correct.

- [ ] **TS212 — Dragging a solid out in the dock.** The dock draws a rectangle, polygon, polyline or lasso, and a
  solid is made by reading a drawn footprint with the inspector's Form row. Add dock tools that drag out a box,
  wedge, cylinder, sphere or torus with the form already chosen (`SketchTool.razor`, `asSolid` in
  `geometry/shape.js`, `addShape` in `sketch-bridge.js`), and inspector rows for an arch's rock `thick`, its easing
  powers and its `camber`, stated over the API today (`docs/tools/sketch.md`, *Solids*).

### Phase 5 — stamps and uploaded worlds

A pgmvox board becomes playable with the studio's markers and outlines on it: the pieces it needs are placed in
Layout, every stamp can be switched off where the world already carries its own, and a world built elsewhere
arrives with its `map.xml` (`docs/pgmvox-port.md`, *Phase 5*).

- [ ] **TC23 — A world uploaded with its `map.xml`.** `POST /map/upload` takes a zip of a world's `region/` and
  its `map.xml` from any member, refused over a size cap, scanned as `import-url` scans; the stamps run over it
  on export. The `map.xml` is read into an intent, on the author's call: teams, spawns, wools, monuments, cores,
  control points and build regions become pieces placed in Layout, and what the studio cannot state is refused by
  name (`docs/tools/configure.md`, `docs/tools/flow.md`).
