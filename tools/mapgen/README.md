# mapgen — a whole map from one JSON spec

> `surface.md` beside this file maps the documents a map is made of and where each generator lives — read it
> before writing anything against this tool's spec, which is a reduction of it (`review.md` MG29).
>
> `review.md` records where the first fifteen maps fell short of what the corpus ships, as a
> pool of `MG` entries. Read it before adding to the tool: several of the knobs documented here are the wrong
> knobs, and the largest entry is that a destroy map needs a board composed for it rather than a capture board
> with its wool retargeted.

```bash
dotnet run --project tools/mapgen -- <spec.json> [more.json ...]
dotnet run --project tools/mapgen -- --describe <spec.json>    # compile only, report the board
```

The spec says what a map is *about*; the generator answers the rest. Everything goes through
`SketchWorldBuilder` and `IntentGenerator` — the same path the studio's own export takes — so a map this
writes is a map an author could have drawn, and anything it cannot say is a gap in the system rather than a
gap in the tool.

Each run writes `region/`, `level.dat` and `map.xml` into `out_dir`, then reports what actually reached the
world: buildings raised, tree sites found, logs and leaves standing. Those counts are read out of the voxels
rather than off the props that were requested, because a prop is a request — the dressing pass drops one that
finds no ground or lands on a protected column — and the only honest report of a forest is the wood in it.

Builds are deterministic: the same spec rebuilds the same map, so two runs can be compared. Each `seed` is
independent — `compose`, `relief`, `trees` and `village` do not need matching values, and giving them one
changes nothing but the draws. **If two runs of one spec disagree, that is not the tool.** It is almost
always a build racing another build: the projects share output, so a second `dotnet build` in the same tree
can swap a DLL mid-run or leave `bin/` without its `runtimeconfig.json`. Build once, then run with
`--no-build`, and never build while another agent is building.

`objective_mode` does not affect anything below it. The same spec at `dtm` and at `dtcm` places the same
buildings on the same ground and seats the same forest, to the block — it changes which goal is stamped, not
where anything can stand. A forest that will not seat is never the objective's doing.

## The board

A spec takes **either** `compose` (ask the layout generator for a board) **or** `plan` (a literal plan
document, for a board that is drawn rather than generated).

```json
"compose": {
  "players_per_team": 15,      // 5–32. A thirty-player map is fifteen a side.
  "teams": 2,                  // 2 or 4
  "symmetry": "rot_180",       // rot_180 | mirror_x | mirror_z for two teams; rot_90 for four
  "seed": 7,
  "cell": 9,                   // blocks per plan cell — what decides how big the map is on the ground
  "objective_mode": "ctw"      // ctw | dtm | dtcm
}
```

`cell` is the knob to reach for when a board feels small: the generator budgets in cells, so raising it grows
the map without changing its layout. At `cell: 5` a fifteen-a-side board lands around 70×120 blocks; the
destroy-the-monument corpus runs a median 148×164, which `cell: 9` or `10` reaches.

`objective_mode` retargets the goals the generator placed. A wool room, a monument and a core occupy the same
slot in a board — one team's thing to defend, sited where the budget put it — so `dtm` turns each goal into a
monument and `dtcm` gives a team both a monument and a core beside it.

## The paint

```json
"theme": {
  "surface": "grass",          // a palette family, or "grass" for the one course that reads from above
  "wall":    "grey stone",     // the face of every drop
  "rim":     "ash",            // the one-block band round an island's outline
  "fill":    "grey stone",     // what the volume under the surface is packed with
  "pattern": "solid"           // solid | voronoi | cell | noise | turbulence | electric
}
```

Families: `verdant` `spring` `turquoise` `loam` `dirt` `brick` `rust` `sand` `gold` `pale stone` `ash`
`grey stone` `cobble` `mauve` `azure` `slate` `dark` `ice` `bright`.

A pattern reads a whole family and carries its fabric down the risers as well as across the ground, so a
stepped surface does not streak vertically where it falls.

## The ground's shape

```json
"relief": {
  "base": 6,                   // the level the field falls back to
  "step": 1,                   // the block quantum the surface snaps to
  "stairs": true,              // cut a way up out of ground a coarse step stranded
  "grain":   { "amplitude": 1.5, "scale": 11, "seed": 3 },
  "scatter": { "count": 10, "min_h": 3, "max_h": 12, "radius": 22, "seed": 5 },
  "marks":   [ ... ],          // optional, stated by hand
  "pushes":  [ ... ]
}
```

`scatter` is the quick way to get ground that is not a table: it places ordinary point marks, so nothing it
does reaches past what an author could draw. `marks` states them by hand instead — `point` (`at`, `r`, `h`),
`line` (`points`, `h[]`, `width`), `area` (`ring`, `h`), `rim` (`h`, `depth`), `scarp` (`points`, `high`,
`low`, `face`, `band`). A relief is bound to every island the board compiled to, because the island is the
unit it is solved over.

Keep it gentle. The corpus walks 0–1 blocks over a median 72.6% of its ground; relief steep enough that the
wall material paints most of the surface reads as a quarry rather than as terrain.

## What stands on it

```json
"trees":   { "count": 60, "form": "mixed", "woods": ["oak","birch","spruce"],
             "min_height": 8, "max_height": 20, "whorled": false, "seed": 4, "clearance": 12 },
"village": { "count": 4, "presets": ["cottage","workshop"], "seed": 3, "clearance": 15 },
"houses":  [ { "preset": "cottage", "x": 40, "z": 0, "front": "negz" } ]
```

`form` is `grown` (the recursive skeleton), `template` (vanilla), or `mixed`. Woods: `oak` `birch` `spruce`
`jungle` `acacia` `dark oak`. `whorled` gathers a grown tree's branches into rings — the conifer against the
broadleaf.

**Ask for two or three times the trees you want.** The dressing pass drops a prop if any cell it occupies
falls on a protected column, and it is normal for well under half of a requested forest to seat on a
generated board. The report says how many sites were found and how many leaves stand, which is how to tell
the two apart: few *sites* means the ground was rejected as too steep or too near an objective, while many
sites and few *leaves* means the pass dropped what was offered.

Read the **leaves**, not the logs. A building's corner posts are logs too, so logs rise as soon as a village
lands and say nothing about whether a tree did. Nothing but a tree lays a leaf.

`clearance` is a distance in blocks, kept between a prop and any objective piece — a room or a spawn, grown
by that much. Raising it pushes scenery further off the goals and leaves less ground to plant on.

**Aim the leaf count, don't just clear a floor.** Around **1,000–5,000 leaves** on a board of this size reads
as wooded — trees you walk between. Under a few hundred is a bare map with a shrub on it. Above roughly
**10,000 the canopy closes** and the map disappears underneath it: a spruce forest at 17,600 leaves on a
120×240 board rendered as one solid green mass with the terrain, the buildings and the routes all buried.
Density is a design decision, so read the number and then adjust it in both directions.

**Relief and dressing compete for the same ground.** Steep terrain is not merely rougher, it is unplantable:
a scarp at face 3 over a 9-block band left 113 of 520 tree sites standing and *none* of them seated, and the
same map with the face lowered and the band widened to 16 carried 785 leaves. If a forest will not seat,
suspect the relief before the trees.

**A grown tree is held to 14 blocks** whatever `max_height` says, because past that it stops being placed at
all rather than being placed tall: measured over one board at twenty-four sites, a grown oak lands 590 leaves
at height 8, 364 at 12 and **nothing whatever at 20**, while a template oak on the same sites climbs
1584 → 3424 → 7194. Its crown is wide, and a wide crown almost always clips something protected.

`village` scatters buildings onto ground flat enough to stand them on; `houses` places one at a stated spot.
Prefer `village` on a generated board — the ground is not known until the plan compiles, so a stated
coordinate is a guess and a guess lands in the void. Presets: `alpine mining` `desert brick` `diorite pyramid`
`townside` `townside on stilts` `cottage` `longhouse` `terrace` `counting house` `workshop`. Every footprint
is held to 20 a side and to the 192 blocks a placed building is allowed.

## The rooms

```json
"room_shell": { "wool": "cottage", "spawn": "terrace" }
```

The buildings a wool room and a spawn room are raised as, named from the same presets. Absent leaves the
built-in shell, which is a bedrock lid — it says "objective here" and nothing about the place it stands in.
`"spawn": "open"` leaves the ground bare, which is right wherever the plateau itself is the room.

Bedrock below this is normal and not a fault: every column of a map carries one at its base so players cannot
dig out, and a wool foundation is laid in it too.

## The rest

`slug` `name` `objective` `authors` `out_dir`. Absent, `out_dir` is
`/media/sf_repos/CommunityMaps/dtcm/<slug>`.

## A whole one

Everything above, as one spec that builds a map worth walking. `objective_mode` is omitted because `ctw` is
the default; say it when the map is a destroy map.

```json
{
  "slug": "thornwake", "name": "Thornwake",
  "objective": "Two woods, one wool each. Cross the hollow and take theirs.",
  "authors": ["mapgen"],
  "out_dir": "/media/sf_repos/CommunityMaps/ctw/thornwake",

  "compose": { "players_per_team": 15, "teams": 2, "symmetry": "mirror_x", "seed": 11, "cell": 9 },

  "theme": { "surface": "grass", "wall": "loam", "rim": "ash", "fill": "grey stone", "pattern": "noise" },

  "relief": {
    "base": 6, "step": 1, "stairs": true,
    "grain":   { "amplitude": 1.8, "scale": 13, "seed": 11 },
    "scatter": { "count": 12, "min_h": 3, "max_h": 12, "radius": 22, "seed": 11 }
  },

  "trees":   { "count": 210, "form": "mixed", "woods": ["oak", "birch", "spruce", "dark oak"],
               "min_height": 9, "max_height": 22, "seed": 11, "clearance": 11 },
  "village": { "count": 4, "seed": 11, "clearance": 15 },

  "room_shell": { "wool": "cottage", "spawn": "longhouse" }
}
```

It reports `4 building(s) · 171/210 tree site(s)` and `6294 leaves` — a wood you can walk through, on ground
that rises and falls, with four buildings on it and its two rooms raised as houses.

## Seeing it

```bash
dotnet run --project tools/mapgen -- --stages <spec.json>       # force the stage-image set on for this run
```

or, in the spec itself: `"stages": true`. **Off by default** — a batch run over many specs should not pay for
pictures it will not look at. When on, every named stage lands as one PNG in `<out_dir>/stages/`, drawn over
the world the build just produced rather than a second read of the region files just written:

| File | Shows |
|---|---|
| `plan.png` | the board before it was built — pieces, zones and markers, off the compiled plan (`GET /plans/{id}/png`'s renderer, called directly) |
| `heightmap.png` | ground shape alone — a hypsometric ramp under hillshade, no reference to what the ground is made of |
| `contour.png` | the same read again with contour lines added — where `heightmap.png` says a slope exists, this says how much it climbs |
| `surface.png` | what the paint actually laid, by material family — structure charcoal, water blue, an unnamed material magenta |
| `dressing.png` | the finished terrain and props read from directly above, before the objective is drawn on top of it |
| `traversability.png` | whether the navigable ground (walkable surface, two blocks of headroom) actually joins spawn to every goal — one dominant colour through every marker is a connected board, a marker in a second colour is cut off |
| `structures.png` | what the world stamped, found by material and independent of theme |
| `topdown.png` | `dressing.png`'s view again with the map.xml goal boxes overlaid — a prop placed through a room shows up in the first, a goal standing over void shows up in the second |

`heightmap.png`/`contour.png` are one renderer read twice, and `dressing.png`/`topdown.png` are the same
top-down read before and after the objective overlay — eight files, six renderers. A top-down alone hides the
third dimension (`review.md` MG13, MG30): read `heightmap.png` for whether a drop is walkable, `traversability.png`
for whether a goal has ground that actually connects to spawn, and `structures.png` for whether a room's floor
sits where the relief left it.

The same renderers are the `RoundTrip` harness's own picture-taking, callable directly against a built map
already on disk:

```bash
dotnet run --project tools/PgmStudio.RoundTrip -- --topdown <out_dir>/region out.png --map <out_dir>/map.xml --scale 3
dotnet run --project tools/PgmStudio.RoundTrip -- --heightmap <out_dir>/region out.png --scale 3 --contour 2
dotnet run --project tools/PgmStudio.RoundTrip -- --surface <out_dir>/region out.png --scale 3
dotnet run --project tools/PgmStudio.RoundTrip -- --structures <out_dir>/region out.png --scale 3
dotnet run --project tools/PgmStudio.RoundTrip -- --traversability-map <out_dir>/region out.png --map <out_dir>/map.xml --scale 3
```
