# Terrain painting — walls, rims, plateaus (G157)

A second pass over the **realized world**, sibling to the structure stamping of `structures.md` and the
first concrete slice of the theming work parked as G34. Where the stampers write rooms, cubes and
objectives onto the terrain, this pass dresses the terrain **itself**: the raw stone a map exports today
becomes a stone body walled in clay, lipped in quartz, and topped in grass. It reads the terrain the
world builder already placed and rewrites its surface — no new geometry, only materials.

**Status: planned — nothing is built.** The model below is the contract to build against; it is validated
only by a scratch prototype's figures (two real seeds — `mirror-tiny-map-cliff`, `isolated-spawn` —
compiled through `PlanCompiler` and rasterized through `SketchRasterizer`), not by any shipped code. Rule
ids here are `TP*` (terrain paint), local to this file the way `structures.md` owns `WX*`. Read alongside:

- `docs/world-export/structures.md` §6.4 — the preset seam (style-as-data). Terrain materials attach here.
- `docs/contracts/sketch-world-export.md` — the world the painter runs inside (layer scheme, `level.dat`).
- `SketchTerrainBuilder` (`PgmStudio.Minecraft`) — the terrain this pass consumes: bedrock at y=0, stone
  fill to each column's surface top, and the per-cell `SketchTerrain.SurfaceTop`.

---

## 1. The painter's domain

The painter touches **only stone**. Terrain arrives from `SketchTerrainBuilder` as a bedrock floor at y=0
with a stone column filling `[1, surfaceTop)` above it; every structure the stampers add — the wool-cage
and spawn-cube **bedrock plateaus**, their shells, the objectives — is bedrock, wool, obsidian, anything
but stone. So the pass runs **last**, after every stamp, and rewrites stone blocks only. Bedrock at y=0 and
the full room pieces (a wool room sits on a solid bedrock plateau, not on paintable stone) are excluded
**by construction**, not by a special case — nothing the painter is allowed to touch survives underneath a
room. This is the one invariant that makes the rest safe to state simply.

The only input beyond the world is the per-column **surface top** (`SketchTerrain.SurfaceTop`, the first
air Y over the column) — the elevation the whole model reads from. A column's top solid block sits at
`surfaceTop − 1`.

## 2. The three outputs

In pipeline order: read the elevation, classify each stone column by its edges, then paint. Every column
splits into at most three painted parts and one kept part — bedrock (y=0, kept), the **rim** (its
top-most block, when it is an edge), the **wall** (the exposed riser below the rim), and the **interior**
(the top-most block, when it is not an edge).

### Rim — the top-most block

- **TP1** *The rim is a single block — the top of an edge column.* Not a volume: one block, at
  `surfaceTop − 1`, and only where the column is on an edge. A map's rim is therefore a one-block-wide
  line tracing every lip, quartz against the grass.

- **TP2** *Edges are found over the eight neighbours, so a corner never gaps.* A column is an edge when
  **any of its 8 neighbours** is void or carries a strictly lower surface. The four-neighbour test is
  wrong here: at a reentrant (inward) corner of an L or a comb, the corner block's only lower neighbour is
  diagonal, so four-neighbour leaves a one-block hole in the outline and the rim reads as broken. Eight
  neighbours close it. The addition is exactly the reentrant corner cells — a straight edge and a convex
  corner are identical under either test — so the rule thickens nothing, it only completes the turn.

- **TP3** *`closed` extends the lip to the full outline of the plateau.* The base (open) rim lips only
  **drops** — a neighbour that is void or lower — so it stops wherever a plateau runs **up** into a room
  or a taller piece. The `closed` bool traces the whole plateau boundary instead: a column is rim when any
  8-neighbour is void, **a room**, or **a different plateau** (higher as well as lower). The difference is
  precisely the edges that face something that increases height — the ring of grass abutting a wool room,
  the seam where a low body meets a raised step from below. `closed` is off by default; the open rim is the
  base every map wants, and the full outline is the opt-in.

### Wall — the exposed riser

- **TP4** *The wall is the stone between the bedrock and the rim, on the exposed face only.* For an edge
  column of height `T = surfaceTop`, the wall is the stone from the **shallowest orthogonal drop** up to
  just below the rim: `y ∈ [drop, T − 2]`, where `drop` is the smaller of the lower orthogonal neighbours'
  surfaces, floored to `1` when the neighbour is void (the bedrock course at y=0 is never wall). Stone
  below `drop` stays stone — it is buried behind the neighbour that made the drop, and painting it would
  only tint blocks no one can see. A freestanding nine-tall column against the void therefore reads, bottom
  to top, **1 bedrock · 7 wall · 1 rim**; a four-block step onto a lower body walls only its four exposed
  courses.

### Interior — the grass top

- **TP5** *The interior is the top block of every non-edge column.* One block of grass at `surfaceTop − 1`,
  everywhere the column is neither rim nor buried under a structure. The body beneath it is left as stone.

## 3. Materials — the preset seam

TP1–TP5 produce **masks**, not block ids: which columns are rim, which faces are wall, which tops are
interior. The blocks those masks resolve to are a **preset** — the same style-as-data seam the room shells
use (`structures.md` §6.4, `DestroyableStyle`'s precedent), so a theme is a data row, never a code path.
The first intended knob is the **team colour on the wall**: an island's stained-clay hue, chosen per
plateau by the team that owns the island — the prototype tints by side of the mirror centre as a stand-in,
but the real assignment reads island→team from the intent. Grass and quartz are the neutral defaults;
bedrock-and-rooms-untouched is not a preset choice, it is TP's domain invariant.

## 4. The cases — and the tests they drive

Each scenario the prototype separated is a fixture waiting to be written; together they cover the model.

- **Disjoint, void-bounded plateau.** A shape surrounded entirely by void: its rim outline coincides with
  its wall footprint, and there is no internal rim. The base seeds are this case (their shapes never
  overlap in height), which is why they cannot exercise the interesting rules — the test asserts
  `openRim == wallFootprint` and `closedAdds == 0`.

- **Nested step.** A higher plateau resting on a lower body (real: `mirror-tiny-map-cliff` at five levels,
  `isolated-spawn` at three): the step's rim faces the lower terrain, not the void, so the rim strictly
  exceeds the void wall. Asserts internal-rim cells exist and each has a lower orthogonal neighbour.

- **Reentrant corner.** An L or comb outline (the real `h` comb body carries several): the fixture is the
  small window where four-neighbour and eight-neighbour disagree — eight-neighbour must add exactly the
  concave cell and nothing else.

- **Room abutment.** A plateau running up into a wool/spawn room: open rim stops at the room edge, `closed`
  lines it, and **neither** ever paints a block inside the room piece. Asserts `closedAdds` covers the
  room-facing ring and the room-piece footprint stays bedrock.

- **Multi-level stack (the cross-section).** A vertical slice through terraced terrain: per column, bedrock
  / wall / rim / grass in the right order, buried stone under a step's drop, and a room column painted
  nothing. This is the readable oracle for TP4's riser bounds.

- **Bedrock floor and full room piece.** The negative control: y=0 and every stamped structure stay
  exactly as the terrain builder and stampers left them, because only stone was ever eligible.

## 5. Where it attaches

A pure pass — `TerrainPainter` over `SketchTerrain`, called last in `SketchWorldBuilder.Build` once the
stampers have run — reading the world plus `SketchTerrain.SurfaceTop`, rewriting stone in place. Pure over
`(world, surface, options)` the way the stampers are pure over their frames, so it unit-tests directly
against the fixtures above with no DB and no IO. The `closed` bool and the material preset ride a small
paint-options record; the preset resolves masks → block ids at the §3 seam.

## Rule catalog

| id | rule |
|---|---|
| **TP1** | The rim is one block — the top of an edge column, at `surfaceTop − 1`. |
| **TP2** | Edges are found over the 8 neighbours (void or lower), so a reentrant corner never gaps. |
| **TP3** | `closed` extends the rim to the full plateau outline — also lipping edges facing a room or a taller plateau. Default off. |
| **TP4** | The wall is the exposed riser, `y ∈ [drop, surfaceTop − 2]`; buried stone and the y=0 bedrock course are left. |
| **TP5** | The interior is the grass top of every non-edge column. |
