# Map decoration — the idea pool

The dressing stage's open work that is not on the board. The stage ships its five tools — paths, ground cover,
boulders, trees and fluids — placed on the Sketch's Dressing canvas and run over the built world, with one claim
book and one keep-out mask between them (`decoration.md`); what is here is what would turn those tools into a
dressing that reads the ground for itself.

**Every entry is one bullet — its id and a title in bold, then what it would build and where, with the reason it
matters.** An id is never reused or renumbered, and an entry pulled onto `BACKLOG.md` keeps its id and leaves this
file. Nothing here is built; `decoration.md` is the model it extends.

**Two entries carry a `G` id, and keep it.** `G162` and `G169` are cited as provenance by work that has shipped
(`FEATURES.md`, `decoration.md`), and an id is a handle: renaming one breaks every commit that cites it, which is
why `CLAUDE.md` retires a prefix without renaming the entries already carrying it. Every id minted here takes the
`WE` prefix, and `G` belongs to `docs/generator/`.

## Play first: what a dressed board must still allow

- **G162 — A prop whose images meet on the symmetry axis.** Every prop is placed once and stamped at each image of
  its orbit, turned, so it is identical per team by construction (`decoration.md` §2, *The fan*). A prop whose
  orbit images overlap — a boulder sitting on or within a radius of the mirror line — is stamped twice into the
  same cells, and the two copies fuse into one shape that is neither's: it reads as a lump, and on a `rot_180` map
  it is exactly where the centre objective usually sits. Three answers: refuse a representative whose prop bounds
  cross the axis, place one prop *on* the axis built symmetric in its own frame, or merge the images and accept the
  fused mass as intended. Cheap to detect (the prop's own bounds against the axis) and to test, since the fairness
  check that counts unmirrored cover cells exists.

- **WE108 — A dressing budget, and the props that narrow a lane.** A building is held to its passage (`DR-PASS`,
  `DR-SPAN`), its walking detour (`DR-WAY`) and the road it crosses (`DR-CROSS`); every prop keeps off the spawns,
  wool rooms, structures and door approaches (`DR-KEEP`) and a road's standoff (`DR-ROAD`); and a goal reads its
  own ground (`decoration.md` §3.1). Open: a **density cap per region kind** — a lane takes far less than a
  backfield — so restraint is a control rather than an afterthought; a corridor-width guard for the props that are
  not buildings, so no tree or boulder narrows a corridor below its minimum; and no gameplay-affecting prop inside
  a build region. The terrain half of the same question is a ruling, not a measurement, and takes the shape of a
  named rule on one measure rather than a single score: `world-scan/terrain-steepness.md` holds the corpus reading
  a threshold would be set against, and the measure not yet taken is the **detour factor between key places** — it
  reads ≈1 only on ground that is genuinely flat, and the walk prices a climb.

## Composition: one pass's result changes what the next grows

- **WE105 — Affinity between the passes.** The order and the exclusion are stated: `PlacedProp.PlacementOrder`
  fixes who places first, `GroundClaims` keeps a claim per layer (`DR-CLAIM`), and a route's cells join the mask as
  it is laid (`decoration.md` §1–§2). What no pass does is change what another grows: a dense canopy suppressing
  tall grass beneath it and seeding shade-ferns instead, a water fill's edge growing reeds. Written as an affinity
  beside the exclusion — each pass reads what the earlier ones wrote and leans its own draw by it — it belongs in
  `decoration.md` as part of the claim book.

- **WE106 — One biome or season over every dressing palette.** Each tool picks its materials and species by hand,
  so nothing prevents snow ground under oak trees beside unfrozen water. One selector — temperate, desert, snow,
  autumn, swamp — harmonizes the dressing palettes at once (grass → dead bush and cactus, water → ice, oak →
  spruce, sand → snow). Its home is the per-cell theme resolution the painter uses (`TerrainThemeScope`, TP10),
  extended from materials to the dressing's species tables; the client tint a biome byte carries is already its
  own family (`terrain-painting.md` §5b).

## Placement by context: grown rather than scattered

- **WE107 — Context-aware placement.** Placement is by hand, which is right for the props that decide a fight and
  tedious for the ones that only fill a hillside. The optional half is a brush that scatters cosmetic dressing over
  an area by **affinity** over derivable fields — slope, height, distance to water, border proximity, canopy cover,
  distance to a lane — so trees thin along lanes and thicken at the edges, boulders follow cliff bases and
  ridgelines, flowers avoid shade and moss prefers shaded faces. The fields are cheap derivations off the built
  surface and the sketch's regions, and the blue-noise scatter it needs is in `Geom` (`BlueNoise`, with
  `OrbitScatter` binding it to a map's symmetry) and unused by the dressing.

- **WE109 — Framing the border and the places that matter.** The **void edge** reads as a boundary — a treeline
  wall, a cliff or a beach at the void — so players read the play-space limit without an invisible wall, as PGM maps
  already do with terrain. And dressing **frames** the important places rather than only keeping out of them: a
  clearing round a monument, flowers marking a spawn, a path leading to a wool room. Both lean on `WE107`'s
  distance-to-border and distance-to-objective fields.

## Faces and water: the ground that is not a top

- **WE104 — Dressing a vertical face.** Every tool but the fluid dresses the top of a column, and the Sketch builds
  cliffs and walls that dress differently: moss and lichen creeping on stone faces, vines and hanging foliage
  dripping from ledges and overhangs (the copied tree's hanging strand turned vertical), the occasional ledge shrub
  or exposed root, and **scree** — the boulder scatter shed at a cliff base. It attaches to **faces** read off the
  built world rather than to the top grid; the slope band (`BandAxis.Slope`) is the paint's reading of the same
  face, and `Materials.CliffAngle` already reads it back to keep a boulder off one (`DR-STEEP`, `decoration.md`
  §5).

- **G169 — Water that looks like water, and the pond.** The channel, the pool and the basin ship (`decoration.md`
  §7), with `edge` wobbling a boundary. Open: **depth shading** warped off-centre so one bank runs deeper than the
  other; a **shoreline** whose width wanders to zero in places; a **voronoi-patterned** bed and shore (sand, pale
  gravel, coarse dirt) showing through the shallows; **reeds** on the bank, the half of edge life the flora overlay
  does not grow (its lily pads float on open water, §3) — on a water fill only, since a lava bed grows none; and
  **ponds**, an organic basin (the §5 boulder blob read concave) scattered onto low ground and joined to channels
  into one watershed. On ground with relief a fluid routes on a depression-filled surface with its bed forced
  non-increasing downstream and one level per pool (§7.1), and the ponds are then the same mechanism, not a
  second one.
