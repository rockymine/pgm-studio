# How a map is made — start here

This is the map over the other documents in this folder. It says what the levels of description are, which
tool works at which level, and how a map moves between them. It deliberately says nothing about how any one
tool is used: each has its own document, listed at the end.

## Four levels of description

A map is not one document. It is four, each describing the whole map at a different grain, each owned by a
different tool and stored separately.

| Level | Is | Document | Authored in |
|---|---|---|---|
| **The board** | rectangles on a coarse cell grid, what each is for, and where the objectives sit | `PlanModel` — `*.plan.json` | Plan |
| **The ground** | the real geometry at block resolution: outlines, heights, relief, paint, props | `SketchLayout` — the sketch layout | Sketch |
| **The play** | teams, spawns, protections, build regions, objectives and how they are captured | `MapIntent` | Configure |
| **The map** | the voxel world and the `map.xml` a PGM server loads | `VoxelWorld` + `MapXml` | — (built) |

**These are grains, not stages of completeness.** A plan is not a rough draft of a layout: it states things a
layout cannot — that this rectangle is a wool room, that these two pieces share a defence wall — and cannot
state things a layout can, like a curve or a one-block step. The same is true upward: a layout knows where
every block of ground is and has no idea what any of it is for. That is why a finished map needs all four and
why no one of them is "the" map.

**The flow is one-way.** A plan compiles into a layout and an intent; a layout rasterizes into a world; an
intent projects into the map document; the document writes out as `map.xml`. Nothing reads back up. There is
no path from geometry to a plan, and none from a finished `map.xml` to an intent — both are deliberate. The
moves compose in one direction only, and hypothesising what a finished map's plan would have been is a
person's job rather than a tool's.

Beside the four sits one more thing that is not a level at all: the **library** of materials, themes, house
parts and room styles. It knows nothing about maps — no slug, no stage — and the tools that build worlds reach
into it to pick a recipe. What they take is a *copy*, so a library edit can never rebuild a map that already
shipped.

## The tools

| Tool | Route | Works at | Writes |
|---|---|---|---|
| **Generator** | `/generator` | the board | nothing, until a candidate is kept |
| **Shape catalog** | `/catalog` | — | nothing; it is the vocabulary the generator builds from |
| **Plan** | `/maps/{slug}/plan` | the board | `plan_json` |
| **Sketch** | `/maps/{slug}/sketch` | the ground | `sketch_layout_json` |
| **Configure** | `/maps/{slug}/configure` | the play | `map_intent_json`, and the projected document |
| **Library** | `/library` | — | its own tables, shared across every map |

Read the row order as the pipeline. A map that already exists as a `map.xml` has no tool of its own: its
document is changed through the entity routes under *A finished map's document* below.

## Where a map starts

Four ways in, and the difference between them is what exists before the studio does anything.

**From a plan.** `/maps/{slug}/plan` on a fresh map. The board is drawn first — pieces, roles, markers — and
everything else follows from a compile. This is the route that produces both a shape and an intent from one
document, and it is the one an agent should reach for.

**From a sketch.** `/maps/{slug}/sketch` on a fresh map. Ground first, and only ground: a sketch states no
teams, no spawns and no objective, so a map begun here arrives at Configure with geometry and nothing else.

**From the generator.** `/generator` browses a library of whole boards the composer made from a size band, a
symmetry and a seed, 500 for each band and symmetry. Keeping one stores it as a candidate; authoring it originates a map at the plan stage. From that point it is an
ordinary planned map.

**From a world built outside the studio.** `/maps/new` imports a Minecraft world that has terrain and no
`map.xml`, scans it into the database, and hands it to Configure with an empty intent. Nothing upstream
exists: the regions are drawn over ground somebody else built.

**The front door itself**, which no tool owns because it stands before all of them. Every other endpoint in
this folder takes a map; these are what a caller with no map reaches for first.

| Endpoint | Answers | Fails with |
|---|---|---|
| `GET /maps[?stage=&q=]` | every stored map, newest touched first, each with its slug, name, stage and the artifacts it holds — the list a driver picks a slug out of | — |
| `GET /maps/stage-counts` | how many maps sit at each stage, which is the dashboard's own read | — |
| `GET /kit.py` | a Python kit written from the studio's own schema: a constructor per shape a route takes, by the studio's field names, writing only what is stated and checking each word and type before anything is sent; `Studio`, a method per route under a name taken from it, which waits out a `429`, prints `warnings` and raises `Refusal`; `find()` over every description; `build()` through the constructors. Its `ETag` is the schema's hash | 304 `If-None-Match` names the kit's hash, which is current |
| `DELETE /map/{slug}` | nothing — **204**, and the map is gone with everything stored under it: its teams, regions, authors, objectives, scans and every document it held, since each of those rows cascades from the map's. A world folder under a maps root is what a map was scanned from rather than something it holds, and stays; an imported one is offered as an import candidate again. The call for a driver cleaning up after a variant, or a spec re-driven under a corrected slug | 404 `RQ4` no map at that slug |
| `PUT /map/{slug}/source[?dry=true&discard=]` | `{slug, change, replaced, edits, cells, islands, configureUrl}`, and on a dry run the `layout` and `intent` it would store — a whole map stored from its source, a plan compiled or a drawn layout and intent, with the refinement applied onto it, as one change. **The authoring call for a headless caller**, not only the import one, and the whole of it: the compile, the refinement, the finish and the intent's projection run inside it. A map already at that slug is replaced, and only once the source has passed everything it is refused for; `?dry=true` decides all of it, answers the edits and stores nothing. A map made from a refinement refuses a source over a change it has not seen, and `?discard=` names the ones it drops. See *A map's source is the way in, and the way back in* below. Every document answers `RQ3` under the member it was stated as | 400 `not a slug` `RQ1` · 400 `no such change` `RQ1` naming `after` or `discard` · 400 `unreadable discard` · 400 `no base` `RQ1` naming `plan`, `layout` or `intent` · 400 `unreadable document` `RQ1` naming the first field each binder cannot read, under its member (`intent.modes[0]`) · 400 `no name given` · 400 `note too long` · 400 `invalid style or theme` · 400 a person nobody could be called · 403 `RQ8` a map at that slug the caller may not edit · 422 `plan not compilable` · 422 `refinement not applicable` `SR3`/`SR4`/`SR5`/`SR6` · 409 `changes not seen` `SR1`, one per edit a change the source has not seen made, handed over · 422 the drawing carries no ground `SK7` — a refused source stores nothing |
| `GET /map/{slug}/refinement` | the refinement the map's source last stated, `{}` where it stated none; its `ETag` is the change that wrote it | 404 `RQ4` no map at that slug, or no source applied to it |

## The hand-offs

Five transitions, and each is a single call. This is the part worth knowing precisely, because the merge rules
differ and getting one wrong loses work silently.

```
                ┌──► layout ──finish───► world ────┐
plan ──compile──┤                                  ├──► what a server loads
                └──► intent ──project──► document ─┘
```

The two branches rejoin at the end rather than staying independent. Building the world **resolves** parts of
the intent — a destroyable's and a core's block volume, which only the terrain they float over can fix, and,
on a sketch-built map, the monuments themselves — and the `map.xml` is rendered from that resolved copy. It is
why Configure drops its Monuments step on a map that came from a sketch: the answer is derived at build time
rather than authored.

**Plan → layout and intent.** `POST /api/plan/compile` turns the document into both halves at once, and it is
pure: the same plan compiles to the same pair on the server and in the editor. Abutting pieces of equal height
fuse into single polygons, so what arrives in Sketch is a board rather than a grid of rectangles.

**Layout onto a map.** `PUT /api/map/{slug}/sketch/from-plan` **merges** rather than replaces: the sketch's
themes, room shells, dressing and any author-corrected structural height are carried onto the fresh geometry.
Relief is the exception — it is keyed by group id and group identity is derived from the geometry, so a
recompile that re-fuses the board produces different groups and hand-authored terrain has nowhere correct to
land. That case answers **409** in the refusal envelope, one `SK1` finding per group it would orphan, and
`?force=true` accepts the loss. It is the author's call, not the server's. Both write paths answer what the
stored document names and does not have (`SK3`/`SK4`/`SK5`) as complaints on the success, the merge path over
the document the merge produced rather than the one that was posted.

**Intent onto a map.** `PUT /api/map/{slug}/intent/from-plan` carries much less: the **authors and
contributors the stored intent already held**, and nothing else. The plan owns the map's structure, so a
rebuild is meant to replace its teams, spawns, wools and build zones. What it does not own is who wrote the
map.

**The credits are the metadata route's, and an intent naming nobody says nothing about them.** A compiled
intent states `authors` and leaves it empty, which is not the same as stating that a map has none — so the
projection writes the people an intent names and leaves the map's own alone where it names none. Clearing
them is `PATCH /api/map/{slug}/metadata`'s, where a stated empty list means exactly that. **The order of the
two calls does not matter**, on a first build or any other. The carry is intent-to-intent and does a
different job: it keeps the *stored intent* truthful about the people, so the artifact and the map do not
disagree.

Two slices that look like they should ride across deliberately do not, and it is worth knowing because both
are Configure's work. **`islandTeams`** is a derivation rather than a decision, and island ids are positional,
so a tag made about the old board may name a different island on the new one — carrying it would relabel
territory rather than preserve an answer. **`symmetry`** is absent from a compiled intent on purpose: setting
the field is what switches the orbit expander on, and the expander rebuilds an intent from a fixed property
set that drops the structure directives. So **a rebuild clears both**, and Configure's World and Teams phases
have to be walked again — the World phase's gate is the presence of a confirmed symmetry, so the rail re-locks
behind it until they are.

**Layout → world.** `POST /api/map/{slug}/sketch/finish` rasterizes the layout into world geometry and moves
the map to the configure stage. This is the only stage transition the studio performs at runtime.

**Intent → document → `map.xml`.** `PUT /api/map/{slug}/intent` stores the intent and projects it into the PGM
document — teams, kits, regions, filters, apply-rules, spawns — in one idempotent pass.
`GET /api/map/{slug}/xml` renders that document, gated on the pre-flight checks;
`GET /api/map/{slug}/export` gives the world.

**A map's source is the way in, and the way back in.** `PUT /api/map/{slug}/source` stores a whole map from
what it is built from — its **base** and its **refinement** — under the slug the route names: the plan to
re-plan from, the drawing rasterized into geometry, and the intent projected into the document. A map already
stored under that slug is **replaced**, because a source names one map and stating it twice is a reload. A map
stored there that the caller may not edit is refused `403 RQ8`, as every write to a map is (`docs/access.md`).

**The base is a plan, or a drawing and what it is played for.** A `plan` stated alone is compiled here as
`POST /plan/compile` compiles it, and each island's team is filled in on the compiled footprint. A `layout` and
an `intent` stated together are the base as they stand, which is how a board without a plan arrives — a grid
board's plots are discs and crosses where a plan piece is a rectangle — and a plan stated beside them is kept as
the one they were drawn from rather than compiled. Half a drawn pair, or neither base, is `400 no base` naming
the member that is missing.

**The refinement is everything the base cannot state, applied before anything is judged.** A plan has no words
for a coast, a relief, a theme or a date, so the refinement states them onto the board the plan compiles to.
Each statement is the one a Sketch route makes, so a board stated here and one edited by hand end the same.
They are applied in the order a hand works: the paint and fields on the shapes already drawn, the storeys, the
shapes drawn onto them, the relief and the layout's registries, the outlines stated by their shape, the outlines
reshaped a point at a time and then bent, and last the intent's members. A bend resamples whatever ring it is
given, which is why every point edit comes before it.

| Member | States | The route that states the same |
|---|---|---|
| `materials` | `{name: material}` — a material stated once, which `{"use": name}` stands for wherever a material is stated | — |
| `themeByHeight` | `{height: theme}` — the theme each compiled ground shape paints with, by the height it stands at. A room piece is not terrain and is reached only by its id | — |
| `themeById` | `{shapeId: theme}`, winning over the height rule | `PATCH …/sketch/shapes/{shapeId}` |
| `shapePropsByHeight` · `shapePropsById` | fields merged onto a shape by the height it stands at or by its id; a field stated as null is removed | `PATCH …/sketch/shapes/{shapeId}` |
| `addLayers` | storeys, each `{id, name, base_y, below, kind, part_of, seat, shapes, groups}` — over the compiled ground, or under it where `below` is true | `PUT …/sketch/layers/{layerId}` |
| `addShapes` | shapes, each carrying the `layer` and `group` it joins beside its own fields. One naming neither joins the compiled ground and its first group | `POST …/sketch/layers/{layerId}/shapes?group=` |
| `editShapes` | `{shapeId: [edit, …]}`, in order, each stating exactly one of `after` (insert a point on that edge, at `x`/`z` or its midpoint), `index` (move that point to `x`/`z`) and `remove` (drop that point); on a shape the board's symmetry carries onto itself the edit is made at every image unless it states `fan: false` | `POST …/vertices`, `PATCH·DELETE …/vertices/{index}` on the shape, once per image |
| `bendShapes` | `{shapeId: {wander, step, seed, tension, side, edges, fan}}` — `edges` names the edges drawn as coast, each by the vertex it leaves, and every other edge stays as drawn; on a shape the board's symmetry carries onto itself the coast is its own image too unless `fan` is false | `POST …/sketch/shapes/{shapeId}/bend` |
| `outlines` | `{id: {at, radius, radiusZ, points, lobes, wobble, phase, turn}}` — an ellipse pulled in and out by lobes, written as the points of whatever carries that id: a shape's vertices (a rectangle or a circle becoming a polygon), the ring of a relief `area` mark or a push, the points of a stroke, a fluid or a flora prop | the points stated on the thing itself |
| `relief` | `{groupId: relief}`, where `*` stands for every group of the compiled ground | `PUT …/sketch/relief/{groupId}` |
| `themes` · `mapTheme` | the theme registry, and the map's default theme — the registry's first where none is stated | `PUT …/sketch/themes/{themeId}`, `PUT …/sketch/map-theme` |
| `biome` · `roomStyles` · `dressing` | the layout's own members, each replacing what the base held | `PUT …/sketch/biome`, `PUT …/sketch/room-styles/{part}`, the props routes |
| `created` · `authors` | the intent's `meta.created` and `meta.authors`; a person is a bare name or `{name, contribution}` | `PATCH /map/{slug}/metadata` |
| `controlPoints` · `scoreLimit` · `spawners` · `shops` | the intent's own members, each replacing what the base held; a capture point and a generator are each stated once and fanned across the board's symmetry | `PUT /map/{slug}/intent` |

**A thing stated more than once is named, and a name stands for a copy.** A material the refinement uses in
several places is stated once under `materials` and used as `{"use": "strata"}`. A row of the studio's library
is named as `{"library": "dunes"}` wherever a material, a theme, a room style, a prop style or a biome is stated,
and resolved when the source is applied (`docs/tools/library.md`). Either copy has the fields stated beside the
name laid over it, and a name that names nothing refuses the source 422: `SR5` for a material the registry does
not state, `SR6` for a library name that names no single row.

**A compiled shape is named by its component and its height, and a statement anchors to that name.** The id is
the component's ordinally first piece and the surface it stands at — `dale-9` — with the patches after the
first numbered on (`dale-9-2`). A piece renamed, or moved to another height, therefore renames what a statement
is keyed on.

**A statement that reaches nothing is said, and the rest is applied.** One naming a shape or a layer the board
does not have is `SR2`, a complaint listing the ids the board has. An edit the board refuses — a point asked of
a rectangle, an index past the ring — is a complaint on the same terms, naming the edit by its place in the
refinement (`editShapes.dale-9[2]`).

**An outline is stated by its shape, and written as points.** `{at, radius}` is a circle of 28 points; `radiusZ`
makes it an ellipse, `turn` turns it by degrees, and `lobes` bulges — three unless stated — each reaching
`wobble` of the radius past the ellipse and drawing in as far between them, the first at `phase` radians. The
id names everything carrying it, so a relief stated for every group outlines the same mark in each. What lands
is the points, rounded to a tenth of a block, so the stored layout carries the ring a hand would have drawn and
every later statement — a point edit, a bend — works on it. An id that reaches only things with no ring of their
own (a room's rectangle, a point mark, a tree) is a complaint, as is one reaching nothing.

**A statement about a symmetric board is made once, and the board's symmetry fans it.** The symmetry is the
layout's own — `setup.mirror_mode` and its `center`. A capture point and a generator are each stated once and
every image added, at the image of the point it stands on: an image of a capture point takes its name numbered
on (`Bench`, `Bench 2`), and an image of a generator its id (`iron`, `iron-2`), since the regions a generator
mints are named for it. One already standing within half a block of where an image would go is that image, so a
point at the centre of symmetry stays one and a hand-placed pair stays a pair. A point edit and a bend to a shape
the symmetry carries onto itself — a shape on the axis, which no group's fan copies — are made at every image:
an insert or a move lands at the image of its point on the image of its edge, a remove takes the image point
too, and a bend reads its wander at each point's canonical image and draws a named edge's images with it, so
the outline stays its own image. `fan: false` on an edit or a bend makes it alone. A move that takes a point
the symmetry holds in place off its line has no image that keeps the outline its own, so the point moves as
stated and `SR8` says the outline is lopsided now. A relief mark needs none of this: the solve folds a group's
surface across the axis (`docs/world-export/relief.md` §8).

**A statement that does not say what it means refuses the whole source.** A storey stated under an id the board
already has, or under none, is `SR3`, a point edit naming no single point is `SR4`, and an outline that draws no
ring — no centre, a radius of nought, fewer than three points, or a `wobble` of 1 or more, where a trough
reaches the centre — is `SR7`. Each answers `422 refinement not applicable` and stores nothing, because none
can be applied without a guess.

**The refinement is kept as the map's fourth document.** A source's refinement is stored beside the plan, the
layout and the intent as it was stated, and `{}` where a source stated none, so the map holds what it was built
from as well as what it came to. `GET /map/{slug}/refinement` reads it back, and a change that wrote it keeps
it, compares it and restores it like the other three.

**A source is not applied over a change it has not seen.** A map made from a refinement is edited by other
hands too — a person in the Sketch tool fixing a coast or showing the agent how, another writer's source — and a
source built before that edit would replace it without a word. So a source states `after`, the change it was
built against, and where the map's stored refinement states anything, every change after it is one the source
has not seen. Absent, `after` is the change the map's source was last applied as. A source over an unseen change
that edited anything is refused `409 changes not seen`.

**The refusal hands the change over.** It carries one `SR1` per edit an unseen change made, naming the change in
`subjects` and saying in its message who made it, when, what was noted on it and what the notes written at it
say. The finding's `edit` states the edit as the source would, into the refinement wherever the refinement has
words for it. Where only the plan can state it — a compiled shape taken away — the edit is the plan's, the
layout's or the intent's own, with both values.

**Each kind of hand edit lands where the refinement states that kind of thing.** A theme painted on a compiled
shape is `themeById`, and an outline redrawn is `shapePropsById` with the bend or point edits that would redraw
it removed. A shape drawn onto the ground is an `addShapes` entry carrying its layer and group, and a prop moved
is the same move in the refinement's `dressing`. A relief, a theme or a capture point edited is stated whole,
because an edit inside one would lose the rest of it. A change that was itself a source hands over its plan and
refinement, and the rest follows from them.

**A change is taken in or dropped.** A source that takes the edits into its refinement states the change as
`after`; one that replaces them names the changes in `?discard=8,9`, and the change it lands as records them as
`discarded`. A map made without a refinement is its drawing, and a source replaces a hand edit of it without
asking.

**Everything is decided before the stored map is touched.** A document its binder cannot read, a plan the gates
refuse, a refinement that refuses, a style or theme the gate refuses, a person nobody could be called and a
drawing the finish would refuse are each decided from the source alone, so a refused reload leaves the board it
would have replaced. Only then are the four documents stored as **one change** on the slug's history, numbered
above every change the slug has answered. A browser tab that read the old board is therefore refused `RQ5` when
it saves, rather than writing it back over the new one (`docs/refusals.md`).

**A source says where it was built and why.** It may state its `origin` — `{repo, commit, path, dirty}`, where
its documents were built — and a `note` of at most 1,000 characters, and both are kept on the change it lands
as.

**The answer says what the source changed.** `edits` is what the source changes in the documents the map held,
in the shape `GET …/diff` answers, every member stated for the first time where no map was stored. Beside it are
the change the source landed as, whether a map stood under the slug before, and the ground columns and
landmasses the drawing came to.

**`?dry=true` decides all of it and stores nothing.** It answers `change: null` beside the same edits, which is
how a caller sees what a pass would change on a board somebody has edited by hand before the pass replaces
their edits. It answers the `layout` and `intent` the source would store as well, so a caller that wants the
refined board as a body for a preview route has it without a store.

**Each document binds onto its record before anything is made of it.** A reader that cannot read one field gives
up on the whole document, so an intent stating `"modes": ["dtm"]` — `modes` takes objects — would otherwise be
stored as an intent with no teams, spawns or objectives, and the export gate would open on it. It is `400
unreadable document`, one `RQ1` per document naming the field its binder stopped at under the member it was
stated as (`intent.modes[0]`), and nothing is stored.

**Every document answers `RQ3` for a field its reader has nowhere to keep.** The path is prefixed with the member
the document was stated as (`refinement.themeByHeigth`), and a member the source itself does not have is named
bare (`refinment`).

**It is the authoring call and not only the import one, and that is the distinction to get right.** A caller
holding a plan and what it wants of the board stores the whole map in this one request: the compile, the
refinement, the finish that rasterizes the layout and the intent's projection all run inside it. The five
hand-offs above are the **other** caller's path, a map walked through the tools one stage at a time, each stage
writing the document it has just drawn — which is what the browser does, and what a driver never needs. A
driver that walks them instead pays a call for every statement, lands each as a change of its own, and has to
know that the intent's projection lands after the metadata write.

It is also the way back in, because nothing else can take a map back. `POST /map/import-folder` refuses a folder carrying a
`map.xml` outright, `import-url` extracts only `region/*.mca`, and no route in the studio reads a `map.xml` at
all — so a map authored against one studio could reach another only as a world, arriving without its plan, its
drawing or its intent, and could never be re-planned. What a source carries is more than the world does.

**The credits are stated in the refinement, and the source writes them to both places they live.** A compiled
intent names only whom its plan credited, so `authors` states who the map is credited to and the store writes
them rather than a second call the caller has to remember. A person is a bare pseudonym or
`{uuid, name, role, contribution}`, and both forms go to the same two places: the map's author rows, and the
stored intent's `meta.authors`/`meta.contributors`, split by role. Both, because the rows are the map's own
record and the intent is what the export reads — the observer platform's board is stamped from `meta.authors`
— so a store writing one without the other credits the map on its rows and exports it carrying `EX6` over a
blank sign.

A plan, its refinement, and where both came from, as one source:

```json PUT /api/map/{slug}/source
{
  "plan": {
    "plan": 2, "meta": {"name": "Weirgate"},
    "globals": {"cell": 5, "symmetry": "rot_180", "maxPlayers": 8, "surface": 9},
    "pieces": [
      {"id": "spawn", "role": "spawn", "rect": [1, 9, 2, 2]},
      {"id": "dale", "role": "piece", "rect": [-3, 4, 6, 5]},
      {"id": "tor", "role": "piece", "rect": [3, 5, 2, 3], "surface": 13},
      {"id": "ford", "role": "piece", "rect": [-1, -4, 2, 8]},
      {"id": "wool", "role": "wool-room", "rect": [-3, 9, 2, 2]}],
    "placements": {"spawns": [{"piece": "spawn", "at": [5, 5], "facing": "front"}],
                   "wools": [{"piece": "wool", "at": [5, 5]}]}
  },
  "refinement": {
    "themes": {
      "heath": {"rim": {"material": {"kind": "solid", "id": 3}, "depth": 1},
                "surface": {"material": {"kind": "solid", "id": 2}, "depth": 1},
                "wall": {"kind": "solid", "id": 1}, "fill": {"kind": "solid", "id": 3}}},
    "relief": {"*": {"base": 3}},
    "editShapes": {"dale-9": [{"after": 0}]},
    "bendShapes": {"dale-9": {"wander": 1.5, "step": 5, "seed": 3, "side": "in", "edges": [2, 4]}},
    "outlines": {"dale-13": {"at": [20, 32], "radius": 5, "radiusZ": 7, "lobes": 3, "wobble": 0.12, "turn": 10}},
    "created": "2026-09-30",
    "authors": ["Opus 5"]
  },
  "origin": {"repo": "rockymine/pgm-studio-mapgen", "commit": "5daa56f", "path": "specs/weirgate", "dirty": false},
  "note": "the first pass"
}
```

The bend is a coast on the ford's two long sides. They are edges 2 and 4 of `dale-9` as the point edit leaves
it, the edit having put a vertex at 1, so they wander inward while the face `dale-13` stands against and the
walls of the two rooms keep the line the plan drew. The tor, `dale-13`, is drawn as a three-lobed ellipse
instead of the plan's rectangle.

**And the plain writes are not merges.** `PUT /api/map/{slug}/sketch` replaces the layout blob verbatim, which
is what makes a deletion stick, and `PUT /api/map/{slug}/intent` replaces the stored intent wholesale for the
same reason. Only the two `…/from-plan` routes merge.

### When the plan stops being the source of truth

While the staged loop runs, the plan is upstream: edit it, recompile, and whatever the downstream tools added
is re-derived. That stops the moment an author does hand work a plan cannot express — a curve, a relief, a
theme, a placed tree. From then on the sketch and the intent are the working artifacts and the plan is
provenance. Nothing enforces this; the 409 above is the one place the system notices and asks.

## Every change a map keeps

**A write to a map's plan, refinement, layout or intent is a change the map keeps.** Whichever road it comes by — a tool
saving, a source, a restore — it lands as one change per request: numbered per slug, counting up and never
repeating, stamped with the person it was written as and the label of the token that wrote it, and carrying
the `origin` and `note` a source states. The documents are kept whole, once each however many changes write the
same bytes, so a map can be read, compared and put back as it stood at any of its changes
(`docs/architecture.md` has how they are stored).

**Two changes compare document by document, in the shape a finding's fix is stated in.**
`GET /map/{slug}/diff?from=&to=` answers the edits taking the four documents at `from` to those at `to`,
each naming its document, the path it lands on, one of `add`, `set`, `move` and `remove`, the value, the value
it replaced, and the change in words (`docs/refusals.md`). A thing in a list is named by its `id` wherever it
sits, so a shape drawn ahead of the others is one `add` rather than every later shape changing; a list whose
ids changed order is one `set` of the whole list, because a shape's place is its draw order; an outline is one
`set` saying how many of its points moved, how far, and how many were inserted or removed; and a prop whose
`x` and `z` changed is one `move` saying how far it went. Unasked, the diff is what the latest change did, and
`from=0` compares against nothing stated at all.

```text
diff fable-hollin-tarn #1 → #2: 3 edits
layout set    layers[ground].layout.shapes[dale-18].vertices  1 of 6 vertices moved (up to 3.6 blocks)
layout move   dressing.props[fir-1]  moved 5 blocks, from (-40, -65) to (-36, -68)
layout set    relief.team.base  base 12 → 14
```

**And column by column.** `world=true` builds the board at both changes and adds the columns the two builds
disagree on, sorted into ground, surface block and structure, each as a count and its largest runs with the
box to find each in; `?format=png` draws them over both boards' ground. What each class means is
`docs/world-scan/read-backs.md`'s. The edits say what was written, the columns what it built, and the second
is what a note about the ground is answered against.

**A change is put back by writing its documents again, as a new change.**
`POST /map/{slug}/changes/{number}/restore` writes back each document that differs from what the map holds,
through the road that writes it anywhere else: the intent is stored and projected into the map document, the
plan, the refinement and the layout are stored, and a finished board's ground is read again from the layout the next time
anything asks for it. The map row, its notes and the pictures kept of it stay where they are, which is what
separates a restore from a reload. The change is noted as the restore unless the body states a note, and it is
a change like any other — compared, listed, and put back in its turn.

```json POST /api/map/{slug}/changes/{number}/restore
{"note": "back to the first pass, before the east shore was redrawn"}
```

| Endpoint | Answers | Fails with |
|---|---|---|
| `GET /map/{slug}/changes[?since=]` | every change, oldest first: `number`, `at`, `writer`, `writerUuid`, `token`, `origin`, `note`, the `documents` it wrote — `plan`, `refinement`, `layout`, `intent` — and the changes it `discarded`. `since` keeps the changes after a number, which is what a round asks for; `?format=text` answers one line a change | 404 `RQ4` no map at that slug |
| `GET /map/{slug}/changes/{number}` | `{number, plan, refinement, layout, intent}` — each document as the latest change at or before `number` wrote it, absent where none had | 404 `RQ4` no map at that slug, or no change of that number |
| `GET /map/{slug}/diff[?from=&to=&world=true]` | `{from, to, edits, world}` — the edits taking the documents at `from` to those at `to`, and with `world=true` the changed columns. `?format=text` answers the edits one a line and the runs under them; `?format=png` draws the columns, `scale` 1–16 pixels a block | 404 `RQ4` no map, no kept change, or no change of that number · 422 a side holds no layout, so there is no board to build there |
| `POST /map/{slug}/changes/{number}/restore` | `{restored, change, documents}` — the change the restore landed as and the documents it wrote; `change` is absent and `documents` empty where every document already stood as it did then | 400 a note over 1,000 characters · 404 `RQ4` no map at that slug, or no change of that number |

## Stages and layers

A map row carries a **stage** — `plan`, `sketch`, `configure`, `edit` — and separately the **layers** it
holds. They are not the same thing and the studio treats them differently on purpose.

A stage is where a map has got to. A layer is a document it carries, and it keeps carrying it: a map built
from a plan still has its plan after it has been sketched, configured and exported, which is why the maps list
offers every tool a map has been through rather than only the one it stands in. The tallies follow the same
split — sketches are counted by the layer a map holds, configure and edit by the stage a map stands at.

**`edit` is not a stage the flow advances into.** A map originated in the studio ends at `configure` and stays
there; the only runtime transition anywhere in the tree is sketch → configure, at finish. Maps at `edit` are
the ones that arrived as a parsed `map.xml`, which on a development checkout is most of them — 349 of 425 in
this one. That is the whole difference between the two halves of the studio: one authors a map into existence,
the other holds one that already exists, which no tool opens at its stage — the maps list links each to the
layers it still holds.

**A stage is a progress marker and not a lock.** The one-way flow above means nothing reads back up — a later
level never writes into an earlier one — and that is all it means: a configured map may be re-planned, and no
endpoint refuses on `map.stage`. What the stage is *for* is saying which of the moves already open is the one
the author was about to make.

**`GET /api/map/{slug}/state` answers all of it**: the stage, the artifacts, and the moves they allow, each with
its route — and as `behind`, the library names the map's refinement holds whose row has moved on since its
source was applied. A move is offered because the documents it reads are stored rather than because the stage is right
— rebuilding a drawing from a plan needs a plan, whatever stage the map is at — so a driver reads its options
instead of learning them from this document. Its pair is `GET /api/map/{slug}/findings`, which answers what is
wrong with the map right now from every gate the stored documents can reach, and names the gates it did not
ask along with the route that does pay for them.

### Where each document lives, and what it nests inside

A map is a short stack of documents rather than one, each with a C# type that owns its shape and an address
of its own. The four levels above are the stack's spine; these are the parts of the layout that are
separately addressable.

| Document | Type | Where it lives | Read · written at |
|---|---|---|---|
| plan | `PlanModel` | its own layer | `GET·PUT /map/{slug}/plan`, `POST /plan/compile`, the `/plans` store |
| refinement | `Refinement` | its own layer, as the map's source stated it | `GET /map/{slug}/refinement`, `PUT /map/{slug}/source` |
| sketch layout | `SketchLayout` | its own layer, whole | `GET·PUT /map/{slug}/sketch` |
| layers | `SketchLayer` | under the layout's `layers` | `GET /sketch/layers`, `GET·PUT·DELETE /sketch/layers/{layerId}` |
| groups | `SketchGroup` | under a layer's `groups` | `GET /sketch/groups`, `PUT·DELETE /sketch/layers/{layerId}/groups/{groupId}` |
| shapes | `SketchShape` | under a layer's `shapes` | `GET·POST /sketch/layers/{layerId}/shapes`, `GET·PATCH·DELETE /sketch/shapes/{shapeId}`, and `…/bend` and `…/vertices` on one |
| relief | `SketchReliefJson` | under `relief`, **keyed by group id** | `GET·PUT·DELETE /sketch/relief/{groupId}` |
| themes | `TerrainTheme` | under `themes`, which a shape names | `GET /sketch/themes`, `GET·PUT·DELETE /sketch/themes/{themeId}`, `PUT /sketch/map-theme`; library at `/themes` |
| dressing | `DressingDoc` | under `dressing` | `GET·POST /sketch/props`, `PATCH·DELETE /sketch/props/{propId}` |
| biome | `BiomeField` | under `biome` | `GET·PUT·DELETE /sketch/biome` |
| room styles | `HouseStyle` | under `roomStyles` | `GET·PUT·DELETE /sketch/room-styles/{part}`, library at `/room-styles` |
| kept views | `WorldView` | its own sidecar, beside the layout | `GET·POST /map/{slug}/views`, `DELETE /map/{slug}/views/{viewId}` |
| intent | `MapIntent` | its own layer | `GET·PUT /map/{slug}/intent` |
| map.xml | `MapXml` | written, never stored | `GET /map/{slug}/xml`; `MapParser` reads one back |

**The relief rides beside the shapes rather than inside them.** It is keyed by group id because a plan
recompile replaces every shape it produced, and a relief is hand work a plan cannot express.

**A shape is addressed twice over.** It is created under the layer that holds it and edited by its own id
alone, which is why a `PATCH` needs no layer and a `POST` does.

**A source and an export are the whole interface for a caller with no browser.** `PUT /api/map/{slug}/source`
takes the base and its refinement and answers the change it landed as; `GET /api/map/{slug}/export` answers the
world.

## What nothing owns

Worth knowing before looking for a control that is not there.

**The observer spawn** is Configure's alone. A plan puts it at the origin at a computed height and offers no
marker, so unless it is placed in Configure's spawn step, spectators stand at `0, 0`.

**Terrain paint and dressing** are the Sketch tool's alone. A plan carrying theme keys has them dropped on
parse, and Configure has no control for them. That is where the finish belongs rather than where its controls
happened to be built: the sketch rasterizer is what makes the world, so a finish authored a level above it is
authored before the thing it finishes exists, and a scope anchored to a plan piece would freeze at compile
while the ground under it kept being edited. It is also the half of a map a generator cannot reach — a
generator emits a plan, never a theme — so the finish is always hand-authored, at the level where the geometry
is final.

**Water lanes** are the Plan tool's alone. They survive a Configure save and generate their region, but
nothing in Configure renders them.

**The shells stamped over spawns and wool rooms** come from the room styles bound in Sketch's Theme phase.
Configure places the markers and draws the rooms; it cannot choose the building.

**Kits** are nobody's. Every generated team gets one fixed preset, and the only kit statement in the studio is
a spawn's `kit` field on the entity routes, naming which kit it grants — nothing states what a kit contains.

## A finished map's document

**A map that arrived as a `map.xml` has no tool: its document is changed through the entity routes, one
targeted edit at a time.** Each write reads the whole document, applies one edit and saves the whole document
back through the codec (`WriteSupport.RunEditAsync`), so whatever the edit does not touch rides through
unchanged — a map's destroyables survive a wool edit that knows nothing about them. The routes write no world
and no intent, so a map changed this way is not pre-flighted, and it exports unconditionally.

**They are for a surgical change to a finished map** — renaming a region, nudging a spawn's yaw, correcting a
monument's coordinates on a corpus map. One PATCH does it and the rest of the document is untouched. Authoring
a new map through them means writing every region, filter and apply-rule by hand, which is what the intent
(`configure.md`) exists to replace.

| Endpoint | Does |
|---|---|
| `GET /map/{slug}` | the whole parsed document — teams, spawns, wools, regions, filters, apply-rules, kits — with its revision as an `ETag` |
| `GET /map/{slug}/regions/tree` · `/regions` | the region tree grouped by category, and the flat registry |
| `PATCH /map/{slug}/metadata` | name, version, objective, max build height, authors |
| `GET /minecraft/player[?name=\|uuid=]` | one player as `{uuid, name}` — a typed username to the canonical uuid an author entry is stored under, and back. A value not shaped like an account name is never asked about, and a resolved pair is answered from `minecraft_player` for thirty days. **404** means no account is called that |
| `GET /minecraft/player/{uuid}/skin` | the player's skin as a PNG, served from the studio so a browser draws a head without asking a third party; fetched from Mojang's texture server on first ask and kept thirty days beside the name. **404** means there is none to be had, and the client draws the player's initial instead |
| `POST` · `PATCH` · `DELETE /map/{slug}/teams[/{teamId}]` | the teams |
| `POST` · `PATCH` · `DELETE /map/{slug}/spawns[/{regionId}]` | a spawn's region, team, yaw and kit — the `kit` field names which kit the spawn grants, and nothing in the studio states what a kit contains |
| `PATCH` · `DELETE /map/{slug}/observer-spawn` | the `<default>` spawn |
| `POST` · `PATCH` · `DELETE /map/{slug}/wools[/{woolId}]` | the wool objectives |
| `POST` · `PATCH` · `DELETE /map/{slug}/wools/{woolId}/monuments[/{monId}]` | their capture points |
| `POST` · `PATCH` · `DELETE /map/{slug}/regions[/{regionId}]` | create, re-coordinate or rename, delete — the numbers nested under `coords` on both writes |
| `POST /map/{slug}/regions/group` · `/ungroup` | union two or more, dissolve a compound |
| `POST /map/{slug}/regions/{regionId}/counterpart` · `/orbit` | mirror a region onto the other team, or round the orbit |
| `GET /map/{slug}/xml` | the rendered `map.xml` |

**Their failures run through one path, so their codes are uniform.** **400** (`RQ1`, `ED1`, `ED2`) is a payload
the document will not take, **404** (`RQ4`) an unknown map, region, team, wool, monument, spawn, filter or
apply-rule, and **409** (`RQ5`) an id already in use, with the id holding the name in the finding's
`subjects`. Payload validation runs before the lookup, so a malformed body aimed at something that does not
exist answers 400 rather than 404. `POST …/spawns`, `PATCH …/observer-spawn` and `POST …/teams` bind their
request record, so a missing `region_id` or `id` is refused before the map is read; every update stays
hand-read, because an update tells an absent field (leave it) from a `null` one (clear it) by whether the key
is there at all.

**Two editors on one map keep only the second, unless they state a revision.** Every write rewrites the whole
document, so any of them may state the revision `GET /map/{slug}` answered as an `If-Match`; one naming a
revision the map is no longer at is refused as `RQ5`, and one stating nothing writes as it always did.
`docs/refusals.md` carries the rule.

**Their bodies and answers are declared.** The request records are `Contracts/EditRequests.cs` and the answer
records `Contracts/EditDtos.cs`, both published in the schema; `EditRequestShapeTests` and
`EditAnswerShapeTests` hold each to the editor behind it. Most writes answer `{}`; a created, grouped,
dissolved or fanned region answers the id the caller now names it by, and a wool, a monument or a team
answers the row in the shape `GET /map/{slug}` carries it.

**A region's numbers always travel nested under `coords`, on a create and on a patch alike.** A patch takes
`{"id": …}` to rename — cascading through the categories, every compound's child list, and any spawn or wool
room pointing at it — and `{"coords": {…}}` to move it, answering the footprint the region now covers. The
stored type chooses which numbers it reads, and a number the type does not use is accepted and ignored. A flat
payload is a 400 carrying `RQ1`; a create whose `coords` is short of a number its type needs names it
(`coords.max_x`), and on a patch an absent or `null` number means leave it.

```
GET   /api/map/sentient                            → the whole document
GET   /api/map/sentient/regions/tree               → the tree: the region's id, type and current numbers
PATCH /api/map/sentient/regions/blue-spawn-point   {"coords": {"min_x": 234, "min_z": 149,
                                                               "max_x": 238, "max_z": 151}}
POST  /api/map/sentient/regions/group              {"type": "union", "child_ids": ["blue-spawn-point"]}
→ 400 { "error":    "edit not applicable",
        "message":  "union requires at least 2 region(s)",
        "findings": [ { "rule": "ED2", "message": "union requires at least 2 region(s)",
                        "severity": "refusal" } ] }
```

The ids are real — `sentient` carries `red-spawn-point`, `blue-spawn-point`, `spawns`, `obs-spawn-point`,
`wool-rooms` and a monument per colour — and `blue-spawn-point` is a `cuboid`.

## What a write endpoint takes

**There are two conventions, and which one an endpoint follows is decided by what the endpoint is about.**
Twenty-five of the studio's write endpoints take one shape and fourteen take the other, so an author guessing
gets it right slightly more than half the time — and a wrong guess is silent, because an unknown property is
dropped rather than reported. Posting `{"style": {…}}` where the body should be a bare style answers **200**
with a preview of the defaults.

**A document endpoint takes the document itself, unwrapped.** Anything whose subject is one of the studio's
own documents — a plan, a sketch, an intent, a relief, a paint, a terrain material, a whole theme, a room
style — is posted as that document and nothing else. `POST /plan/compile` takes a plan; `POST
/terrain/material-preview` takes a material; `POST /room-styles/preview-snapshot` takes a `HouseStyle`. There
is no envelope and no field to name.

**A library endpoint takes a wrapper, because it carries more than the document.** Saving into the library
needs a name and a kind beside the thing being saved, so those take a small record: `POST /styles` is
`{name, kind, params}`, `POST /themes/import` is `{name, themeJson}`. Where such a wrapper carries a document,
the document is a **string** in that field rather than an object — the mirror of the `GET` that returns it,
which answers `{themeJson: "…"}` and `{styleJson: "…"}` the same way.

**The rule for telling them apart**: if the endpoint is asking *about a document*, post the document; if it is
asking to *file a document under a name*, post the record that names it. A document endpoint's own tool
document says which document, in the table row.

**A body that cannot be read is refused, never crashed.** An absent, empty or malformed document answers 400
carrying `RQ1` and, where the reader knew one, the field — `roof.gableWindows` rather than a sentence to hunt
through a document for. A missing wrapper field is refused the same way, and every missing field is named at
once rather than one per round trip. `docs/refusals.md` has the envelope.

## Where to read next

| Document | Read it for |
|---|---|
| `plan.md` | the board: the plan document field by field, what a compile produces, the refusals |
| `sketch.md` | the ground: shapes, groups, relief, themes, dressing — the largest tool |
| `configure.md` | the play: the intent, the import path, the objective phases, the export gate |
| `generator.md` | composed boards: the request, what a compose produces, the library and the browse feed |
| `shapes.md` | the vocabulary the generator fills boxes with, and how far each shape actually gets |
| `library.md` | materials, themes, house parts and room styles — the fourteen material kinds |

One document outside this folder carries the rest: `docs/generator/model.md` is the canonical model of
layout generation and governs on any disagreement about it. What the system can be **asked** for is not a
document at all — `/api/openapi/v1.json` names every route with its body and its failure codes,
`GET /api/rules` names every refusal with its fix, and `GET /api/map/{slug}/state` names the moves one map
has open.

`docs/gameplay/approaches.md` answers the question neither of those can: what the ground around an objective
does to a match, and therefore what a board should be composed *for*. It is kept separate because every claim
in it is the author's rather than the repository's — no corpus reading and no line of code settles what plays
well — and each one is marked with whether the author has confirmed it. Every claim it currently carries is
confirmed, so it is law rather than advice.
