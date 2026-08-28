# Terrain painting — walls, rims, plateaus (G157)

A second pass over the **realized world**, sibling to the structure stamping of `structures.md` and the
first concrete slice of the theming work parked as G34. Where the stampers write rooms, cubes and
objectives onto the terrain, this pass dresses the terrain **itself**, bucket by bucket — rim, wall,
surface, fill — through whichever theme applies; unthemed, every bucket stays the raw stone it already is.
It reads the terrain the world builder already placed and rewrites its surface — no new geometry, only
materials.

**Status: the whole model — TP1–TP21, including scoped per-shape theming (TP10) — is built and shipped.**
`TerrainPainter` (`PgmStudio.Minecraft`) paints every sketch export, wired last into `WorldBuilder.Build`;
the four-stage architecture of §5 is in place. A theme is resolved **per cell** through `TerrainThemeScope` (a
sketch shape's own theme, else the map default); themes are authored in the Sketch tool's **Theme** phase and
stored on the sketch layout. Depth is a per-bucket knob (`TopBand`
carries a bucket's material, depth and toggle), so a theme sets the rim depth and the surface stack
independently; the built-in default states no finish — stone, one block deep (TP11) — and a picked theme,
such as the library's seeded Meadow finish (grass over two dirt, three deep), states one. Any bucket's material can
be a pattern — voronoi or cell regions, a fractal / turbulence / electric field, wall-runs that wrap the
void-facing perimeter (TP13), those runs sheared onto the diagonal, a checkerboard laid in the face it
paints (TP17), or that same board turning one log upright and on its side (TP20) — every one of them sampled
at the cell folded into the board's primary image, so a mirrored map is painted alike on both sides (TP21) —
and the whole theme serializes to the theme JSON (`TerrainThemeJson`), the data a TP10 scope attaches to a
shape. The model was first validated by a
scratch prototype's figures (two real seeds — `mirror-tiny-map-cliff`, `isolated-spawn` — compiled through
`PlanCompiler` and rasterized through `SketchRasterizer`) and is now covered by `TerrainPainterTests`. Rule
ids here are `TP*` (terrain paint), local to this file the way `structures.md` owns `WX*`. Read alongside:

- `docs/tools/sketch.md` — the tool a theme is authored in (the Theme phase), and the layout it is stored on.
- `docs/world-export/relief.md` — the elevation this pass dresses: a mesa's face and a scarp arrive here as
  edges with a known drop, which is what the rim/wall rules were written for.
- `docs/world-export/structures.md` §6.4 — the preset seam (style-as-data). Terrain materials attach here.
- `docs/world-export/sketch-world-export.md` — the world the painter runs inside (layer scheme, `level.dat`).
- `TerrainBuilder` (`PgmStudio.Minecraft`) — the terrain this pass consumes: bedrock at y=0, stone
  fill to each column's surface top, and the per-cell `BuiltTerrain.SurfaceTop`.
- `TerrainPainter` / `TerrainProfile` / `TerrainTheme` (`PgmStudio.Minecraft`) — the implementation:
  the pass, the classifier core, and the theme.

---

## 1. The painter's domain

The painter touches **only stone**. Terrain arrives from `TerrainBuilder` as a bedrock floor at y=0 under
every column whose ground reaches it, with a stone column filling `[1, surfaceTop)` above it; every structure the stampers add — the wool-cage
and spawn-cube **bedrock plateaus**, their shells, the objectives — is bedrock, wool, obsidian, anything
but stone. So the pass runs **last**, after every stamp, and rewrites stone blocks only. Bedrock at y=0 and
the full room pieces (a wool room sits on a solid bedrock plateau, not on paintable stone) are excluded
**by construction**, not by a special case — nothing the painter is allowed to touch survives underneath a
room. This is the one invariant that makes the rest safe to state simply.

The only input beyond the world is the per-column **surface top** (`BuiltTerrain.SurfaceTop`, the first
air Y over the column) — the elevation the whole model reads from. A column's top solid block sits at
`surfaceTop − 1`.

### The stamps it consults

"Untouched" is not only the room pieces. Every stamped structure is bedrock, wool or obsidian, so the
stone-only rule already keeps the painter off all of them; but the rim and wall rules must additionally
**read** them, because a structure that rises above the terrain is a height the outline turns on and a face
the riser must not paint behind. The set the painter consults splits by how a stamp is anchored:

- **Piece-relative** — the wool/spawn **room pieces** (`WoolIntent.Piece`/`SpawnIntent.Piece`) and the
  iron cubes: a whole footprint, a bedrock plateau (`structures.md` WX1).
- **Interface-relative** — the **bedrock approach wall** (`StructureStamper.StampWall`, from
  `StructureIntent.Walls`; rule ST4). Not a piece but a **seam barrier**: two blocks thick across the shared
  edge between two pieces, the interface width along it, filled with bedrock from y=0 up to
  `approach.Surface + 2` and capped by one course of cobweb — so it stands **above** the terrain on both sides.

- **TP6** *A stamped structure is height-bearing, and the painter reads it as a wall, never as a drop.* For
  every rule below, a neighbour that is a structure — a room plateau or an approach-wall barrier — is
  treated as **taller-or-equal terrain that seals the face**: it is never a drop (so it never opens an
  open-rim lip and never paints a clay riser toward the structure — the barrier already covers that seam),
  and it always counts as a boundary for the **`boundary`** rim (the plateau's outline turns at it, TP3). This
  is one rule for both anchorings; the approach wall enters the model through exactly the same door as the
  room piece. The clean way to get it is to read the **finished** world — a neighbour column whose top solid
  is not stone is a structure, and its height is that column's top — so no separate registry is needed,
  though the footprints (`Piece` rects, the `WallStructure` list) are equivalent inputs.

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

- **TP3** *`rimEdges` picks how wide a net the lip casts — three nested tests, narrowest first.* The base
  answer, **`drop`**, lips only **drops**: a neighbour that is void or lower. It stops wherever a plateau runs
  **up** into a room, an approach wall, or a taller piece.
  **`boundary`** traces the whole plateau outline instead: a column is rim when any 8-neighbour is void, **a
  structure** (room or approach wall, TP6), or **a different plateau** (higher as well as lower). The
  difference is precisely the edges that face something that increases height — the ring of grass abutting a
  wool room, the seam a bedrock approach wall seals, the edge where a low body meets a raised step from below.
  **`void`** narrows instead of widening: a column is rim only where a neighbour is **off the footprint
  entirely**. That is the landmass's true outside, and it is the answer a body built out of stacked plateaus
  wants. A staircase of five shapes each a block above the last is a drop at every tread, so `drop` draws a
  lip on each one and the body reads as five plateaus that happen to touch; `void` caps the outside and leaves
  every tread bare, so it reads as one body with a rim around it. The rim is the only thing that changes — the
  wall has its own face question (TP9) and answers it independently, so a tread's riser is still walled.
  `drop` is the default; the outline and the outside are both opt-in.

### Wall — the exposed riser

- **TP4** *The wall is the stone between the bedrock and the rim, on the exposed face only.* For an edge
  column of height `T = surfaceTop`, the wall is the stone from the **shallowest orthogonal drop** up to
  just below the rim: `y ∈ [drop, T − 2]`, where `drop` is the smaller of the lower orthogonal neighbours'
  surfaces, floored to `1` when the neighbour is void (the bedrock course at y=0 is never wall). A structure
  neighbour is not a drop (TP6): a seam sealed by the bedrock approach wall exposes no face, so no clay is
  painted behind the barrier. Stone below `drop` stays stone — it is buried behind the neighbour that made
  the drop, and painting it would only tint blocks no one can see. A freestanding nine-tall column against
  the void therefore reads, bottom to top, **1 bedrock · 7 wall · 1 rim**; a four-block step onto a lower
  body walls only its four exposed courses.

### Interior — the grass top

- **TP5** *The interior is the top of every non-edge column.* The top course at `surfaceTop − 1` —
  the **surface** bucket (grass in the examples; layered to a depth under TP11) — everywhere the column is
  neither rim nor buried under a structure. The body beneath it is the **fill** bucket (§3).

## 3. Materials — the preset seam

TP1–TP5 produce **masks**, not block ids. The masks sort every paintable block into four **buckets**, and
the blocks each bucket resolves to are a **preset** — the same style-as-data seam the room shells use
(`structures.md` §6.4, `DestroyableStyle`'s precedent), so a theme is a data row, never a code path:

- **rim** — the edge lip (TP1).
- **wall** — the exposed riser (TP4).
- **surface** — the top of the interior, a **layered stack** to a configured depth, not fixed at one block
  (TP11) — one block of stone by default; grass over two dirt, three deep, in the seeded Meadow finish.
- **fill** — the interior body below the surface. The **required** bucket: it claims whatever no other
  enabled bucket took, so a theme is never partial (TP12).

A bucket's material is a **spec** (`TerrainMaterial`), not necessarily a single id. Its forms: a single block
(`SolidMaterial`); a **band stack** (`LayeredMaterial` — a run of materials each with a thickness, read along a
stated axis, §3.1); the **team tint** (`TeamTintedMaterial`, below); and the
**patterns** that vary the block across the bucket's cells (`VoronoiMaterial`, `CellMaterial`, `NoiseMaterial`,
`TurbulenceMaterial`, `ElectricMaterial`, `WallRunMaterial` — TP13, §6). Each nests any material, so a pattern can embed a tint or another pattern. The quartz / clay /
grass / stone in this document and the prototype are **examples**, chosen only to tell the buckets apart on
sight — none is canonical.

### 3.1 A band stack, and which way it is read

`LayeredMaterial` is a `BandStack` — an ordered run of `(material, thickness)` — plus the **axis** it is read
along. The stack states its bands and what happens where they run out, and deliberately not the distance,
because the distance belongs to whoever is measuring it.

| axis | the distance | what it draws |
|---|---|---|
| `depth` *(default)* | `BucketContext.DepthFromTop`, courses down from the top of the bucket | grass over two dirt; a wall's banded riser (TP11) |
| `inward` | `BucketContext.Inset`, steps in from the landmass's void-facing edge | a cobble rim, two rings of stone brick, then a field |

**One type with the axis named, not two types.** The bands, the thicknesses and the run-out rule are identical
either way and only the distance differs, so a second material reading a different property would have been
the same code twice. `depth` is what a layered material always meant and stays the default, which is why
nothing already stored has to be rewritten.

**`ending` decides what happens past the last band, and `beyond` says what shows there.** Under `repeat` the
last band claims everything further along, which is right where the stack owns its whole space — a bucket. A
ring stack usually does not own its whole space: it is meant to run a few rings in and then stop, so it ends
`handOver` and `beyond` answers for everything past it. Unset, `beyond` is stone, the fill every unclaimed
block already falls to. Off the footprint the inward axis is -1, which is not "past the last band" — there is
no ring to be in, so the stack is never asked and `beyond` answers directly.

**Restricting rings to the top course needs no knob, because the two axes compose.** A surface material that
is a `depth` stack whose first band is one course thick and is *itself* an `inward` stack rings the top course
and leaves the courses under it alone — which is also what keeps a grass ring one block thick rather than
three (a palette that repeats grass down every course is the most-repeated authoring mistake in this repo).

The author's own sequence — a rim, two rings of the next block, a checkered ring, five of a field, then the
surface — stored:

```json
{"kind":"layered","axis":"inward","beyond":{"kind":"solid","id":2,"data":0},
 "stack":{"ending":"handOver","bands":[
   {"material":{"kind":"solid","id":4,"data":0},"thickness":1},
   {"material":{"kind":"solid","id":98,"data":0},"thickness":2},
   {"material":{"kind":"checker","size":1,"a":{"kind":"solid","id":159,"data":15},
                "b":{"kind":"solid","id":159,"data":0}},"thickness":1},
   {"material":{"kind":"voronoi","seed":11,"scale":7,"bands":[…]},"thickness":5}]}}
```

**What the schema gained is two optional fields on `layered`** — `axis` and `beyond` — and nothing else. A
stored theme written before either existed reads as `axis: "depth"` with no `beyond`, which is exactly what it
meant. `ending` is now written as `"repeat"`/`"handOver"` rather than 0/1, the way `rimEdges` and `axis` are:
it had never been read by a person before, because nothing authored a stack that ends.

`tools/library-map.cs` puts two plots on the catalogue map from this — a disc and a cross — so the reading can
be walked as well as read, and the cross is there because an inner corner seeds ring 0 as surely as an outer
one does.

**Team tint (built).** `TeamTintedMaterial(block, neutral)` stamps a colour-by-damage block (clay, wool,
stained glass) with the owning team's colour — **the same 0–15 damage scale wool uses** (`BlockColors`), so a
team's clay wall matches its wools — falling back to `neutral` on a cell with no team. It is a **material,
not a wall feature**: it works on any bucket (a team-tinted rim or surface is just a theme that puts it
there) and composes inside a `LayeredMaterial` or a pattern, because the tint reads the owning team from the
shared `BucketContext`. The painter fills that context per cell from a `teamDamageAt(x, z)` map that `WorldBuilder` gets from
**`TeamTerritory`** — one shared ownership decomposition. It splits the terrain footprint into the
**canonical** islands (`IslandDetector`, the same 1-based ids `islands_json` and the configure canvas use)
and gives each island its team: a stored `intent.IslandTeams` value wins, else a spawn's team on the island,
else a wool's owner, else **neutral**. So a whole team landmass takes one colour and anchorless land (a mid
island) stays neutral. `IslandTeams` is populated **once, when it's cheapest** — the `/plan/compile` endpoint
pre-fills it on the canonical decomposition (so ownership is derived at plan time, not re-derived at export),
and the **configure step** overwrites it when the author clicks islands onto teams. Because every side keys
on the one `IslandDetector` id space, the tint the export paints is exactly what configure shows — no second
decomposition to drift. (This reproduces `BoardDeriver`'s ownership exactly: its islands are the same
connected components, and its captive/stepping-stone analysis refines the zone/mid grammar, not who owns the
land — verified cell-for-cell across the plan corpus.) The built-in theme states no tint at all — every
bucket is stone regardless of team; a theme whose wall is a team tint, such as the seeded Meadow finish
(neutral fallback: light-grey clay), is what puts a team's colour on the wall. What is *not* a theme choice
is the domain invariant — bedrock and every stamped structure stay untouched regardless (TP6).

The eventual theme file (a JSON extension) is exactly this record serialized: a `TerrainMaterial` per bucket
— any of which may be a team tint or a pattern that embeds one — plus the depth knobs, resolved per scope
(TP10).

**A scope is a layer and a cell, not a cell.** The board is painted one layer at a time, each storey against
its own surface, so a cell standing on two layers is painted twice — once per surface it carries. That is
what puts a gallery floor's turf under a deck's meadow rather than giving the deck the paint of what lies
beneath it, and it is why `TerrainThemeScope.ThemeAt` answers `(layer, x, z)` and
`SketchRasterizer.ShapeThemeOwners` keys `(layer, cell)`. Within one layer nothing changes: the smallest-area
shape still wins a contested cell. Across layers there is no contest at all — each surface shows its own.
The passes cannot tread on each other because of the stone-only invariant: a course a lower layer has already
finished is no longer stone. Where two layers genuinely meet flush, the one drawn first is what stands.

## 4. The cases — and the tests they drive

Each scenario the prototype separated is a test fixture; several are now written as `TerrainPainterTests`
(the void-edge stack, the terrain step, the depth knobs, the structure-facing edge), and together they cover
the model.

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

- **Approach-wall seam (TP6).** A bedrock approach wall straddling a seam between two pieces (real:
  `mirror-tiny-map-cliff` has two, `isolated-spawn` four): the barrier is never painted, the terrain either
  side of it is lined by `closed` (not by open, since the barrier rises above — not a drop), and the clay
  riser that a bare drop would earn is **absent** where the barrier seals the face. The cross-section is the
  oracle: bedrock barrier standing above the terrain, grass tops beside it, no clay buried behind.

- **Multi-level stack (the cross-section).** A vertical slice through terraced terrain: per column, bedrock
  / wall / rim / grass in the right order, buried stone under a step's drop, and a room column painted
  nothing. This is the readable oracle for TP4's riser bounds.

- **Bedrock floor and full room piece.** The negative control: y=0 and every stamped structure stay
  exactly as the terrain builder and stampers left them, because only stone was ever eligible.

## 5. Architecture — a shared core, modular themes

The whole point of the design is a **separation**: a geometric core that knows nothing about materials, and a
theme layer that knows nothing about geometry, meeting at one data type. That is what lets the system be highly
flexible (any depth knob, any pattern, any scope) without becoming tangled — each kind of change lands on
exactly one seam, and the core never moves.

**The runtime seam.** `TerrainPainter` runs **last** in `WorldBuilder.Build`, after the stampers, and
rewrites **only stone** — so bedrock and every structure are excluded by construction and a re-run is
idempotent. It reads inputs already in hand: the finished world (column heights + materials), the per-cell
surface grid (`BuiltTerrain.SurfaceTop`), and — only for scoped theming — the shape footprints the rasterizer
already walks. Running after the stampers is what makes TP6 free: the rooms, cubes and approach walls
are already non-stone columns, so "consult the stamps" is just "read the finished world."

**The four stages.** The pass is one pure function assembled from four separable stages, in pipeline order:

1. **Profile — the shared core (theme-agnostic).** Classify every stone column into neutral geometric facts
   (`ColumnProfile`): its `surfaceTop`, its plateau, whether each face drops (to void, to lower terrain, or is
   sealed by a structure — TP6), its membership of all three nested rim-edge tests (void · open · closed, the
   three a theme's `rimEdges` chooses between), its void/terrain drop floors, and its
   **perimeter arc** — the index around its landmass's outer void-facing boundary that a wall-run reads (TP13).
   The arc comes from splitting the footprint into connected landmasses and Moore-tracing each one's outline.
   Nothing here depends on a theme, so the same `TerrainProfile` serves every theme, scope and pattern. **This
   is the shared core**, and it is the only stage that touches geometry.

   The stage also measures the axis running the other way — the column's **inset**, how far *in* from the
   void-facing edge it stands, 0 on the edge itself and −1 off the footprint. The arc says how far *round* the
   edge a cell sits and the inset how far *in* from it: same pass, same struct, and the pair is what a band can
   be read along in either direction. Both come from one place, `GridBoundary` — `TracePerimeter` for the arc
   and `StepsInward` for the inset — which is the same pair a building's own plan reads for its wall runs and
   its floor zones, so a wall and the plateau beside it cannot answer at different scales.

   **The inward walk crosses an elevation step.** It runs over the whole footprint, seeding only from the
   geometric outer face, so a staircase of plateaus gets one set of bands running across the treads and up the
   hill rather than a set per tread. That is an authoring call rather than a derivation: the concept is reached
   for on flat ground most of the time, where the two readings coincide, and it is stated in this direction so
   that varying heights stay available rather than being ruled out by the seeding rule. The seed is the
   *geometric* edge either way, not the `rimEdges` toggle — a rim mode chooses which drops a rim caps, which is
   a different question asked further down the pipeline.

   **What reads the inset is `LayeredMaterial` under `BandAxis.Inward`**, described in full under *A band
   stack and its axis* above: a theme that names no axis resolves exactly as it did before the axis existed,
   and one that names `inward` reads its bands as rings. What is still missing is the *editor* — a rim stated
   as a sequence of bands rather than one material of width N is expressible in the theme JSON and not yet in
   the Theme phase (`B200`).

2. **Theme resolution — the scope layer.** A `Theme` is a data row: the bedrock mode, the `rimEdges` and
   wall-face knobs, plus a `TopBand` per top bucket (its material, depth and toggle) and a material for the wall and
   fill — each bucket's depth living with its bucket, not as a loose scalar. Scoping (TP10) resolves the theme
   **per cell**: `TerrainThemeScope` picks the **whole** theme of the narrowest scope over a cell — the
   shape's own theme, else the map default, winner-takes-all, not a field merge — reading the sketch shape
   footprints the rasterizer produces (a cell → shape → theme resolver, the `TeamTerritory` shape), so it adds
   only the lookup, no new geometry.

3. **Bands — the resolver (pure depth math).** Given `(Profile column, resolved Theme)`, compute the vertical
   band assignment: which y-range is bedrock, rim, wall, surface, fill. Every depth and toggle rule lives here
   and nowhere else (TP7/TP8/TP9/TP11/TP12) — bedrock claims the bottom, rim or surface the top, wall the
   exposed riser, fill the remainder; a disabled bucket reroutes down its fallback chain; each band takes only
   what the band above left. Output: an ordered list of `(yRange, bucket)` per column. One pure function,
   unit-tested per column against synthetic profiles.

4. **Materials — the painter (the pattern seam).** For each band, the bucket's `TerrainMaterial` resolves the
   actual block per `BucketContext` (`x/y/z`, bucket, depth-from-top, team nibble, perimeter arc) and writes it.
   The material is the plug-in point: `SolidMaterial`, `LayeredMaterial` (surface's grass-over-dirt, a wall's
   vertical bands), `TeamTintedMaterial`, and the patterns `VoronoiMaterial` / `NoiseMaterial` (area) and
   `WallRunMaterial` (perimeter stripes) — composable (each nests any material) and deterministic (hashed from a
   seed and the cell, never RNG, the same discipline as the rest of the generator). Adding a pattern was a new
   `TerrainMaterial` and nothing else; the whole graph serializes through `TerrainThemeJson` under one `kind`
   discriminator.

**Why the split holds.** Each kind of change touches exactly one seam:

| To … | touch only … |
|---|---|
| add a depth/toggle knob (TP7–TP9, TP11–TP12) | the `Theme` record + the band resolver |
| add a material pattern (TP13) | a new `TerrainMaterial` |
| scope a theme to a shape (TP10) | the theme-resolution lookup |
| change the geometry (a new edge kind, a new stamp to consult, TP6) | the Profile core |

The core never learns about materials; the materials never recompute geometry; the resolver is the only place
depths interact. The bucket vocabulary — **bedrock · rim · wall · surface · fill** — is the shared contract
every stage speaks. And the interface-relative principle proven for the approach wall (TP6) is the same one
that lets a per-shape theme resolve at a seam rather than over a shape interior: a cell is rim/wall by its
**geometry** and themed by its **owning shape**, so a boundary between two same-height shapes has no rim to
contest, whatever their themes differ on. Every stage is pure over explicit inputs, so each tests directly —
the Profile against the §4 fixtures, the resolver against synthetic columns, the specs on their own — with no
DB and no IO, exactly as the stampers do.

## 6. Extensions

The base model (TP1–TP6) and the per-column extensions below share one **resolution order** (`TerrainPainter.Resolve`):
the bedrock floor is claimed first (it sets how much stone a column even has), then from the top down the rim
(on an edge) or the surface stack (on an interior), then the wall fills the exposed riser below the rim to the
drop, and **fill** — the required base — takes every block no enabled bucket claimed. Every band takes only
what the band above it left and the bedrock floor always wins the bottom, so a short column runs out of stone
gracefully rather than overlapping. Two rules are orthogonal to the depth stack and both are built: the theming
*scope* (TP10) and the material *patterns* (TP13), which slot into the material seam.

- **TP7** *(built)* *Rim depth is configurable; the wall takes the rest.* The rim is the top **`Rim.Depth`**
  blocks of an edge column, not always one (2 or 3 are ordinary). The wall then occupies the remaining
  exposed height **below** the rim, down to the drop. The rim never overrides the bedrock floor: it is clamped
  to the stone available above bedrock, so a column with only one paintable course is all rim and no wall, and
  the bedrock course is never recoloured. Default `Rim.Depth = 1` — the base model. Depth lives on the bucket's
  `TopBand`, not a loose theme scalar, so the rim and the surface each carry their own.

- **TP8** *(built)* *Bedrock floor thickness is configurable, in two modes.* The bedrock floor (`BedrockSpec`)
  is that many blocks up from y=0, at least **1**, never more than the column is tall. It is claimed before
  anything else, so rim, wall, surface and fill all divide only the stone it leaves — and when the thickness
  equals the column height (a piece exactly as tall as its floor), **no stone remains and rim and wall stop
  altogether**. Two ways to set it: **absolute** (a fixed block count, `BedrockSpec.Absolute`), or
  **terrain-relative** (`BedrockSpec.TerrainRelative`) — you name the intended *terrain depth* (how thick the
  painted stone shell should be measured down from the surface) and the bedrock takes the rest of the column,
  `thickness = columnHeight − terrainDepth`, per column. Terrain-relative keeps the dressed shell a constant
  depth over uneven ground (thick bedrock under a tall plateau, thin under a low one).

- **TP9** *(built)* *Wall on terrain-to-terrain faces is a toggle.* The void-facing wall is fundamental, but a
  face between two **terrain** pieces is exposed to air too — the mid-island case where one piece docks a
  neighbour four blocks shorter shows raw stone on the taller piece's inward side. That face is wall when
  `WallOnTerrainFaces` is on: with `Rim.Depth = 1`, an adjacent height difference of **≥2** leaves, after the
  one rim block, one-or-more wall blocks on the taller side (a difference of 1 is covered by the rim alone, no
  wall). Off, only void-facing faces paint and internal risers stay fill/stone.

- **TP10** *(built)* *Theming is scoped: a map default, overridden per shape.* One theme covers the board as
  the **map default** — the lower layer, covering every cell no narrower scope claims — and a theme also
  attaches to a **sketch shape**, the higher one. A cell resolves to exactly one theme, **whole**, by the
  narrowest scope covering it: **shape assignment › map default**. A scope supplies a complete theme, not a
  field patch — every theme field already has a default, so "one theme applied" is the whole thing, and the
  lower layer is simply ignored for that cell.

  The resolution mirrors the team-ownership decomposition (`TeamTerritory` / `IslandTeams`): the semantic
  assignment is stored on the sketch layout, the geometry is resolved at export. The layout carries a **theme
  registry** (`themes`, `themeId → theme JSON`) and the **map default** (`mapTheme`), and a shape carries the
  id of the theme it takes (`shape.theme`). At export `TerrainThemeScope` walks the rasterizer's own shape
  footprints — the primary plus every orbit copy, since a mirrored image keeps its shape id — and hands the
  painter a per-cell `themeAt(x, z)` instead of one theme; because the band resolver and the materials already
  run per column against one theme-agnostic `ColumnProfile`, the per-cell lookup needs no new geometry. A
  layout that themes nothing resolves to a constant map default and allocates nothing per cell.

  **The scope is the shape, rasterized fresh at export**, so reshaping a shape moves its paint with it — which
  is the whole reason the scope keys on sketch geometry rather than on a plan piece frozen at compile. Where
  two themed shapes overlap the **smaller** — the more specific — wins; a subtract and a role-tagged structural
  shape are never scopes, since neither places terrain of its own.

  "Resolved at interfaces" then falls out with no special case: a shape's rim and wall appear only where
  **its** cells are edges (the profile already computes that per cell), a shape dead-centre among same-height
  neighbours has no edge and so no rim or wall the way an interior column does, and a seam between two
  differently-themed shapes is borne by the **taller** side's edge cell — so that cell's shape, and its theme,
  paint the seam, unambiguously.

  Authored in the Sketch tool's **Theme** phase (`docs/tools/sketch.md`): Create edits a theme against live
  previews, Apply assigns it to a shape or to every shape of an island, and the map default is named beside
  them. A plan states no paint at all — `themes`, `mapTheme` and `themeScopes` on a plan document are dropped
  on parse.

- **TP11** *(built)* *The surface is a layered stack with its own depth.* Not one block: an ordered run of
  layers (`LayeredMaterial`) claimed from the top of an interior column — the seeded Meadow finish's being
  **one grass over two dirt**. It has a total depth (`Surface.Depth`) like the rim, and the same clamp: it
  cannot descend past what the bedrock floor leaves (bedrock takes priority), so a shallow column drops the
  surface stack's **deepest** layers first and keeps the topmost. Below the surface, fill takes the rest.
  Default `Surface = TopBand(SolidMaterial(Stone), Depth: 1)` — a bare depth-1 band, not a stack; a theme
  states a layered surface by naming one.

- **TP12** *(built)* *Surface, rim and wall are toggleable; fill is required and is the fallback.* Fill always
  claims every block no enabled bucket took, so a theme is never partial. The fallbacks follow the treatments'
  roles: turn **wall** off and its riser blocks become fill; turn **rim** off and its top blocks fall to
  the **surface** stack first (an edge then reads as surface right up to the lip), and to fill only if
  surface is off too; turn **surface** off and its blocks become fill. So any block resolves to its own
  bucket if enabled, else the next treatment down that chain, else fill.

- **TP13** *(built)* *Buckets take patterns, not just a block.* A bucket's material can be a **pattern** that
  varies the block across the bucket's cells, at the same seam as a solid — each entry itself a material, so a
  pattern nests a team tint or another pattern. Six of them, in two families plus the wall's own.

  The **region** patterns tile the footprint with a jittered grid — one deterministic seed point per grid cell
  of period `CellSize`, every block belonging to the nearest region, pure per cell with no global precompute —
  and differ in what they do with that tiling. **`VoronoiMaterial`** takes an ordered list of `Bands` measured
  **inward from the cell boundary**: band 0 sits on the boundary and so draws the grid as one connected network
  of lines, each later band is a concentric ring further in, and the last takes the middle. A cell too small to
  reach a band never shows it, which is what gives cell size a meaning — small cells come out filled by whichever
  band they did reach. Depth is the Worley `F2 − F1` gap, whose contours are hyperbolic rather than straight, so
  the inner bands round off the cell's corners while the outline stays sharp. **`CellMaterial`** takes the same
  regions and gives each *whole* region one material from a palette, having first displaced the lookup position
  by a noise field of its own (`Warp`) and loosened the sites off their grid squares (`Jitter`). Where a voronoi
  draws a diagram, a cell draws a fabric: flat organic patches, any two of which may meet.

  The **field** patterns cut a fractal field into bands by an ordered ramp of N `Stops`, so `n` stops give `n`
  materials and only neighbouring stops ever share a boundary. They differ in one thing: how each octave is bent
  before the sum. **`NoiseMaterial`** leaves it alone — cloudy regions fading into one another.
  **`TurbulenceMaterial`** takes its distance from the midpoint, folding the field at every crossing into a
  crease: billowed, marbled bands. **`ElectricMaterial`** inverts that fold and squares it, so the crossings
  become thin branching filaments with everything else falling away from them. The sum is normalised by its own
  deviation rather than by its amplitude total, which is what keeps the spread constant as `Octaves` rises —
  dividing by the amplitude total averages independent samples, so the field crowded towards its middle and the
  first and last material an author named all but vanished (measured at 1.0% each at five stops, three octaves).

  Every area pattern — both region patterns and all three field patterns — carries a **`Rise`**: the vertical
  period of its field in blocks, or 0 for none. A pattern of the plane answers a whole column at once, so it
  decides the ground and leaves a wall face as vertical stripes, which is what a wall-run draws on purpose and
  what an area pattern drew by accident. A positive rise samples the field over the *volume* instead, at that
  vertical period, so a wall carries the same fabric its surface does. The period is separate from `CellSize`
  or `Scale` because terrain is a slab — hundreds of blocks across and a dozen tall — so cells as tall as they
  are wide would put barely one layer of them in a wall. It defaults to 0 for two reasons: it is the more
  expensive field (a volume voronoi searches the 3×3×3 neighbourhood, three times the sites of the plane
  search, and measures 3.4× the plane's wall-clock over a whole board's paint — 1114 ms against 331 ms), and on a surface one to three courses deep
  there is nothing for it to vary. It earns its cost on the buckets that are tall, which are the wall and the
  fill. A volume octave is measurably narrower than a plane one — trilinear interpolation averages eight
  lattice corners where bilinear averages four — so a risen field carries its own mean and deviation, without
  which its outer stops would starve exactly as the un-normalised fBm's did.

  **`WallRunMaterial`** is the wall's own: a list of `(material, width)` runs that repeat in order along the
  **void-facing perimeter**, reading the arc index the profile assigns each outer-wall column, so any number of
  stripes of any widths cycle continuously around every corner (a cell off the outer wall reads as arc 0, the
  first run). A wall's *vertical* bands up the riser are the existing `LayeredMaterial`.
  **`WallDiagonalMaterial`** (TP17) is that same cycle sheared: each course begins `Slope` cells further round
  the loop than the one beneath, so at a slope of one a stripe travels one cell along per course up — 45° on a
  square-blocked face — while larger slopes lay it flatter, a negative slope leans it the other way, and zero is
  the vertical run again. The shear reads world height rather than height above the footing, so two walls of
  unequal height standing together meet with their diagonals in line.
  **`CheckerMaterial`** (TP17) alternates two materials over squares `Size` on a side, laid **in the face it
  paints**: the perimeter arc and height on an outer wall, the two ground axes anywhere else. That choice is the
  same one `Rise` answers for the area patterns — world x and z everywhere would answer a whole column at once
  and come out as stripes down a wall rather than squares on it. Square indices are floored rather than
  truncated, since a truncating divide folds at the origin and seats two squares of one colour together there.
  **`LogCheckerMaterial`** (TP20) lays that same board with **one** log and varies how it is turned rather than
  what it is made of: upright on one square, on its side on the next, so the grain runs vertically and then
  across and a single block reads as a woven board. It is its own material rather than a checker over two
  solids because the two squares are one block and two orientations, and an orientation is not something a
  solid can carry — a log's data nibble *is* its axis, so a material resolving that nibble from the cell's
  coordinates would turn every log the same way and paint a flat patch of wall. **A log on its side lies along
  the wall, never across it**: the axis decides which two of its six faces are the sawn ends, and a log laid
  across a wall puts one of them straight out at the viewer. The wall's own run answers that — the third
  perimeter fact beside the arc and the turn (`GridBoundary.RunAt`), taken from the chord over the same window
  the turn is measured on. A **corner** has faces on both axes and no lying log shows bark to both, so it
  stands, which is what a timbered corner post is anyway. Off a wall there is no face to protect and the
  squares read as bark against sawn end, which is a log floor. The pattern is the timbering nearly every
  hand-built house on the corpus uses; `alpine_mining_ii` does it in acacia, and the rule that a horizontal
  log runs along the wall was read off its walls rather than guessed.
  **`WallFrameMaterial`** (TP19) inks the top and bottom courses and the corners of the shape the wall wraps,
  filling the panel between. A corner is a **turn**, not a change of direction — the profile measures each
  boundary column's bend over a span either side (`GridBoundary.TurnAt`, TP18), which cancels the raster's
  staircase and leaves curvature, so a vertex reads its exterior angle and one threshold separates a corner
  from a curve. A shape drawn round reaches it nowhere and the frame becomes its two courses, which is a layer
  stack. Every choice is a
  deterministic hash of a seed and the cell — never RNG — so a map exports the same pattern every time. The
  slice was cleanly separable exactly as planned: a pattern changes only *which block* a cell resolves to, never
  *which cells* are in the bucket, so TP1–TP12 (the geometry) were untouched — the one new geometric fact is the
  outer-perimeter arc (below).

- **TP21** *(built)* *A pattern samples the cell folded into the board's primary image.* A pattern is a
  function of position — a voronoi asks which site is nearest, a field asks what the noise reads — so on a
  mirrored board a cell and its mirror ask about two different places and come out two different blocks. The
  fault is not in any one pattern; it is in the coordinate every pattern reads. What a mirrored map claims is
  that its halves are the same ground, and the ground includes what it is made of, so a floor pattern that
  does not match across the axis and a middle that is not symmetric with itself are the map failing its own
  claim.

  The painter therefore hands each column a **sample point** (`BucketContext.Sample`) beside the cell it is
  painting: the cell put through `OrbitScatter.Canonical` — the same fold the dressing pass already scatters
  props on — which answers the one member of a symmetry orbit that stands for all of them. Every cell of an
  orbit samples that member, so every cell of an orbit resolves to the same block; a cell on the axis is its
  own representative and folds to itself. The fold is of the **plane** only, because no symmetry mode this
  studio has turns the vertical: a risen pattern (TP15) samples the folded column at its own height, so a wall
  carries the same fabric on both sides while still varying up its face.

  The cell being painted is untouched — `Sample` sits beside `X`/`Z` rather than replacing them — because the
  two are different questions. Where a block is written is a fact about the world; where its material is read
  from is a fact about the board's symmetry, and a context built off a world with no orbit to fold into (a
  style swatch, a house course, a board authored freeform) samples its own cell, which is the answer it had
  before there was a fold at all. A **team-tinted** material is the one thing that must *not* fold: it reads
  `TeamData`, which is a fact about the cell, so each side keeps its own colour under a pattern the two share.
  The fold is taken once per column rather than once per block, since it does not depend on height.

  The same point is handed to the two dressing materials resolved at world cells — a path's **pave** and a
  water prop's **bank** — so a cobbled road is cobbled the same way on both sides. A boulder needs nothing:
  it is built in its own local frame and turned into place, so its material was already asked in coordinates
  its images share.

## Rule catalog

| id | rule |
|---|---|
| **TP1** | The rim is one block — the top of an edge column, at `surfaceTop − 1`. |
| **TP2** | Edges are found over the 8 neighbours (void or lower), so a reentrant corner never gaps. |
| **TP3** | `rimEdges` picks the edge test: `void` (only where the footprint meets the void — no lip on the treads of a stacked body), `drop` (the default — void or lower), `boundary` (the full plateau outline, also lipping edges facing a room or a taller plateau). |
| **TP4** | The wall is the exposed riser, `y ∈ [drop, surfaceTop − 2]`; buried stone and the y=0 bedrock course are left. |
| **TP5** | The interior is the top course of every non-edge column. |
| **TP6** | A stamped structure (piece-relative room/cube, or the interface-relative bedrock approach wall) is height-bearing: never painted, never a drop (no open lip, no clay behind it), always a closed-rim edge. |
| **TP7** | Rim depth is configurable (`Rim.Depth`, default 1); the wall takes the height below it, and the rim never overrides the bedrock floor. |
| **TP8** | Bedrock floor thickness is configurable — absolute, or terrain-relative (bedrock = column height − intended terrain depth); ≥1, ≤ column height. When it equals the height, no rim or wall. |
| **TP9** | A toggle paints wall on exposed terrain-to-terrain faces (adjacent height difference ≥2 after the rim), not only void-facing ones. |
| **TP10** | Theming is scoped: map default › shape, winner-takes-all (whole theme). A `themes` registry + `mapTheme` + each shape's own `theme` on the sketch layout; a per-cell `themeAt` resolver (`TerrainThemeScope`, the `TeamTerritory` shape) walking the rasterizer's shape footprints at export, the smaller shape winning an overlap. Authored in the Sketch tool's Theme phase. Resolves at interfaces with no special case. |
| **TP11** | The surface is a layered stack to a configured depth (`Surface.Depth`; one block of stone by default, grass over two dirt in the seeded Meadow finish), clamped by the bedrock floor. |
| **TP12** | Surface, rim and wall are toggleable; fill is required and claims the rest. Rim off → surface, then fill; wall/surface off → fill. |
| **TP13** | Buckets take patterns, not just a block. Region: `VoronoiMaterial` (bands inward from the cell boundary — band 0 is the grid line, the last is the middle, a small cell never reaches it), `CellMaterial` (one material per warped region). Field: `NoiseMaterial` · `TurbulenceMaterial` · `ElectricMaterial` (an N-stop ramp over a fractal field, bent plain / folded / ridged; spread held constant across octave counts). Wall: `WallRunMaterial` (N stripes wrapping the void-facing perimeter arc). All deterministic, and each entry nests any material. |
| **TP14** | A theme is authored as a form, not as JSON: one section per bucket carrying its toggle, its depth and a material editor that switches the bucket between every kind and recurses into the materials a composite nests. Blocks are picked from `TerrainPalette` — the curated offer list, named and coloured by `BlockPalette`, so a swatch cannot promise a colour the export will not place — with the three sixteen-shade colour families offered as a colour row rather than forty-eight dropdown lines. The offer is not a restriction: a block it does not carry is typed as an id/data pair beside the name. The editor writes the theme node itself, so there is no second model of a material; every edit re-renders the server swatch through the real materials. |
| **TP15** | Every area pattern carries a `Rise`: the vertical period of its field in blocks, 0 for the plane. At 0 a column resolves to one block and the pattern decides only the ground; above it the field is sampled over the volume, so the wall and the fill carry the pattern too. Off by default — it is the more expensive field and a three-course surface has nothing to vary. A volume field carries its own octave statistics, so its outer stops hold. |
| **PT1** | *A block that surfaces ground is never painted below the course it surfaces.* Grass, podzol, mycelium and farmland are each exactly one course thick in the world — what lies under one is soil — so a bucket deeper than a course filled with one writes it into every course of its depth, and the ground comes out made of its own skin. The rule is about **depth**, which is where a theme's materials and its buckets meet: a `layered` material is a stack and may carry a surfacing block as its **top band at one course**, which is what the seeded Meadow finish's grass-over-two-dirt surface does; every other kind is a **pick**, so whichever block it picks fills the whole depth and a surfacing block cannot go in one at any depth over one. The fill claims everything under the buckets above it, so a surfacing block there is buried by definition. Refused where a layout is stored, alongside the house styles (`docs/tools/sketch.md`'s Refusals); podzol is answered by its variant, since it shares an id with the dirt under it. |
| **TP16** | `TerrainPalette` is grouped by **tone family** — the set of blocks that read as one ground with a texture — and a family is the unit a material's list is filled from: one entry per block, in the family's light-to-dark order, replacing what the list held. An author then removes the blocks that ground does not use, which is shorter than picking each one by hand. Only full cubes are named; an ore sits with its stone, planks are in where logs are out, and bedrock has no family because it is the map's floor and the shell of its walls. Where colour and use disagree the use wins — gravel is cobble, not grey stone, because it is laid at the water's edge. **A neutral family holds neutral blocks**, though: where a family's own colour is a grey every block in it has to read as one, since asking the picker for the pale grey and being handed a yellow-tan is the swatch lying about what it offers — which is why the two mushroom blocks sit with sand, whose colour they actually read as, and why a test holds every grey family to it with the two by-use exceptions named rather than derived. The same table names ground in the surface analysis, so what an author paints and what a report measures cannot drift apart — which is what makes the table's completeness load-bearing: `SurfaceReport` counts a full cube no family names as **unnamed material** and legends it magenta, so a block missing from a family reads as a fault in the board rather than as a hole in the vocabulary. Two things follow. A **data variant** of a claimed id is claimed too wherever it reads as the same ground — smooth sandstone beside sandstone, chiselled stone brick beside stone brick — and the **blocks a board's own fixtures are built of** are named, since an iron block left out put a permanent magenta speck on every board carrying an iron cube. |
| **TP17** | The wall's stripe cycle is also read sheared and squared. `WallDiagonalMaterial` starts each course `Slope` cells further round the perimeter than the one beneath, so a stripe travels one cell along per course up at slope 1 (45° on a square-blocked face), lays flatter as the slope rises, leans the other way when it is negative, and is the vertical run again at 0 — read off world height, so two walls of unequal height meet with their diagonals in line. `CheckerMaterial` alternates two materials over squares `Size` on a side, laid **in the face it paints**: the perimeter arc and height on an outer wall, the two ground axes anywhere else, since world x and z everywhere would answer a whole column at once and come out as stripes down a wall rather than squares on it. Square indices are floored rather than truncated, so the board does not fold at the origin. |
| **TP18** | A corner is a **turn**, not a change of direction. A wall exists only as squares, so a boundary that is not axis-aligned is drawn as steps and its direction changes at nearly every cell; the profile instead measures how far the boundary bends over a span either side (`GridBoundary.TurnAt`), which cancels the staircase and leaves curvature. Each boundary column carries that angle (`BucketContext.PerimeterTurn`) beside its arc, so a material asks one threshold and a shape drawn round reaches it nowhere. |
| **TP19** | `WallFrameMaterial` inks the top and bottom `Thickness` courses and the corners of the shape the wall wraps, filling the panel between. `Angle` is how sharp a turn (TP18) has to be to be inked, and the same number sets how far the ink wraps round each corner, because the measured turn ramps to a vertex rather than switching on at it. A circle reaches no usable threshold, so it has no corners and the frame falls back to its two courses — a layer stack, which is the right answer for a shape with nothing to pick out. A wall too short to hold two courses is all edge. |
| **TP20** | `LogCheckerMaterial` lays a checkerboard with **one** log and varies how it is turned rather than what it is made of: upright on one square, on its side on the next, so a single block reads as a woven board. Its own material rather than a checker over two solids, because the two squares are one block and two orientations and a log's data nibble *is* its axis. **A log on its side lies along the wall, never across it** — the axis decides which two faces are the sawn ends — so it takes the wall's own run (`BucketContext.PerimeterRun`, the third perimeter fact beside the arc and the turn); at a corner no lying log shows bark to both faces, so it stands. `LaidLogMaterial` is that pattern with one of its two squares taken away: the beam course running through the masonry everywhere. |
| **TP21** | A pattern samples the cell **folded into the board's primary image**, not the cell itself. Every pattern is a function of position, so on a mirrored board a cell and its image sample two different places and resolve to two different blocks — a floor that does not match across the map and a middle that is not symmetric with itself. The painter fills `BucketContext.Sample` with the cell put through `OrbitScatter.Canonical`, the one member of a symmetry orbit that stands for all of them, so every image resolves alike and a cell on the axis folds to itself. The fold is of the plane only — no symmetry mode turns the vertical — so a risen pattern (TP15) samples the folded column at its own height. The cell being painted is untouched: a context with no orbit to fold into (a style swatch, a house course, a freeform board) samples its own cell. A team tint does not fold, since `TeamData` is a fact about the cell and each side keeps its colour. The same point is handed to a path's pave and a water prop's bank; a boulder needs none, being built in its own local frame and turned into place. |
