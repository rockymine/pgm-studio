# mapgen — a whole map from one JSON spec

> `docs/tools/capabilities.md` maps the documents a map is made of and where each generator lives — read it
> before writing anything against this tool's spec, which is a thin addressing layer over it: `plan`,
> `layout` and `intent` are handed through verbatim, and the convenience fields below are shorthand that
> expands into a fragment of one of those three (`mapgen-review.md` MG29).
>
> `docs/tools/mapgen-review.md` records where the first fifteen maps fell short of what the corpus ships, as a
> pool of `MG` entries. Read it before adding to the tool: several of the knobs documented here are the wrong
> knobs.
>
> `docs/gameplay/approaches.md` is what a map is composed *for*, and is the author's document rather than
> this one's.

```bash
dotnet run --project tools/mapgen -- <spec.json> [more.json ...]
dotnet run --project tools/mapgen -- --describe <spec.json>    # compile only, report the board
```

The spec says what a map is *about*; the generator answers the rest. The build runs through
`MapExportComposer.ComposeSketch` — literally the studio export's own sketch leg, gates included — so a map
this writes is a map an author could have drawn, a map the HTTP export would also have shipped, and one it
would have refused fails here with the same findings (`OB17` objective placement over the rasterized ground,
wool monuments included; `OB19` goal clearance; the `B140` playability judgement). Anything neither the
convenience fields nor a handed-through document fragment can say is a gap in the system rather than a gap
in the tool.

There is no sampler. `sketch.md` states the studio's own design: dressing is authored, and there is no
scatter, no density pass and no "fill this island with forest", because a tree is cover and where cover
stands is a gameplay decision. A prop reaches the map the same way here — one entry in `dressing.props`,
written through the spec's `layout` fragment, at an exact coordinate the author chose.

Each run writes `region/`, `level.dat` and `map.xml` into `out_dir`, then reports what actually reached the
world: shapes, islands, goals, spawns, and the buildings/trees read back out of the finished layout's own
dressing. Buildings and trees are counted from what was authored (`dressing.props`), not from what landed —
a prop is a request, and the dressing pass declines one that finds no ground or lands on a protected column.
Every whole-prop decline is reported: one stderr line per drop (`! <slug>: dropped <kind> '<id>' — <reason>`)
and, when anything dropped, `region/dressing-report.json` beside the provenance sidecar — absence means
everything authored stood. `--stages`' `dressing.png` remains the picture the report is checked against.

Builds are deterministic: the same spec rebuilds the same map, so two runs can be compared. **If two runs of
one spec disagree, that is not the tool.** It is almost always a build racing another build: the projects
share output, so a second `dotnet build` in the same tree can swap a DLL mid-run or leave `bin/` without its
`runtimeconfig.json`. Build once, then run with `--no-build`, and never build while another agent is building.

## The board

A spec takes **either** `compose` (ask the layout generator for a board) **or** `plan` (a literal plan
document, for a board that is drawn rather than generated).

```json
"compose": {
  "players_per_team": 15,      // 5–32. A thirty-player map is fifteen a side.
  "teams": 2,                  // 2 or 4
  "symmetry": "rot_180",       // rot_180 | mirror_x | mirror_z for two teams; rot_90 for four
  "seed": 7,
  "cell": 9                    // blocks per plan cell — what decides how big the map is on the ground
}
```

`cell` is the knob to reach for when a board feels small: the generator budgets in cells, so raising it grows
the map without changing its layout. At `cell: 5` a fifteen-a-side board lands around 70×120 blocks; the
destroy-the-monument corpus runs a median 148×164, which `cell: 9` or `10` reaches.

`compose` only ever asks for a capture-the-wool board — the generator has no destroy-native composer
(`mapgen-review.md` MG1's still-open half). **A destroy board is authored as its own `plan`, not converted
from one.** A wool room and a destroyable do not occupy the same slot in a board: a wool sits at the far end
of a dead-end lane, inset about five, walled and entered from one side, because a wool is a thing an *enemy*
has to reach and carry back. A destroyable is a thing a team **defends in the open**, and it may stand on
**any piece of the plan** — in a field, on a plateau, on the frontline, anywhere ground exists. It needs no
room, no lane and no protection region. Write `placements.destroyables`/`placements.cores` directly into the
`plan` document — `DestroyablePlacement`/`CorePlacement` in `Pgm/Plan/PlanModel.cs`, each a `{ id, piece, at,
style, materials, float, name }` — the same way a wool marker is authored, and the goal lands where the design
put it rather than where a wool budget sited it.

`DestroyablePlacement.Materials` names what a monument is made of — one of the four the stamper can actually
build (empty defaults to obsidian, over half the corpus): `obsidian`, `emerald block`, `gold block`, `ender
stone` (`PgmStudio.Domain.DestroyableMaterials`). A core's casing is not a knob: it is always obsidian, the
same as PGM's own default. The spawn kit's pickaxe has to be able to break whatever a `materials` names — an
obsidian goal wants a diamond pickaxe rather than the default iron — and a kit that cannot break a goal it
ships refuses the map rather than shipping it silently unbreakable.

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

`theme` paints the **whole map** with one registry entry — it is shorthand for one `TerrainTheme`
(`Minecraft/TerrainTheme.cs`) written to `layout.themes.map` and bound as `layout.mapTheme`. The full type is
five buckets (`rim`, `surface`, `wall`, `fill`, `bedrock`), three of which carry their own geometry, and it
scopes **per shape** — a board can carry as many themes as it has shapes for, the way
`tools/seeds/ruediger.layout.json` carries three. None of that is reachable through `theme`; it is reachable
through `layout` (below), which hands a `themes` registry entry through verbatim.

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
`low`, `face`, `band`) — the full five-kind vocabulary handed through verbatim either way. A relief is bound
to every island the board compiled to, because the island is the unit it is solved over.

Keep it gentle. The corpus walks 0–1 blocks over a median 72.6% of its ground; relief steep enough that the
wall material paints most of the surface reads as a quarry rather than as terrain.

## The rooms

```json
"room_shell": { "wool": "cottage", "spawn": "terrace" }
```

The buildings a wool room and a spawn room are raised as, named from the house presets: `alpine mining`
`desert brick` `diorite pyramid` `townside` `townside on stilts` `cottage` `longhouse` `terrace`
`counting house` `workshop`. Absent leaves the built-in shell, which is a bedrock lid — it says "objective
here" and nothing about the place it stands in. `"spawn": "open"` leaves the ground bare, which is right
wherever the plateau itself is the room.

Bedrock below this is normal and not a fault: every column of a map carries one at its base so players cannot
dig out, and a wool foundation is laid in it too.

## The addressing layer: `layout` and `intent`

```json
"layout": {
  "themes": { "vault": { "closed": true,
    "surface": { "material": { "kind": "solid", "id": 155, "data": 0 }, "depth": 3, "enabled": true },
    "rim":     { "material": { "kind": "solid", "id": 41,  "data": 0 }, "depth": 2, "enabled": true } } },
  "dressing": { "props": [ { "kind": "tree", "x": -5, "z": 20, "form": "template", "species": "spruce", "height": 14 } ] }
},
"intent": { "maxPlayers": 16 }
```

`layout` is a `SketchLayout` fragment, `intent` a `MapIntent` fragment — both handed through verbatim and
merged onto whatever `compose`/`plan` produced (`DocumentOverlay.Merge`, `src/PgmStudio.Pgm/`), *after* every
convenience field above has run. An object key present on both sides merges key by key, so naming one more
theme does not erase the ones `theme` already wrote; an array key present on both sides is **appended to**,
not replaced, so a shape, a destroyable, a prop or a wall adds to what the board already carries rather than
overwriting it; anything else — a scalar, a key the base document does not have — the fragment's value
replaces outright.

This is where a prop is placed. There is no sampler (above), so `dressing.props` — the studio's own authored
dressing document — is the *only* way a tree, a boulder, a path or a building reaches a generated board:
`{"kind":"tree",...}` (`TreeProp`) is the simplest; `{"kind":"house",...}` (`HouseProp`) additionally needs a
full `HouseStyle` in its `style` field, the same JSON `GET /room-styles/{id}` or `HouseStyleJson.Serialize`
over a `HousePresets` row produces — there is no preset-by-name shorthand at this layer, since `layout` hands
the document through exactly as the studio stores it. It is also where a shape the convenience
fields cannot state goes — an extra `SketchShape` with its own `theme`, `floor`, `base_height`,
`anchor_heights` and `relief_scope` — and where the full `TerrainTheme` a theme registry entry can carry
(a rim band with its own depth, a per-shape scope) is reachable, since `layout.themes` is a plain dictionary
an overlay key merges into. A `MapIntent` fragment reaches whatever the plan a board compiled from did not
carry — `structures.walls`, `structures.ironCubes`, in already-resolved world coordinates.

**One key limit, honestly stated: the overlay adds, it does not retarget.** An array is appended to, so a
new shape can carry its own theme, but an *existing* shape the composer or the plan already produced cannot
be repainted through `layout` — array-append has no way to reach into element `N` and change one field.
Differing the paint per **compiled** shape (`mapgen-review.md` MG2) is still a gap in the system for a
composed or plan-drawn board; it is reachable only by drawing the whole board's paint into the `plan`
document's own pieces, or by hand-editing the sketch afterward in the studio.

## The rest

`slug` `name` `objective` `authors` `out_dir`. Absent, `out_dir` is
`/media/sf_repos/CommunityMaps/dtcm/<slug>`.

## A whole one

Everything above, as one spec that builds a map worth walking.

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

  "room_shell": { "wool": "cottage", "spawn": "longhouse" },

  "layout": {
    "dressing": { "props": [
      { "kind": "tree", "x": -45, "z": -10, "form": "template", "species": "spruce", "height": 14 },
      { "kind": "tree", "x": -45, "z": 20, "form": "template", "species": "birch", "height": 11 }
    ] }
  }
}
```

It reports `8 shapes · 1 island(s) · 4 wool(s) · … · 0 building(s) · 2 tree(s)` at compile, and the finished
build's census reads `162 logs · 304 leaves` — both trees standing, none dropped to a protected column or
void. `--describe` is how the `x`/`z` above were found: it reports the compiled board's shape ids and
bounds, so a prop's site is a stated coordinate rather than a guess even though the board is not known ahead
of a `compose` call.

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
| `surface.png` | what the paint actually laid, by terrain-paint family — structure charcoal, water blue, an unnamed material magenta |
| `dressing.png` | the finished terrain and props read from directly above, before the objective is drawn on top of it — false-coloured by category (ground/structure/foliage/water), not by real material |
| `foliage.png` | `dressing.png`'s categories again with only foliage highlighted, everything else a flat context tone |
| `traversability.png` | whether the navigable ground (walkable surface, two blocks of headroom) actually joins spawn to every goal — one dominant colour through every marker is a connected board, a marker in a second colour is cut off |
| `structures.png` | what the world stamped, found by material and independent of theme |
| `topdown.png` | `dressing.png`'s view again with the map.xml goal boxes overlaid — a prop placed through a room shows up in the first, a goal standing over void shows up in the second |
| `objectives.png` | the map.xml overlay alone, on a dim uniform backdrop — where the declared goals sit, with nothing else competing for the reader's eye |

`heightmap.png`/`contour.png` are one renderer read twice, and `dressing.png`/`topdown.png`/`foliage.png`/
`objectives.png` are the same top-down renderer read four ways — the combined categorised view, that view
with the goal overlay, and two single-question isolations. A top-down alone hides the third dimension
(`review.md` MG13, MG30): read `heightmap.png` for whether a drop is walkable, `traversability.png` for
whether a goal has ground that actually connects to spawn, and `structures.png` for whether a room's floor
sits where the relief left it. Every PNG here carries a legend and a scale baked into the image itself
(`B98`, `B95`) — the false colour is deliberate (foliage violet, structure orange, ground a muted grey, water
cyan), chosen to separate categories rather than to depict them, and `--topdown`'s `--material` flag switches
back to the real per-block colours for a caller checking a theme's actual paint rather than the map's shape.

The same renderers are the `RoundTrip` harness's own picture-taking, callable directly against a built map
already on disk:

```bash
dotnet run --project tools/PgmStudio.RoundTrip -- --topdown <out_dir>/region out.png --map <out_dir>/map.xml --scale 3
dotnet run --project tools/PgmStudio.RoundTrip -- --heightmap <out_dir>/region out.png --scale 3 --contour 2
dotnet run --project tools/PgmStudio.RoundTrip -- --surface <out_dir>/region out.png --scale 3
dotnet run --project tools/PgmStudio.RoundTrip -- --structures <out_dir>/region out.png --scale 3
dotnet run --project tools/PgmStudio.RoundTrip -- --traversability-map <out_dir>/region out.png --map <out_dir>/map.xml --scale 3
```
