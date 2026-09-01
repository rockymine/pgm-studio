# Decoration — flora, paths, scatter, canopy (G34, slice G161)

A third pass over the **realized world**, sibling to the structure stamping of `structures.md` and the
terrain painting of `terrain-painting.md`. Where the painter dresses the terrain's *material* — stone
unless a theme states otherwise, grass over stone and quartz on the rim in the seeded Meadow finish — this
pass adds the terrain's *life*: tall grass and flowers scattered on the soil, a worn stroke dragged across
it, boulders and trees seated on top. It is the second half of the
theming work parked as G34: G157 carved out the terrain slice ("no new geometry, only materials"); this is
the **prop-stamps** slice, and it is the opposite by construction — it exists to add geometry.

**All five tools ship: paths, ground cover, boulders and trees (G161), and water channels (G169, §7).** Water
is the one that changes the ground rather than standing on it — its carved bed is an elevation change the rest
of the stage never makes — so only its richer reads and the closed pond form remain open under `G169`.

**Dressing is placed, not sprinkled.** This is the stage's central decision and it took a rewrite to reach:
the first cut authored *recipes* — named density fields assigned to shapes, the way a terrain theme is — and
that is wrong for a prop. A tree is cover and a boulder is a wall, so where each one stands decides how the
map plays, and a decision about how a map plays belongs to the person making it rather than to a noise
field. So every part of this stage is a thing put somewhere: a route is dragged, an area of cover is traced,
a tree and a rock are clicked into place, and each carries its own knobs. The fields that remain are the ones
*inside* a drawn area — which blade of grass, which cobble — where placing them one at a time would be data
entry rather than authoring. The model was worked out against a live prototype whose every figure — the noise fields, the stroke variants,
the boulder elevations, the grown tree, the forest scatter — was emitted by the real algorithm rather than
hand-drawn; what it settled is stated here, and the C# is the authority for all of it. Rule ids here are `DR*` (dressing), local to this file the way
`structures.md` owns `WX*` and `terrain-painting.md` owns `TP*` — `rules.md` is compose-scoped and frozen,
so decoration law lives in the world-export docs, not there. The one exception is §3.1's clearance decline,
`OB19`: it fires from this pass's own ground read but it is a claim about an *objective*, so it takes the
`OB*` id `destroyables-and-cores.md` owns rather than minting a second family for one rule.

A note on the name. In this codebase **"decorative"** already means two settled things — the non-playable
floating masses the island detector prunes (dragons, birds), and the non-objective wool a map places for
colour. To keep those apart from *this* stage the code family is **dressing** (`DR*`, a `Dressing` pass, a
`DecorationStamper` is the one concession to the human word); the doc keeps "decoration" only as the title,
where the collision cannot bite.

Read alongside:

- `docs/world-export/terrain-painting.md` — the pass this one runs immediately after; the surface it reads.
- `docs/world-export/relief.md` — the elevation under all of it. Its §9 is what a channel owes ground that is
  not flat, which is the one part of this stage the flat-plane assumption gets wrong (`S46`).
- `docs/world-export/structures.md` §6.4 — the preset seam (style-as-data). A dressing style attaches here.
- `docs/tools/sketch.md` — the sketch document this stage stores its props beside.
- `docs/generator/ideas.md` — G34 (the umbrella), G32-C (structures & elevation, the sibling pass), G142
  (the roughen pass, whose noise operators the stroke edge borrows).
- `docs/world-export/ideas.md` — the dressing-stage gap pool: what turns these tools into one coherent
  stage. The fairness rule (G162) is answered here in §2; what remains is the guardrail budget,
  vertical-surface dressing, the arbitration contract, a biome theme, context-aware placement, and
  border/POI framing.

---

## 1. The pass's place

A generated layout compiles into a `SketchLayout` (geometry) and a `MapIntent` (objectives). The rasterizer
turns the geometry into a stone `VoxelWorld`; the stampers seat rooms, cubes and objectives on it; the
painter rewrites the stone surface into grass, dirt, quartz, clay. Every one is a pass in *realize*, and
they run in that order for one reason: each reads what the last produced. The dressing pass is the next
link — it reads the finished, **painted** world and the ground the map keeps clear, and it runs **last**,
after `TerrainPainter.Paint` in `WorldBuilder.Build`.

Running after the painter is what makes the whole stage tractable. The painter has already decided, per
cell, what the surface *is* — and the single fact the dressing pass needs is exactly that: soil accepts
flora, quartz does not; grass can be replaced by a stroke, a monument's wool cannot. So the pass reads the
top block of each column and the ground the map keeps clear, and never has to re-derive either. The one
elevation model it needs is an elevation per cell (`Dictionary<(int X,int Z),int>`, the first air Y above
each column), and the one it is given is **`BuiltWorld.Surface`** — `BuiltTerrain.Ground`, the tops of
everything on the board that is not a made thing.

**A made thing is not ground, and nothing that rests on the board treats it as one.** `BuiltTerrain.SurfaceTop`
answers the highest thing standing at a cell, and by now that is nobody's question: this pass reads
`BuiltTerrain.Ground`, the build ceiling reads the buildings and steps over the made things (`G6` amendment
25), and every placement that *seats* — a room's floor, a goal's box and the buried plate beneath it, a wall,
a build-region marker, the world spawn — reads `Ground` through `SurfaceFor`, whose fallback it is. That last
one was the half left undone: a cloud drawn at y78 over a car park is the top of every column beneath it, so
the goal there read the cloud and was stamped at **y83**, over a build ceiling of 68 that it had not itself
raised. A balloon flying
thirty blocks over a field is that field's answer, so a tree stated on it would seat at the envelope and
every column under it would read as built and take nothing at all. The ground beneath a floating thing is
exactly the ground an author decorates, so the surface the pass reads leaves the prop layers out. The same
elevation goes to `DressingScope.KeptClearAt` and to `MapExportComposer.CheckStructureSites` (`WX11`), which
would otherwise report a shed under a balloon as standing on a fifty-block plinth. The same fact is recorded
for the renders as `ProvenancePass.Made`, claimed after this pass rather than before it — what runs between
works on the terrain round a made thing, and a harbour that fills round a hull claims every column it filled,
which is true of the water and false of the ship.

**On a stacked board a prop says which layer it rests on.** That surface names the highest ground at a
cell, so a prop stated for a gallery floor would land on the deck over it. A prop carries an optional
`layer`, and `DressingContext.GroundFor` answers that layer's own surfaces — `BuiltTerrain.SurfaceFor` is the
same resolver for a stamped thing, so the two readers of a placement's layer cannot disagree about where it
is. Naming no layer keeps the top surface, which is where everything already authored goes; naming one the
board has no ground on is **declined** (`DR-LAYER`) rather than seated on the top, because that is exactly
the layer the author was saying they did not mean.

**A shape can say it is not ground to dress.** Everything else in the keep-out is read off the intent or off
the finished world's top block, and neither can see a wall or a crop bed drawn as *terrain*: the painter wrote
it with a theme like any other ground, so `KeepOut.Built` does not fire and its material says nothing. The
layout carries the answer instead — a shape marked `keepClear` (`docs/tools/sketch.md`) puts its own columns
in the mask as `KeepOut.Structure`, exactly and with no margin, so a road still runs to a gate while the wall
either side of it keeps its top course. Without it a stroke repaints whatever it crosses and a channel, whose
water line is the *lowest* surface its band crosses, cuts every other column in the band down to that line —
which on a wall standing seventeen courses over a river is a hole through the wall, not a bank.

**The claim book is per layer for the same reason.** `GroundClaims` is keyed on the layer as well as the
cell, and each placement is handed one layer's view of it — `claims.On(prop.Layer)` — so a channel carved
into the ground holds the columns it cut on the ground and none of the columns above them. Two props share a
cell only where they share a layer, which is the only case in which they can collide; a tree on a floating
island and a river under it stand on different ground and neither is `DR-CLAIM`. A prop naming no layer
claims on the top surface, which is a layer like any other here, so a board with one layer has one book.

The break from the painter is the geometry. `TerrainMaterial.Resolve` only ever answers *which block* a
stone cell becomes — it cannot add a cell. Flora, boulders and trees **are** added cells: a tall-grass
block in the air at `SurfaceTop`, a rock volume, a trunk and canopy. So the dressing pass is not a new
material — it is a sibling pass that calls `VoxelWorld.SetBlock` (and `AddTileEntity` for a potted or
NBT-bearing prop) above the surface. That it runs after the stone-only painter also means it can place
non-stone freely without the painter overwriting it.

## 2. What every part shares

The parts below read as four features but run on the same handful of primitives, which is the argument for
one stage rather than four:

- **Noise.** The same deterministic `PatternNoise` (`Hash`/`Unit`/`Value`/`Fbm`) the terrain patterns are
  built from. A density field decides where flora goes; a low-frequency field gathers flowers into meadows
  and trees into groves; a per-cell `Unit` is the dice a worn stroke rolls. Deterministic hash-from-cell,
  **never RNG** — the discipline `terrain-painting.md` §5 already holds, so a map re-exports identically.
- **Mask.** Eligibility from the painted surface (soil vs. quartz, read from the top block) and from the
  **keep-out** `DressingScope.KeptClearAt` builds: spawns, wool rooms, stated structures, built columns,
  the sketch shapes that marked themselves `keepClear`, and every door's approach — the whole of it for
  anything that stands on ground, and only `KeepOut.Structure` for a stroke, which stands on nothing (§3.1). Nothing lands where it would break play or read wrong, and the mask answers *which*
  of those held a cell (`KeepOut`), because a decline that cannot name what stopped it is one nobody can act
  on. This is not the map contract's `protection`, which is a region rule about what a player may enter and
  break; it reads a spawn's protection areas but what it answers is the other thing. A route's own cells join the mask as it is laid, so nothing grows through a road; a stroke that is paint claims nothing and is planted over.
  A destroyable and a core are **not** in that mask, and what they ask for instead is §3.1.
- **Placement.** A **point** for the props that stand somewhere (a tree, a boulder), a **drawn outline** for
  the ones that cover a stretch (a route along a line, cover inside a ring), and a **dragged rectangle** for
  the one whose stamp takes a footprint (a building, §8). Three interactions, and the split is the model's: a
  marker is a click because a spot is a click, an area is a trace because tracing is how a stretch of ground
  is described, and a rectangle is a two-corner drag because that is what a box is. A marker seats on the ground, so it can only be dropped **on the
  rasterized terrain** — the canvas refuses a click over a gap or off the map (a red ghost, no drop), because
  the stamp below would refuse it anyway. An **area** has no such limit: a route or a channel may be drawn
  across a void, and only its cells that land on real ground do anything.
- **Reshaping.** A placement ends its tool: the prop is selected and the canvas returns to select, so the next
  click picks the thing just put down rather than dropping a second one beside it. A selected prop wears one
  square grip per point — per traced point on a route or an area, on the anchor for a marker — and dragging one
  moves that point alone, block-snapped, with the band or the outline following it. A marker's grip obeys the
  same terrain rule its click does. Reshaping never changes how many points a prop has: a route drawn once is
  corrected in place rather than retraced.
- **Stamp.** A 3-D volume seated on `SurfaceTop` and written cell-by-cell with `SetBlock` — the
  shape-mask-in-a-box `ObjectiveStamper` already uses for destroyables and cores. A prop seats on the
  *lowest* column of its own footprint, so it sits into a slope rather than floating over the low side, and
  it refuses ground that is missing or kept clear rather than half-placing itself. What it *rests* on needs
  real ground; what it merely reaches over does not — a crown or a boulder lobe may overhang a drop or a void,
  which is why a marker can seat at an island's edge and still lean out past it.
  **Only the feet are asked about, and the rest is written wherever it meets air** — a cell of the prop
  standing where something already is is skipped and nothing else happens. That is the right rule for a prop
  meeting terrain, and it is why clearing the seat is not the same as fitting: the ground a building holds
  reaches one block past its stamp (`DR-CLAIM`), which keeps a stem out of a wall and says nothing about a
  crown eight blocks wide. `DR-CUT` is what closes the gap, and §2.1 is what it draws the line on.
- **The fan (G162).** Every prop is placed once and stamped at **every image of its orbit**, in the prop's own
  local frame, with each offset **turned** by that image's transform. An author draws one half of a map and
  gets a fair one, which is the contract the layout itself has had all along — and the canvas draws the
  images as ghosts, so half a map is never authored blind. Within a drawn area the one gameplay-affecting
  species, two-block grass, is decided on the orbit representative (`OrbitScatter.Canonical`) so the same
  ground is tall or bare for every team, while the flowers beside it stay free: two identical flower beds
  read as a glitch and decide nothing.

  Fanning the *site* alone is not enough and this is the mistake worth naming: mirroring only the anchor
  leaves both teams with the same unmirrored prop shape, so a boulder with a lobe to its east has a lobe to
  its east on both sides of the map. The prop has to be built as offsets from an anchor and the offsets
  turned, which is why every generator here answers in a local frame. An offset is a delta between cells,
  and the delta between two mirrored cells is just the mirrored delta — so the turn is a plain rotation with
  no half-cell correction, which the anchor's own mirroring has already applied. A stamp is all-or-nothing
  per image: if one image's ground is missing or kept clear, that image is skipped rather than clipped.


### 2.1 Being clipped, and being cut in two (`DR-CUT`)

A prop written into whatever air is left comes out in one of two states, and only one of them is a fault.

**Truncated** is a prop with a face taken off it. A rock pushed against a wall fills the air beside the wall
and stops; every block it places still stands on the ground or on another of its own blocks, and what a
player sees is a rock against a wall — which is what an author putting a rock against a wall meant. It can
cost a great deal of the prop and still be right: measured against a wall two blocks off its centre, an
erratic of size 7 loses **36% of its 1,089 blocks** and severs none of them.

**Cut in two** is a prop whose obstruction took out the blocks that joined a piece of it to the rest. A limb
runs into a wall, the cells inside the wall are skipped, and the far half of that limb — still in open air,
so still written — now stands with nothing under it and nothing beside it. Nothing declines, the tree is in
the world, and there is a piece of tree floating next to a building.

The second is what `DR-CUT` names, and it is counted against the prop **as it would have stood on open
ground** rather than in absolute terms, because a prop is not obliged to be one piece to begin with: a crown
gathered at the branch tips is several. What is asked is which of the parts that *were* joined to the feet no
longer are. Face adjacency, because a block joined to its neighbour at a corner alone has air on all six of
its own faces and is seen straight past.

The threshold is `DressingRules.ClipSevered` blocks cut away, not a share of the prop — a share is the
truncation measure, and it is the boulder's answer rather than this one. Against a wall taller than the tree:

| prop | clearance from the wall | blocked | cut off | `DR-CUT` |
|---|---|---|---|---|
| grown oak, height 8 | 2 | 8 | 6 | — |
| grown oak, height 12 | 2 | 33 | 3 | — |
| grown oak, height 20 | 2 | 57 | 79 | raised |
| grown oak, height 20 | 8 | 1 | 39 | raised |
| grown oak, height 32 | 2 | 140 | 162 | raised |
| grown oak, height 32 | 8 | 23 | 43 | raised |
| erratic, size 4–10 | 2 | 51–1,060 | 0 | — |

The height-20 row at eight blocks' clearance is the whole case for the rule: **429 of its 430 blocks are in
the world** and a limb is floating. No measure of how much landed can see that, which is why the finding is
raised off what was severed and reports both numbers.

A prop is left exactly as the clip left it, floating piece included. This is a complaint: the author moves
the prop or makes it smaller, and neither is a decision the pass can take for them.

## 3. Flora overlay — the paint-aware overlay (`DR-FL`)

The plainest decoration is WorldEdit's `//overlay`: put 70% air and 30% grass-and-fern on top of
everything. The studio can do better than *everything* because it just painted the surface. Grass, fern
and flowers belong over grass-block, dirt, coarse dirt and podzol, sparsely over sand, and nowhere over the
quartz of a plaza or the wool of a monument. The overlay is masked by the paint beneath it — the eligible
set is a property of the top block, read the way `TerrainProfile` reads a column.

Two knobs do the work, and both are noise. A **density field** decides which eligible cells get anything:
white noise gives an even TV-static speckle, value noise (`Value`/`Fbm`) clumps into meadows and clearings,
blue noise spaces plants so no two touch — the look of deliberate placement. A flat threshold on the field
is the line between a sparse verge and a lush meadow. Within the vegetated cells a **species mix** decides
what: a second, low-frequency field paints flower *fields* — poppies and dandelions cluster where it peaks,
so the map gets meadows of colour instead of confetti, with grass and fern filling the rest by a share
noise. A cell that the density field rejects simply stays air; that air is the "70%".

`DR-FL` walks the cells inside a **drawn outline**, and for each one whose top block is soil and whose
density field clears the threshold, sets a plant into the air cell at `SurfaceTop`. The outline is the
authored part and the field is what fills it: nobody wants to place nine hundred blades of grass, and nobody
wants grass everywhere either. It adds one block per cell and never touches the ground — the lightest of the
four passes, and the one that reuses the most. `FloraSpec` carries the knobs: `Coverage` against the
density field, `Scale`/`Octaves` shaping it, and `FernShare`/`FlowerShare`/`FlowerScale`/`TallShare` mixing
the species. `DressingPalette.SoilShare` is the eligibility read — sand takes a fraction of what grass does,
quartz none.

The split that matters here is not a species one. Grass, fern and flowers are one block tall and walked
straight through, so they are **cosmetic** and scatter freely; the two-block `TallGrass` and `LargeFern`
break a sight line and are marked **gameplay**, which routes them through the fan of §2. A plant's
`PropClass` is declared on the palette row, not inferred at placement.

### 3.1 The ground a goal is read against

A destroyable and a core are the two objectives with no room and no protection region, so nothing else holds
ground around them — and what they need held is not the same thing a spawn needs. A spawn's ground is
**forbidden**: a prop there breaks play. A goal's ground is **kept open**, which is a narrower claim. Grass,
fern and flowers grow across it and under a floating monument exactly as they grow anywhere, because none of
them changes what a player can see or reach, and a monument standing in a ring of bare dirt reads as a
diagram rather than as a place.

What may not stand there is **cover**. `DressingScope.GoalGroundAt` is every block the structure covers grown
by `GoalClearance` (**4**), and the flora pass declines to raise a tall plant inside it — growing its short
cover on that cell instead of skipping the cell, so a meadow stays continuous across a goal. The footprint
comes from the box the stamper wrote wherever there is one, for the same reason the emitted region does
(OB8): the ground kept open is then the ground the structure occupies by construction, rather than by two
derivations agreeing. `ObjectiveFootprint` answers for an intent that has not been through a world build.

A tree, a boulder or a building inside that ground is a different case, and it is **declined** (`OB19`,
`DressingScope.GoalClearanceAt`): the prop is not in the world, the finding names its kind, its id, the cell
it rested on and the goal it reached into, and the map still exports. A goal is what the map is for and a
prop is removable — which is the whole difference from `OB17`, where the goal itself is what stands wrong and
there is nothing to drop. The mask is wider than the cover one: the goal's own ground plus a `GoalStandoff`
square about the marker. Because the pass fans a prop before it sites it, a clearance only one team's mirror
reaches still drops the whole prop — a rock standing on one half of a mirrored map and missing from the other
is worse than neither. The clearance
is why the refusal is wanted at all: an objective is the one thing on a map that wants its approach legible,
so a defender can see what is coming and an attacker pays something visible for arriving. For those three
props the kept-open ground reaches further than the cover clearance: never nearer than **ten blocks to the
goal's marker** (`DressingScope.GoalStandoff`, the author's radius) — the ring a fight happens on, measured
from the marker rather than from however wide the structure under it happens to be.

**A door's approach is kept clear, and it stops a prop rather than refusing a map**
(`DressingScope.ApproachAt`, part of `KeptClearAt`). The ground in front of a spawn room's door — **twenty
blocks** out from the stamped building's own face, the wall's width — and in front of each wool-room entry —
**ten blocks** — is in the keep-out mask, so a prop authored into a lane never lands: the pass declines it and
says so (`DR-KEEP`), and the world builds. Every kind is turned away, boulders included; the earlier
low-cover carve-out for a boulder does not survive the size a boulder actually reaches, and a mask that reads
a prop's kind before deciding whether a lane is a lane is a rule nobody driving the studio can predict. The
lane is measured from the room resolution the stamper actually builds
(`WorldBuilder.SpawnRoom`/`WoolFrame`), deliberately not from the protection region projected around
it: the ruling is about the building and the way players walk out of it. A legacy wool cage with a door per
wall keeps the lane on all four faces.

**A building obeys the approach and nothing else in the mask.** The rest of what is kept clear — a spawn's
own margin, a wool room, a stated structure, a stamped column — never turns a building away, because someone
drew that rectangle where it is and a room's margin is not a reason to lose it. The lane is the exception,
and the reason is size: a building is the one prop big enough to close a door's approach entirely.

**And a stroke obeys the stated structures and nothing else** — the mirror of that, and for the mirror
reason. The mask is about things that *stand* on ground: a trunk in a doorway, a boulder inside a spawn's
protection, a house across the lane. A stroke stands on nothing. It replaces one course of finish, and in
front of a door it **is** the lane the approach is being kept clear for, so a road held to the whole mask
stops twenty blocks short of every spawn and tapers away as the approach rect cuts across it — `opus5-slipway`'s
spawn road paved 228 cells, narrowing from seven wide to one and ending twelve blocks from a room it is
authored to reach. What a stroke must still respect is `KeepOut.Structure`: a wall, an iron
cube, and a shape that marked itself `keepClear` — repainting the top course of a town wall or a flight of
stairs is exactly what that word exists to prevent. A stamped column needs no entry in the mask, because the
pass tests the block under its own feet per cell and with no margin (§4), which is the sharper reading of the
same question: the road runs to the wall and stops **at** the wall. The same road now paves 367 cells at its
full width, into the room's own doorstep.

## 4. Strokes — drag a line, replace the finish (`DR-PA`)

A stroke is drawn the way the lasso is: press, trace, release. Where the button comes up is the last point,
which is the whole interaction — there is no separate way to finish and so no way to get stuck mid-draw. The
drag is one point per block of pointer travel, so it is simplified on release to the points at real bends
(the *open* Douglas-Peucker, not the ring simplifier: a stroke has a direction and the ring version would
reorder it).

What is stored is that line and a radius, and nothing else. Every cell within the radius of the nearest
centerline segment takes the stroke's material, **replacing the surface finish beneath it** — a stroke adds
no cell. It is a finish laid over ground that already exists, so it can run over a slope without becoming a
ramp, and a bridge across a void stays the draw phase's job.

**A stroke says whether it is a route, and the style does not say it.** The five styles below shape the
*band* — that is a brush, and a brush says nothing about whether players read the result as a way through. A
gravel tongue over a crag and a road between two spawns can be the same brush at the same radius. So the
document carries one more word, `route`, and it is **off by default**: a route claims the cells it covers and
paint claims none. Everything below about claims and standoffs is a route's; a painted forest floor is ground,
and things stand on ground.

Why the default falls that way is a measurement. `DR-ROAD`'s distances are reasoned about a road — a canopy
closing over one stops it reading as a road through trees — and are meaningless for a smear of dirt; asked of
every stroke, twenty-one of them over a 110 × 220 board left **eleven** plantable cells on the whole map. The
gate was not wrong about roads. It was being asked of the wrong thing, which is why the answer is a word on
the prop rather than a waiver on the gate: a waiver is for a gate that is right and an author deliberately off
the norm, and it would have hidden this for every later board.

**A route's claim holds against the scatter, never against a building.** Strokes are laid first, and a road is
meant to run to a porch or a door — so a house drawn across the pavement stands, its floor takes the ground
inside its walls, and the stroke simply ends where the wall does. What the claim was ever for is the props
above the buildings: a trunk, a rock or tall cover in the middle of the route is still refused, because a
route with a tree in it is not a route.

**The road reaches further than its pavement: a standoff, stated per prop kind (`DR-ROAD`).** A trunk against
the kerb reads as trees in the road rather than a road through trees, so a tree keeps **three blocks** of
clear ground between its resting cells and the nearest cell a route claims, and a boulder — no canopy, but
still a wall beside the route — keeps **two** (the author's numbers). The rule is the kind's, not the stroke's:
`PlacedProp.RouteStandoff` names each kind's distance, `GroundClaims.NearerThan` measures it (Chebyshev,
strictly-nearer-than, so a trunk exactly three off stands), and the seat check refuses a breach with the
offending cell and the rule id in the drop report, so `GET /api/rules?rule=DR-ROAD` answers what the census
cited. Everything else — cover, water, buildings — states zero and may run right up to the pavement.

The single solid band is the boring case; the imperfect paths are the point, and all five of them are the same
distance field with one extra gate (`Geom.PathStroke`):

- **Solid** — `dist ≤ R`, a clean utility road.
- **Worn** — and a per-cell dice below a coverage threshold: gravel scattered thin, a trail rather than a road.
- **Rough edge** — `R` perturbed by an `Fbm` sample, so the band's outline is organic, not ruled. The same
  operator family as the G142 roughen pass's edge displacement.
- **Stepping stones** — discs sampled at intervals along the centerline's **arc length**, with gaps between:
  the disconnected band, stones across a void.
- **Tapered** — `R` varied along the arc (fat middle, thin ends).

What a style decides is the **band**; what fills it is the stroke's **pave**, and the pave is a full
`TerrainMaterial` — a solid, a cell fabric, a noise ramp, any pattern the painter offers, edited by the same
`MaterialEditor` a theme bucket is. The two are independent, so a worn cobble and a solid cobble are both
sayable, which they were not while the tiling was a mode of the stroke. A cobbled road is now a `CellMaterial`
at a three-block patch size — the same jittered grid the old style tiled by, said in the vocabulary every other
finish already used. It resolves at the symmetry fold the painter uses (`terrain-painting.md` TP21), so the
images of one stroke are paved alike rather than each falling where its own noise falls; the channel's bank
below does the same. A boulder needs neither, being built in its own local frame and turned into place.

Being a *fill* rather than an *outline* is what makes all five possible. An earlier cut made a stroke a
`SketchShape` whose closed ring was rasterized as terrain, and two of these could not be expressed that way
at all — worn and stepping stones gate cells, not a boundary — so they had to be filed rather than built.
Placing the stroke in the pass instead costs nothing and gets them back, because the pass was already writing
cells one at a time.

The **outline** still exists, in `Geom.PathBand`, and does a different job: it is what the canvas strokes to
show where a route runs. The two deliberately differ — an outline cannot draw a gap — so the preview shows
the corridor and the fill decides what within it is paved.

## 5. Boulders — erratics, standing on the ground (`DR-SC`)

A boulder is the first decoration that is genuinely 3-D, and it is the same shape-mask-in-a-box the
objective stampers already build. Seat a `BlockBox` on `SurfaceTop` (via `SurfaceYOver`), then fill the
cells that pass an ellipsoid test — `((x−cx)/rx)² + (y/ry)² ≤ 1`, the squared-distance mask `StampCore`'s
`Inset` and `StampDestroyable`'s `InPlusSection` are the precedent for. The finish is a material and a
micro-mask: stone, andesite, mossy cobble, blackstone — and moss creeping onto the top-lit faces, itself a
tiny `Unit` mask, so the finish carries its own micro-flora.

**What a boulder is, is a glacial erratic** (the author's ruling): a mass a glacier carried and left, large,
rounded but irregular, standing on the ground with weight. That decides its proportion, its seating and its
surface, and each of the three is a separate statement in `BoulderShapes`.

It stands on the ground rather than emerging from it. `BoulderShapes.Bed` is the share of the rock's height
below the surface — **0.30**, so its middle is lifted clear of `y = 0` and only its foot is under. That is
enough that no course shows daylight beneath it and enough to seat it on a bank; sinking the middle to the
surface instead halves the rock and leaves a dome the full width of the thing and a third of its height,
which reads as a knuckle of bedrock rather than as something carried here. The one form that genuinely
emerges is the **outcrop**, and it is the one whose middle stays at the surface. The lift also puts the
widest course a little above the ground, so a rock overhangs its own foot the way a perched erratic does: on
a size-7 round rock, 23 of its 151 footprint columns stand over air, none by more than three courses.

It is big. A rock's `size` is its reach from the middle and runs **2 to 10**, a default of 4 — a rock a
player takes cover behind rather than a stone they step over. At size 7 an erratic fills about 1,100 blocks,
stands **10 courses** over the ground and measures **15 across**; at 10 it is 3,100 blocks, 14 courses and
22 across.

Its silhouette is its own. `BoulderShapes.Of(form, size, seed)` answers three lobes — a main mass, a haunch
at its foot and a shoulder over it, the latter two thrown out on bearings hashed from the rock's own seed —
so the plan outline is a rounded irregular blob and the elevation leans, and two rocks of one form and size
standing near each other are two rocks. `Geom.Blob` fills them: a quadric eroded by a noise field sampled in
the lobe's own frame at a **scale of three blocks**, which weathers the surface into facets. A field that
turns over every block chews the whole surface at once — the result is lumpy rather than eroded whatever the
amplitude, and at the amplitude an angular rock wants it detaches chips: an `angular` rock of size 7 came
out in three pieces with two blocks standing in mid-air at (−308, 10, 48) and (−312, 11, 51).

A `BoulderProp` is placed at a cell and carries its own form (round, angular, outcrop, cairn), size, rock
material, moss flag and seed. Round and angular are the same erratic at two erosion amplitudes. The rock is a
full `TerrainMaterial` like the stroke's pave and the channel's bank, resolved in the boulder's **own frame**
rather than the map's — offsets from its anchor, before it knows where on the map it goes. That is what keeps
a mirrored pair one rock: resolving against map coordinates would give two teams the same shape in different
colours, which is the thing the whole fan exists to prevent. Depth is measured down from the rock's own crust,
so a layer stack reads as a weathered skin over a core rather than as the terrain bands it names anywhere
else, and the moss mask is laid over whatever the material resolved. A boulder is a solid volume standing on
the ground, so where it stands is cover, which is why it is placed rather than scattered.

## 6. Trees — copied and grown (`DR-TR`)

The trivial tree copies vanilla: a trunk of a known height and a canopy of a known profile, parameterised
per species — oak's blob, birch's tall slim crown, spruce's layered cone, acacia's leaning flat top,
jungle's broad canopy, dark oak's two-wide trunk. Each is a parametric template (trunk height, canopy
radius, canopy profile), not a copied schematic, and an oak grove is genuinely all most maps need.

The interesting tree is **grown**, and the shape that reads as a tree is neither a fractal nor a straight
pole — a recursive brancher reads as a fractal, and one clean spline reads as a mast. The model that works
takes both halves. The skeleton is **recursive** — the ez-tree knobs of levels, children per level, and a
per-level radius and length taper, with a gentle upward *force* — but **every branch is a gnarly stepped
path smoothed into a flowing Catmull-Rom curve**. The trunk carries a slow noise sway so it wanders like a
bonsai (up, twist, straighten) rather than rising dead straight; the limbs carry a sharper per-step jitter;
both are pulled gently upright. Limbs branch to a **second degree**, and the leaf clusters gather on the
outer tips. The trunk is a **continuous central axis**, not a stub that forks and stops: a **leader** knob
sets how far it climbs — at a low leader it dissolves into a spreading fork (a decurrent oak), at a high
leader it carries on up through the crown as one dominant spine, thinning and twisting, with branches
staggered up its length and a small fan at the top (an excurrent birch or conifer). A **stems** knob gives
the base setups a real tree has — one stem, a double, or a triple. A **height** sets the tree's size in
blocks, and it is not a uniform scale: a smaller tree carries a **thinner stem at the foot** and **fewer
branches** (a branch shorter than a floor length is terminal, so recursion stops sooner), and its leaf
clusters shrink with it — a sapling is a few clusters on a thin stalk, not a shrunken big tree. The rest
(leader, trunk flow, branch angle, levels, leaf size) are hash-keyed so a seed always grows the same tree. This is the ez-tree lesson (Dan Greenheck) — a tree reads from **taper + curve + a canopy at the
tips**, not from branch count — married to the Catmull-Rom flow a Minecraft builder draws by hand in
Axiom's path tool. The stage renders it two ways: the **spine** (the centerlines and their thickness, each
limb spline in its own colour) is the view the shape is designed against, and the voxelised blocks are the
swept-disc fill of §4's stroke lifted one dimension (a capsule along a spline instead of a band along a
line), so the tree and the stroke share one rasterization primitive.

The crown is where a naive generator gives itself away — a spherical brush with holes punched in it reads as
one blob, and you cannot tell which branch a patch of leaves belongs to. So the crown is placed the way a
mapmaker does it by hand: **one disc-shaped cluster per outer tip**, seated *on* the tip so the branch it
hangs from is inside it, and **sized by the branch that carries it** — a short limb holds a small clump.
Clusters are **small and many** rather than large and few, because a hand-built crown is lace: 1.7% of its
leaves are enclosed on all six faces and each carries about six occupied neighbours, and a puff two blocks
across is lace by construction where a wide one is a solid whatever is done to its surface. Neighbouring
clusters keep a **seam of air** between them: a cell fills only when it clearly belongs to one cluster
(nearest-cluster ownership, the seam where two are equidistant), so a viewer still reads each patch as its own
branch's instead of one merged mass. Most of the air is those seams; a little of it is perforation inside a
cluster, which at this size ragged-edges a puff rather than hollowing a ball. Whatever the fill leaves behind,
**only foliage that reaches wood through foliage is emitted** (`TreeCrown.Rooted`) — a leaf floating in the air
is not made rare, it is made impossible. A few short strands hang below each disc for a broken lower edge.

The laterals come in two arrangements, and they are two trees rather than two settings of one. **Staggered**
puts one branch at a time up the trunk, spiralling — every broadleaf. **Whorled** gathers them into rings a
fixed 5.2 courses apart, each ring shorter than the one below and none of them forking — the conifer, whose
cone comes from the ring lengths and from each cluster being sized by its own branch.

Both trees are the same stamper as the boulder: a trunk-and-limbs volume plus a leaf mask over a box, and
both end at one place that turns those cells into blocks — so the wood, the no-decay bit every leaf carries,
and the all-bark orientation every log carries, are decided once. A built tree's wood is scenery rather than a
felled trunk and its limbs run every way, so each log takes the all-bark variant (`LogAllBark`, bark on all
six faces) instead of the pale end grain an upright log shows where a branch turns; the wood it paints as still
reads through the low two data bits. `TreeTemplate.Build` answers the vanilla tree's wood and leaves from a
`TemplateShape`, its canopy a radius per course from `CanopyProfiles` (profile-as-data, the same seam
`BoulderShapes` uses for a rock's form). `TreeSkeleton.Grow` answers the grown tree's limbs and tips from a
`TreeShape`, `SweptVolume` fills each limb as a capsule along its spline, and `TreeCrown` places the
clusters and answers which one owns a cell.

A `TreeProp` is placed at a cell and names which of the two it is. **The forms are two trees, not two
settings of one**, and collapsing them is a mistake worth naming because it was made: six presets of the
grower, one per species, offer six silhouettes and build one. The grower has no notched cone and no flat
umbrella in it — there is no knob for a canopy that steps in as it rises — so a "spruce" preset is the
grower's own crown wearing spruce blocks, which is exactly the promise a drawn picker exists to refuse.
So a template tree names a **species** (`TreeSpecies`: its wood, its canopy profile, its proportions) and
scales it by height, while a grown tree names a **wood** (`TreeWood`: the log and leaf blocks) and is shaped
by the knobs above. Wood is the one thing neither form decides for the other, which is why it is its own
row and why the grown tree's picker is six cards of the same tree in six colours — where the species
picker's six cards must differ in shape, or they are one tree wearing six palettes.

A grove is therefore a handful of trees an author placed rather than a density field, and that is the
intended trade: a forest that clumps by itself is quicker to get and impossible to aim, and a treeline
across a lane is exactly the thing worth aiming.

The crown described above is the intent; `tree-corpus.md` is the measured record of what the code builds,
against 75 hand-built trees. It reports the gap — the cluster seated beyond its own branch tip, the missing
tiers — and the thresholds a generated crown can be gated on. It reads the wood the same way, with the
foliage disregarded: a generated limb leaves the trunk at 41° off vertical where an author's leaves at 59°,
and reaches half as far relative to its own trunk; the wood carries half again as many neighbours per block
as an author's and barely thins on the way out. It also locates why generated twigs come apart — a swept ball
between radius 0.5 and 0.866 can fill no cell at all.

### 6.1 Read as a point, not a mass

The category render (`TopDownRender`, §9 below and `docs/tools/capabilities.md`'s renderer section) paints
every leaf and log cell it finds, so a wood reads as one irregular violet mass whose internal structure means
nothing: two crowns that touch become one blob, and the tree count — the measure that actually decides whether
a board reads as wooded or as buried — cannot be read off it. A tree is authored as **one prop at one
coordinate**; the isolated `--subject foliage` image can draw exactly that instead of re-deriving a silhouette
from the blocks the build wrote, because the question that layer answers is about the trees themselves rather
than the cover a player crouches behind.

`DressingScope.TreeFootprints` reads every `TreeProp` off the dressing document, fanned across the map's
symmetry the same way every other footprint here is, and pairs each anchor with `Decorator.CanopyRadius(tree)`
— the farthest a leaf cell of the **same deterministic build** (`TreeTemplate.Build` or `TreeSkeleton.Grow` plus
`TreeCrown`) stands from the trunk, horizontally. This is the **measured** figure, read off the crown's own
geometry before any world exists rather than a species-nominal number: a tree is deterministic and RNG-free
(§2), so the shape `CanopyRadius` measures is exactly the shape the stamp goes on to write, and a taller tree
of the same species answers with a larger radius rather than a fixed one. `TopDownRender`'s `--subject foliage`
picture then plots each point as a circle grown to that radius, softly tinted so overlapping crowns build up
visibly denser rather than fusing into one shape, with a solid one-block trunk mark on top of every one so a
cluster stays countable even where its circles merge.

**This is a mode of the isolated foliage layer, not a second renderer.** The combined view still paints the
mass, and rightly: a player's cover is the leaves, not the centres, so the question the combined picture
answers is unaffected by any of this. Only `--subject foliage` switches, and only when it is given the points to
plot. The points come from the **dressing document** alone — a tree's crown is a pure function of its own
fields and seed, so no built world is needed to measure it — never from `WorldProvenance`, which carries no
tree claim at all and was never meant to (§8's provenance paragraph, and its own docstring, say why: a log and
a leaf already answer the material question a Ground/Structure claim exists to correct, so a tree has nothing
for provenance to fix). A **scanned** world has neither a dressing document nor a build-time provenance record,
so the isolated foliage layer has no points to plot there; it falls back to the leaf/log mass it has always
painted, stated on the console rather than silently substituted, which is the same shape of answer §8's
provenance state gives when a region carries no sidecar. `PgmStudio.RoundTrip --topdown --subject foliage`
takes an optional `--dressing <layout.json>` naming the document a bare region directory cannot otherwise
reach, and falls back to the mass without one.

## 7. Water — channels (`DR-WA`)

A channel begins exactly where the §4 stroke does — a dragged centerline and a radius, the same swept-disc
band (`Geom.WaterBed` reuses `PathBand.Centerline` and `Polyline`'s distance field). What makes water its own
tool is that it **cannot drape on the surface** the way gravel can: laid on a slope it reads as blue paint.
Water has to sit in a **carved bed** and fill to a **level plane**, so water is the one prop that takes the
ground *out* rather than standing on it.

**A body of water is drawn one of two ways, and `shape` says which its points mean.** A `channel` strokes them
as a centerline and takes its width from `radius` — a canal, a river, a moat. A `pool` closes them into a ring
and fills it, corners and all: a harbour, a lake, a flooded basin, the shape a stroked line cannot make
however wide it is drawn. The bowl is the same law either way and only the distance it is measured along
differs, which is the argument for one prop rather than two: a channel's bowl deepens away from its
centerline, a pool's inward from its shore, and on a pool `radius` is read as that **shelf** — how far in the
bed takes to reach full depth, so a harbour shallows against its quays rather than dropping to a trench at the
wall. `edge` wobbles either boundary by a value field, so a lake is not a ruled polygon.

The carve is a shallow U — deepest on the centerline, rising to a single block at the band's edge — so the
fill sits in a bowl rather than a walled trench. `WaterBed` yields a depth per cell from that parabolic law;
the dressing pass (`Decorator.PlaceWater`) turns each depth into a cut against the surface the cell actually
crosses. The fill is one **water line** for the whole run, and where that line comes from decides what the
carve may touch.

**Derived** — the default — the line is the lowest surface the channel crosses, which is what keeps the water
from floating: every column's surface is at or above it, so the pass fills from just above the bed floor up to
the line with stationary water and cuts any bank *above* the line back to air, leaving the channel open. The
carve then **only ever touches existing terrain**: it never rises past a column's old surface and skips any
column the surface map does not carry, so a channel dug across a hollow keeps the hollow and one dug over a
stamp leaves the stamp — the same exclusion a stroke respects.

**Stated** — `level`, a world Y — the line is that Y and the fill reaches it whatever the column beneath is
doing. **A basin is a low floor and not a hole**, because the pass skips any column the surface map does not
carry: a subtract takes the whole column out and leaves nothing to fill, so a harbour is drawn as an override
add laying a floor at the depth the water is meant to reach down to, inside the shore's own footprint. That is
the only way a basin holds water: ground dug out in the sketch has no surface up at the line
for a derived line to find, so the lowest surface the channel crosses *is* the basin floor and filling to it
puts no water in the hole. A lake, a harbour, the water a ship floats on can only be stated. What the author
owns then is the **rim**: water rises to the line inside the prop's own footprint and nowhere else — the pass
never floods outward looking for a wall — so a line above the surrounding ground stands as a wall of water
rather than spilling, which is visible the moment it is exported. The bed floor is laid only where terrain
already stood, so a stated line over a basin already deeper than `level − depth` leaves the basin's own floor
alone instead of hanging a shelf in it.

**Water fills round what stands in it and never cuts under it.** The two halves of the pass are different
acts on a column something else keeps clear: carving takes that thing's own ground out from beneath it, which
is what a keep-out is for, while filling puts water in the air beside a hull or a pier — and a harbour dry
under the ship floating in it is not a harbour. So a kept column is filled and never cut, its bed floor is
not laid and it claims nothing; and above any column's own surface the pass writes only into air, so the
water goes round a hull, a mast or a stamp rather than through it.

Like every prop, a channel is **fanned across the symmetry orbit**, so both teams get the same water from the
same side; a stated line is the same Y at every image, a level plane being level in all of them.

The water meets the land through a **beach**. The shore is its own pass — the band *outside* the water, out to
a width that wanders with a noise field and drops to nothing in places, so the water meets the grass directly
in some stretches and spreads into a flat in others (`WaterBed.ShoreCells`). Both the beach and the bed floor
are laid with the channel's **bank**, and the bank is not a block but a full **`TerrainMaterial`** — a solid, or
by default a cellular voronoi of gravel, coarse dirt and sand, the same pattern the painter tiles
and edited by the same `MaterialEditor`. So the floor the shallows show through and the shore the water meets
read as one ground, drawn from one palette.

Channels take a **form**, and it drives both the water and the land. The three laws live in `WaterBed`, which
cuts the bed and the beach together so they cannot disagree. A clean-banked **canal** holds a uniform width. A
**natural** edge wobbles its width off the nominal by a value field (± the *bank roughness* knob, in blocks). A
**stream beads**: its width runs a rectified sine along the *arc* — pinching to half the radius and swelling
back to it on a fixed beat, never wider than the nominal — so it narrows and widens down its length into a
string of riffles rather than tapering once, and it runs shallower throughout. The **shore** rides just outside the water edge — a beach cell's inner
edge *is* the water's, so the bank hugs the water whatever shape it takes. Its width is read along the channel's
**arc**, not the plan grid, so at a point down the run both banks take the same width and the beach stays
symmetric about the water, widening into a flat here and closing to nothing there *along* the channel rather than
drifting onto one bank the way a plain spatial field does on a bend (the prototype's `shoreWidth` sampled the
grid; carrying it to the arc is the one deliberate departure, and it is what makes the beach fit the water). A
**beach edge** toggle switches that wander off for a clean, even band of the full width the whole way. The carve leans toward the **G32-C** elevation
pass rather than depending on it: the bed is negative terrain laid straight into the realized world, so a
channel works on the flat layouts the sketch tool builds today and will read as a cut valley once that pass
gives a layout its heights.

**Still to come (`G169`).** The reads that take a channel from "a filled cut" to "water that looks like
water," and the closed form: **depth shading** warped off-centre so one bank runs deeper than the other; an
**irregular shoreline** whose width wanders to zero in places; a **voronoi-patterned** bed and shore (sand,
pale gravel, coarse dirt) showing through the shallows; **edge life** reusing the §3 flora overlay masked to
the bank (reeds, lily pads); and **ponds** — the closed version, an organic basin (the §5 boulder blob read
concave), scattered onto low ground and joined to channels into one watershed on a single water level.

## 8. Buildings — a rectangle and a shell (`DR-HO`)

A building is dressing that already had a stamper. The shell a wool cage and a spawn cube are raised in is a
`HouseStyle` over a footprint (`structures.md` §7), and nothing in that stamper knows or cares where the
footprint came from — so a **drawn rectangle** is as good an origin as a plan piece, and a house becomes the
sixth prop.

**A room's building is the single-wing case of this one**, and the studio says so rather than leaving it to be
noticed. Both are a footprint and a shell; both reach the stamper through one `BuildingPlan`, whose
single-rectangle constructor is what a room uses; both are held to one least span (`RoomFrames.MinFootprintSpan`,
`WX2`/`HP2`); and both are drawn in one ink wherever they appear, so a house dressed onto the ground and a
shell raised on a spawn read as one kind of thing. What a room adds is what being played through asks for — a
pad, monuments, chests and an entry contract — and when it is stamped: **before** the painter rather than
after it with the rest of the dressing. Spawns and wools still resolve their frames from pieces and markers,
at the stage they always did; what changed is that the rectangle underneath is no longer a second model of a
building.

One thing still differs by where a footprint came from, and it is the **ceiling**. A prop is capped at
`MaxFootprint` covered cells; a room's building is bounded by the region it stands on (`WX12`) and capped at
20×20 by `ST9`. Whether those should be one number is the author's, and until it is answered the two are
stated apart rather than quietly averaged.

**The geometry is a third interaction.** A point is a click and an outline is a trace; a building is a
press-drag-release rectangle, because a footprint is what the stamper takes and a stamper takes a box. It is
stored as its **two opposite corners**, which is what lets the fan mirror it as the shape it is: a rectangle
turned through ninety degrees is a rectangle whose width and depth have swapped, and taking two corners round
the orbit says that without the stamp being told it happened. The **door turns with it** — an edge is a
direction, so it goes round the orbit the way an offset does (`DressingSymmetry.TurnEdge`). Fanning the
rectangle alone would put a copy on the far side of the map with its door still on the same compass side, so a
mirrored pair would face the same way and one team would walk out toward the other's half.

**It obeys one part of the keep-out mask, the approach, and never joins the mask itself.** A building is not
generated the way a flora field is: someone drew this rectangle here, and a spawn's own margin is not a reason
to lose it. A door's approach is the exception, because a building is the one prop big enough to close a lane
entirely (§3.1); the decline is reported like every other, so nothing is dropped silently.
Its cells do join the pass's running claim, which is a different mechanism entirely — the rule that keeps grass
from growing through the walls, exactly as a route's cells claim the road. In the ordering that puts buildings
after strokes and before the props that scatter around them — and a route's own claim is the one a building
does not check, because a road is meant to run to a porch (§4): a house collides with water or with another
house, never with pavement.

**What it claims is what it stamps, grown one block outward.** Every other placement already reserves ground
around itself — a goal keeps props off its standoff, a door keeps its approach open, a tree stands three
blocks off the road — and a building used to claim the *wall* rectangles alone, so a verge overhung ground the
pass believed free and the next prop seated under it. The claim is the extent the stamp writes, eaves
included, plus a block of ring; what a placement is **tested** against is the stamped extent rather than the
ring, so the ring is spent once between a pair and two buildings end up with one block of clear ground between
them rather than two. A trunk near enough to drop its crown through a roof is refused as `DR-CLAIM`, and the
owner a later decline names is the ground the building really holds.

What it does need is ground, and that is physics rather than policy: it seats on the **lowest** column of its
own footprint, one course down, so it settles into a slope instead of standing on stilts over the low side.
That seat is why the ground is required under **every** cell of the footprint and not merely under one:
a plan with one cell on land and ten over void would seat on that one and hang off the rest, and nothing else
covers it — the passage walk reads the bands *outside* the footprint, and the excavation skips a missing
column rather than refusing it. Half a building on solid ground is worse than none, so the first bare column
declines the whole prop as `DR-SITE` and the finding names that column.

**And that seat is why the site has to be level enough (`DR-SLOPE`).** Seating on the lowest column means
the terrain standing over that floor runs through the rooms, so the pass carves it out — every footprint
column is cleared from the floor's own course up to its old surface, and the ground outside the walls keeps
its height. That is what lets a house dig into a hillside instead of standing on a plinth, and it has a
limit: where the ground rises across the footprint by as much as the building itself stands — its wall
courses plus the rise of its roof — the uphill side is under the ground beside it *roof and all*, and what
was built is a house nobody can see. The threshold is the style's own height rather than a constant, so a
two-storey barn may stand on a bank a cottage may not.

The failure was generated for real, on `pgm-studio-mapgen`'s `opus5-ravensmere`: four of five houses sited
on rolling downs came out on footprints spanning 8 to 13 courses of relief, and one of them landed inside a
crevasse — declined by nothing, because every cell had ground under it. The fix at the authoring end is the
same one an objective's ground gets: an `area` relief mark under the footprint, which states the plateau
rather than hoping for one.

**It must leave a way past itself (`DR-PASS`).** Beside a building there must be **five blocks** of passable
ground along at least one of its four sides — the whole run of that side, extended one step past each
corner, which is the cell a player turns in from and exactly what separates a flank that can be entered from
one walled off at both ends. The failure this closes was generated for real: a house across the full width
of a land leg, void on both flanks, so the only way to the other side was through the building. A house
against the map's own edge is fine — a coast house is a house — as long as the other side keeps the passage.
Passable means terrain with nothing *built* on it: a road or a channel alongside the wall still counts as a
way past, an earlier building does not. A breach declines the whole prop with the rule id in its census
reason, decided once for the orbit like every other refusal here.

**It may end a road but never stand across one (`DR-CROSS`).** A road is meant to run to a porch or a door,
so a building taking the ground a road covers is ordinary: the road ends at its wall and the building wins the
cell (the author's ruling, and why a building checks every claim but `ClaimKind.Route`). A building the road
carries on **past** is the other thing entirely — what was one way through the board becomes two dead ends
facing a wall, and every other gate answers 200 because the ground beside it is wide and the objectives still
connect by some other way.

The two are told apart by **what is left of the paving once the footprint is out of it**: one run of road is
an end, two or more is a crossing (`RouteCrossing`). The count is taken **before and after** rather than
against one, because a stroke's own coverage leaves cells out — a worn road is holes by design and a
stepping-stone crossing is nothing but holes — so what fires is the building *adding* a break. For the same
reason two paved cells within two blocks of each other count as one run. Only a stroke the author marked a
**route** is a way; paint laid to change a finish is ground, and a building on it is a building on grass.

Measured over the thirty-two boards `pgm-studio-mapgen` has built: **7 of 122** buildings stand across a
route, on five boards, and **3** sit at the end of one and stand.

**It must not close a way the board is played along (`DR-WAY`).** `DR-PASS` is local: it asks whether there
is ground beside the building, and a building can leave five clear blocks on every side and still cork the
one leg the map is walked down, because the ground it corks is a hundred blocks away and shaped like a neck.
So the board is walked. Between every pair of the cells the map is **played between** — its spawns, its wool
rooms and monuments, its destroyables and cores — the shortest route over the bare terrain is taken once, and
the building is then admitted to that board: its whole orbit's footprint comes out of the ground and every
route it stood on is walked again. A pair that had a route and now has none is a way closed. A pair whose
route survives more than **ten blocks** longer is the same fault at a lesser degree — ten is `Walk.Detour`,
how far out of their way a player will go, so a building spending more than that has moved the route rather
than been walked past. Either way the whole prop is declined.

Three things make it cheap enough to ask of every building. The walk is over the **terrain surface**, one
place a column, which is the ground a building is planted on and the ground a route between objectives runs
over. A candidate standing on **no current route** changes nothing and is admitted without a second walk —
the shortest way it does not touch is still there at the price it already cost. And the board is read once,
before the first building, rather than per prop.

Props **accumulate**: an admitted footprint stays out of the ground the next candidate is judged on, so two
buildings that each leave a way and together leave none are caught at the second. The declared route strokes
are not part of this reading — a road is judged by `DR-CROSS` above, on whether it ends at the building or
carries on past it — so a stroke is a finish on the ground here and never a way in its own right.

**It carves the slope out of its own rooms.** Seating on the lowest column means that on a hillside or a
relief mark the higher ground runs exactly where the rooms will be — and the stamper deliberately never cuts
terrain (air resolved out of a material is a gap left open, never a hole punched), so without a carve the
relief stands inside the house. The building wins the ground it was drawn on, the same rule that keeps a
trunk from rooting in one: before the stamp, every footprint column is cleared from the floor's course up to
its old surface, so the house sinks into the slope with its interior intact. Only the wall plan is carved —
the ground under the eaves is outside the building, and a hill leaning against the wall is the look the seat
rule exists to produce — and a column whose surface carries a stamp is left whole, as everywhere.

**A building is bounded at both ends.** The floor is the stamper's own — three blocks a side, two walls and an
inside — and the ceiling is the prop's: **192 blocks of footprint**, three times the 8×8 shell a wool cage is
stamped in, so a 12×16 or a 14×13 house is buildable and a 20×30 one is not. The unit is deliberately the room
the map is played through. This is scenery a map is dressed with rather than architecture it is built from, and
scenery covering much more than a few of those stops reading as scenery and starts competing with the
objectives for the ground — these are maps of pieces and lanes, not a landscape with a town in it.

The cap is an **area** rather than a side length, so a long low building is as buildable as a square one. It
bounds what a building costs and how much map it covers, and nothing else. **Height is bounded separately, and
by the roof rather than by the prop**: every form's rise is measured over the building's shorter side
(`structures.md` §7.1), which is what stops a 10×60 hall carrying a lean-to sixty courses over its wall. Two
bounds because they are two questions — a cap on area cannot answer the second, and at a steep pitch a square
building of legal area is the taller one.

The ceiling belongs to the **prop** and never to the stamper. A wool cage and a spawn cube run through the same
`HouseStamper`, and their footprints come from the plan piece they sit on (WX1) — a map's own geometry, which a
dressing limit has no business refusing. The canvas restates the number so a drag can be judged while it is
still in the pointer, and shows an overshoot as refused the way a marker over the void is; a rectangle that has
not yet grown to three blocks is not marked, because that is the first moment of every drag, where being too
big is a thing the author kept dragging to do.

The shell is a **snapshot** on the prop, not a library id — the rule a map's bound room styles follow
(`structures.md` §9). Picking a style from the library copies its JSON in, so editing that row later cannot
rebuild a map's scenery.

**Every whole-prop decline is a `Finding`, in the shape everything else says no in.** A house whose wings
make no building, a house whose ground something already claimed, a house with a cell of its footprint over no
ground, with no way past it
or standing in a door's approach, across a drawn road or across a way the board is played along, a tree or a boulder whose site finds no ground, lands on a column the map
keeps clear or one already claimed, or breaks its kind's road standoff — each appends one finding to
`DressingPlacement.Declined`: a rule id (`DR-KEEP`, `DR-CLAIM`, `DR-SITE`, `DR-ROAD`, `DR-PASS`, `DR-CROSS`,
`DR-WAY`, `DR-SIZE`, or
the building rule that refused a plan), one sentence naming the prop, the cell and the cause, the prop's id
as its subject, and `Severity.Complaint` — the world was built, and some of what was authored is not standing
in it.

The sentence names **what** stopped the prop, not merely that something did: `KeepOut` says whether a cell is
held for a spawn, a wool room, a stated structure, built ground or a door's approach, and `GroundClaims`
carries the *owner* of every claim, so a collision reads `claimed by the route 'p'` rather than `already
claimed`. The first claimant keeps a cell, so that owner is the one that actually holds the ground.

Each one is a **`decline`**, the severity between a refusal and a complaint: the world was built, so nothing
stopped, and this prop is not in it, so there is nothing for the author to ignore. That is what a caller reads
off a 2xx to answer *did what I posted survive* — a complaint beside a success would say the opposite.

The declines travel three ways. Back from `POST /map/{slug}/sketch/columns` and `POST /plan/columns` under
`warnings` beside the payload, which is the loop an agent actually drives. As `region/dressing-report.json`
beside the provenance sidecar (written only when something dropped, deleted on a rebuild that dropped
nothing), **inside the export zip** — the two sidecars are the two halves of one census, provenance saying
what landed and this saying what did not, and an HTTP caller that got only the first could not tell a prop
that was never authored from one the pass refused. And in the **`Pgm-Warnings` header** on
`GET /map/{slug}/export` and `GET /map/{slug}/xml`: the count and each rule id once, which is what a caller
reads without unzipping, and on the XML route — same world, same pass, same drops, no sidecar — the only
answer there is.

**One of the two is ordered, and the order is not obvious.** `DR-KEEP` reads the spawn doors' approaches
and the goal rings, and those come off the map's **intent** — so `sketch/columns` asked before
`PUT …/intent/from-plan` answers a shorter list than the same call asked after it. A driver that reads the
declines at the end of the sketch stage sees every rule but that one. A stroke's per-cell skips stay unreported; a
band crossing kept-clear ground one cell at a time is the ordinary shape of a stroke, not a decision an author
needs restated.

**The pass answers without building.** `POST /map/{slug}/sketch/dressing` runs it against a posted layout and
stops before anything is written, answering per prop the columns it covers, where it rests, the height it
resolved to, and every prop that did not land as the `DR-*` finding it draws. A keep-out is measured against
what a prop *claims*, and a stroke's claim is decided by its style, its coverage and its seed — none of which
can be reasoned about from the document, so tuning one without this is a guess corrected by drive, read the
declines, move the prop, which lands on the wrong distances in both directions before it lands on the right
ones.

**Its footprint claims provenance the same way a room's does, only later.** `Decorate` reports a
`PlacementClaim` for every building it raises and `WorldBuilder` records each as `WorldProvenance`'s
`Structure` layer, so a house standing on a plaza the painter finished in the same material as its own walls
still reads as a building rather than fusing with the ground it stands on: two different passes claimed the two
sets of cells, whatever either is made of.

**Every other prop claims too, on the `Prop` layer.** A tree, a boulder, a road, a water course and a bed of
flora each report the columns they covered, under a claim of their own. The record used to stop at the
buildings, on the argument that the rest separate from built ground by material already. They do — what
material cannot say is that a *pass* put them there, or which prop they belonged to, and those are the two
things a read-back has to be able to prove: a flora prop that landed nothing looks exactly like one that was
never authored, which is how two of them once landed nothing with no diagnostic anywhere. The layer keeps the
distinction a reader needs in the other direction as well: `StructureFinder` asks for `Structure` and is
therefore never handed a tree, however that tree's blocks happen to read. It changes no picture — a claimed
prop still draws by its own material, so leaves stay foliage and a road's gravel stays ground.

**And a claim is walked, not carried across as a rectangle.** Every stamper that fills a footprint publishes
the columns it filled — `StructureStamper.FoundationCells` for a room's own plinth, `WallCells` for an approach wall,
`RedstoneLineCells` for an entrance row — and the claim is made over that walk. The two conventions are
genuinely different and neither is wrong: a stamp's footprint is **max-exclusive**, because that is what an
intent rect means over whole world blocks, and `WorldProvenance.ClaimRect` is **max-inclusive**. So a rect
handed from one to the other is recorded a column wider on each axis than it is built — a 25 × 2 bedrock wall
drawn as a 26 × 3 bar by every read that trusts the sidecar, and a bedrock line's thickness is exactly what
decides whether it can be built over.

**The claim comes from the placement, and that direction is the point.** The pass drops a building **whole**
when any of its orbit images overlaps something already standing (MG7), stands over no ground, or fails its
turn — so a claim rebuilt afterwards from the layout document cannot see any of it, and claims every authored
house on every image regardless. That was the state of it until `B202`: on two authored houses whose stamped
rings overlap, one placed and two claimed, 56 columns carried a `Structure` claim over bare ground, and because
provenance is *preferred* over the material estimate a stage image drew a building that was not there and said
it was certain. So the claim is now built inside the same loop that stamps, from the images that were actually
raised: a dropped building leaves an empty list and nothing is claimed for it. The rule that names this —
a claim is taken from the placement, never rebuilt beside it — is `PlacementClaim`'s own docstring, and it is
the rule `DressingScope.GoalGroundAt` had already been following for a goal's ground.

**Each claim carries an owner, not just the layer, and the owner says which image it is.** The owner is a
`StampId` — `kind`, the authored `unit`, and which orbit `image` of that unit this claim is — and there is one
claim per unit per image rather than a flat cell list. This is what lets a reader (`Render.StructureFinder`)
tell two authored houses apart even when their stamped rings genuinely touch: a layer alone answers "is this
built", and only the owner answers "built by which stamp" — a terrace of houses sharing a wall reads as one
finding per house instead of one finding for the row it would otherwise flood into.

Splitting the unit from the image is what makes a mirrored board readable. The owner used to be a string each
stamp site built for itself, and the sites did not agree about the number in it: a house put its orbit image
there while a spawn, a wool and a destroyable put a running index into the *already-fanned* list. So two
entries of one form meant different things, both images of one thing were separate entries with nothing saying
which thing they were two of, and a reader wanting to pair a stamp with its own mirror had to recover the
pairing geometrically — and got it wrong. Now two images of one unit share an `Identity` and differ only in
`Image`, so the structure render colours a mirrored pair as one thing and a genuinely unpaired structure is
what stands out.

The id is minted **where the fan happens** rather than where the blocks land: `PlanCompiler` sets it as it
fans a plan's placements, `SymmetryExpander` sets it as it fills an intent that carries only one unit, and
`WorldBuilder` gives a list index to anything that arrived without one. A stamper receiving an entry out
of an already-fanned list cannot know which authored unit it came from and can only count, which is exactly
the ambiguity this replaced.

**The claim is the stamp's own reach, not the rectangle someone dragged.** The claim used to be
`HouseProp.Plan()` — the two-corner rectangle a style's walls stand on — and stop there. A roof reaches
past that by its `overhang`, a `verge` is commonly a log, and a `BeamStyle`'s log ends run further still, so
the ring of cells the eaves actually land on kept whatever claim the ground under it had before the house was
placed and read `Foliage` by material — a village of eleven houses drew eleven rectangles outlined in
tree-colour on the category render. `HouseStamper.StampedExtent(ground, style)` answers what the stamp actually
reaches — the wall rectangle grown by the greatest of the overhang, the beam reach and the one-block sill that
rings every footprint regardless of either — read straight off the style's own fields rather than re-derived
from voxels, since `overhang`, `pitch` and `Beams.Reach` are exactly what the stamper itself reads to lay the
roof and the beam ends. The claim reads `HouseStamper.StampedCells` for each wing of the image it just
stamped — the stamper's own function, so the claim and the stamp are one derivation rather than two that agree
today — and the union of the wings rather than one box round the whole plan, because an L or a T has ground in
its notch no eave reaches. That single formula is also what a stamped porch never needs its own case for: a porch is
carved out of the footprint it was handed rather than added past it, so its own canopy overhangs by the same
`Overhang` and never reaches further than the main roof already does.

## 9. What it reuses, and what it adds

The stage leans hard on machinery G157 and the sketch tools already shipped; the net-new surface is small
and lands in the same realize seam.

| Concept | Reuses | Net-new | Rule family |
|---|---|---|---|
| Ground cover | `PatternNoise`; `SurfaceTop`; the `TerrainProfile` column read | the overlay pass, gated by a drawn outline — one `SetBlock` above the surface | `DR-FL` |
| Strokes | `CatmullRom`; `Ribbon`; `Polyline`'s distance field; the lasso's own press-trace-release | `PathStroke`'s six gates; `PathBand` + its `geometry/path.js` twin for the drawn outline | `DR-PA` |
| Boulders | `SurfaceTop`; the squared-distance masks the objective stampers fill by | `Blob`; `BoulderShapes` | `DR-SC` |
| Trees | the boulder's seating; `CatmullRom` for the limb splines | `TreeSkeleton`; `TreeCrown`; `SweptVolume`; the species rows | `DR-TR` |
| Water | the §4 path stroke's band (channels); the §5 boulder blob + FBM edge (ponds); the §3 flora overlay (reeds) | `WaterBed` + `Decorator.PlaceWater` — the carve-and-level bed (shipped); depth shading, the shoreline band, ponds (G169) | `DR-WA` |
| Buildings | `HouseStamper` + `HouseStyle` whole; the room-style library; `DressingSymmetry`'s outline fan | `HouseProp` + `Decorator.PlaceHouse`; the rectangle drag; `TurnEdge` for the door | `DR-HO` |
| The ways past a building | `Walk` + `WalkGround.OfSpans` — the one traversal every distance is measured with, and `Walk.Detour`'s ten blocks | `WayThrough` — the waypoint-pair routes read off the bare terrain, held as each building is admitted to them | `DR-WAY` |
| A road a building stands on | the stroke's own placed cells, per orbit image | `RouteCrossing` — the runs the paving falls into with the footprint out of it, before against after | `DR-CROSS` |
| The document itself | — | `DressingParseException` — a parse failure anywhere in the stored document names the prop and the field rather than being read as though nothing had been placed; joins the export gate as a 422 (`docs/tools/configure.md`) | `DR-DOC` |

Two neighbours bound the stage. G32-C (structures & elevation, the "second generator") is the sibling pass
that gives a flat layout its heights; a boulder or tree seats on whatever surface that pass leaves, so the
two compose but do not depend on each other — and water, whose carved bed **is** an elevation change, cuts
its bed straight into the realized world, so it works on today's flat layouts and simply reads as a cut valley
once G32-C gives a layout its heights. G142 (the roughen pass) shares this stage's architecture —
last in realize, over the authored unit, symmetry re-fanned — and its edge-displacement operator is the
path's rough edge; if both land, they share the noise operators rather than duplicating them.

## 10. Where the code lives

The organizing rule is the repo's own (`CLAUDE.md`): a unit of code lives in the lowest project that has the
deps it needs and every consumer can reach — push pure algorithms down to the leaf, keep world-writing where
the world is. The dressing stage straddles two projects, and splits cleanly along that line.

**`PgmStudio.Geom/Algorithms` — the pure math, shared by every generator and its JS twins.** The folder was
always meant for geometry algorithms, and the dressing math is exactly that; three of the pieces already sit
there:
- `CatmullRom` — centripetal spline. Smooths the tree limbs and the path/channel centerline (the same
  algorithm the tree prototype reimplements in JS — reuse it, don't re-add it).
- `Ribbon` — offsets a centerline into a strip outline (`Uniform` fixed-width, `Varied` variable/tapered/
  organic). This **is** the DR-PA/DR-WA band; the code is the old orphaned `Lane` class (long-dead sketch-tool
  approaches), kept but **renamed**, because a "lane" is a layout role (a run of pieces), not a geometry
  primitive. `Varied` gives the tapered channel and the jittered shore in one call.
- `PatternNoise` — hash/value/fbm, **migrated here from `Minecraft`** (it was pure but trapped beside the
  terrain-paint materials). The pattern *materials* (`VoronoiMaterial`/`CellMaterial`/`NoiseMaterial`/`WallRunMaterial`)
  stay in `Minecraft` and call it. The move added the one edge that was missing, **`Minecraft → Geom`**, which
  is what lets the dressing stamper reach any of this.
- `Blob` — the eroded quadric a boulder lobe is; `SweptVolume` — the capsule a tree limb is; `Polyline` —
  the distance field a stroke of any width is filled by.
- `TreeSkeleton` and `TreeCrown` — the grower and the crown placement, as abstract limb centerlines, radii
  and leaf-cluster centres. No block ever appears in either.
- `TreeTemplate` and `CanopyProfiles` — the other tree: a trunk under a canopy whose profile is a radius per
  course. It is a sibling of the grower rather than a mode of it, because the two build different shapes.
- `PathStroke` — which cells a stroke paves, one gate per style; `PathBand` — the outline the canvas draws
  it as, and the one C# side of the `geometry/path.js` parity pair; `WaterBed` — the same swept-disc band read
  as a carve, a bed depth per cell (deepest on the line, one at the shore) for the three channel forms.
- `OrbitScatter` — which cell of an orbit is its representative, the answer §2's fan is built on.
- `BlueNoise` — even, non-touching scatter sites. Nothing in the shipped stage places by it any more, since
  everything is placed by hand; it stays for the auto-placement ideas of `ideas.md`, which will want it.

**`PgmStudio.Minecraft/Dressing` — the world-writing pass.** `Decorator`, sibling to `ObjectiveStamper` and
`TerrainPainter`: it takes a `DressingContext` (the surface, the placed props, the keep-out mask and what
each cell is held for, the symmetry, the cells the map is played between) and writes blocks via `SetBlock`. It reaches `Geom` for the algorithms and `DressingSymmetry` for
the orbit fan. The props themselves (`StrokeProp`, `WaterProp`, `FloraProp`, `HouseProp`, `TreeProp`,
`BoulderProp` under one `PlacedProp` discriminator) and the block palette live here beside
`Blocks`/`BlockPalette`. `WayThrough` is here too, for the same reason: it reads `Walk` out of `Geom` and
answers a question only the pass asks. A building's own stamper is **not** here — `HouseStamper` sits a folder up, where the
room stampers already call it, and the pass reaches sideways to it rather than growing a second copy.

**`PgmStudio.Export`** — **reading + wiring.** `DressingScope` answers the three things the pass needs from
a map: what was placed, how the map is mirrored, what must be left bare, and the cells the map is played
between (`WaypointsOf`, `DR-WAY`). Unlike `TerrainThemeScope` there
is no scope to resolve — a prop is not a recipe applied to a footprint, so reading it is reading a list.
`WorldBuilder.Build` then calls `Decorator.Decorate` immediately after `TerrainPainter.Paint`.

**`PgmStudio.Api/Services`** — **the preview.** `DressingPreview` draws a prop by placing it — a sample patch
painted with a theme and run through the real `Decorator` — and draws every picker's cards the same way, so a
picker can never offer a look the export does not produce.

The side view is a **projection**, not a cut. `Minecraft.BlockSideView` looks through every row and keeps the
nearest block, shading it by how far back it stands; a single row through a crown meets it wherever that row
happens to fall — as often through the air between leaf clusters as through them — so a cut comes out
speckled and missing pieces that are plainly there. It is the model `Analysis.SideView` already draws a whole
map with for the build-height step, with one difference: that one projects vertical *segments*, which carry
no block identity, so its picture is a stone-grey ramp; this projects a real world and keeps the block, so
the colours stay the export's own and only the shading comes from depth. Depth is measured against the
projected box rather than against the depths the view happened to reach, so a block's shade says where that
block is and nothing else — stretching the scale to fit the content would let one block moving back re-shade
every other block in the picture.

Both views look **inside** the sample's outermost ring. The painter reads a footprint's perimeter as its edge
and finishes it as one, a rim course over a wall, so that ring is the sample's own boundary rather than
ground a prop could stand on; seen from the side it is the entire front face, and a tree that stands in grass
was drawn standing behind a wall. Within one picker every card is drawn on one patch, sized to the widest
option, and cropped to the same courses — the tallest option decides the top and the ground decides the
bottom — so the cards sit in a grid with their floors on one line and their heights honestly compared, and a
two-block rock is not four pixels in a sample cut for a tree.

**`PgmStudio.Pgm/Sketch` — storage only.** `SketchLayout.Dressing` is the stored blob and nothing here reads
into it. A path was briefly a `SketchShape` and is not one: it places no terrain, so putting it in the
rasterizer meant the geometry model carried a kind that never contributed a cell.

**`PgmStudio.Client` — the placing tools.** `dressing/dressing-doc.js` is the document (the same wire format,
so there is no second model of a prop), `controllers/dressing-controller.js` the five tools, and
`render/dressing-render.js` how a placed prop and its mirror images look on the canvas. The inspector is
Blazor and owns no state: it reads the bridge's pushed document and writes patches back.

**Symmetry (G162)** is not a place but a rule, and §2 is where it binds.
