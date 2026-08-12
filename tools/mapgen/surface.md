# What a map is made of, and where each part is made

The reference `tools/mapgen` should have been written against. A map is not one document — it is a short
stack of them, each owned by a different project, each with its own JSON shape, its own endpoint and its own
generator. Reading the stack is what stops a tool from inventing a flatter format of its own and losing the
system's reach in the process (`review.md` MG29).

Four layers, in the order a map moves through them: a **plan** says where things go in cells, a **layout**
says what ground exists and what it is made of, an **intent** says what the map is played for, and the
**world plus `map.xml`** is what a server loads. Nothing skips a layer, and each is stored separately, so a
map can stand at any of `plan` / `sketch` / `configure` / `edit` (`MapStage`) and carry the layers below it.

## The documents

Each is real JSON with a C# type that owns its shape. The type is the specification; the endpoint is how the
editor reaches it.

| Document | Type | Lives in | Read / written at |
|---|---|---|---|
| plan | `PlanModel` | `Pgm/Plan/PlanModel.cs` | `GET /map/{slug}/plan`, `POST /plan/compile`, `/plans` store |
| sketch layout | `SketchLayout` | `Pgm/Sketch/SketchLayout.cs` | `GET·PUT /map/{slug}/sketch` |
| relief | `SketchReliefJson` | `Pgm/Sketch/SketchRelief.cs` | inside the layout under `relief`, keyed by island id |
| themes | `TerrainTheme` | `Minecraft/TerrainTheme.cs` | inside the layout under `themes`; library at `/themes` |
| dressing | `DressingDoc` | `Minecraft/Dressing/DressingJson.cs` | inside the layout under `dressing` |
| room styles | `HouseStyle` | `Minecraft/HouseStyle.cs` | inside the layout under `roomStyles`; library at `/room-styles` |
| intent | `MapIntent` | `Pgm/Authoring/MapIntent.cs` | `GET·PUT /map/{slug}/intent` |
| map.xml | `MapXml` | `Domain/MapModel.cs` | written by `XmlWriter`, parsed by `MapParser` |

The **plan** is the smallest and the most semantic: pieces with rects and roles, and `placements` holding
spawns, wools, iron, destroyables and cores as piece-relative half-cell offsets, authored for **team 0 only**
and fanned by symmetry at compile. Beside them it carries `walls` — a defence wall named by the two pieces
whose interface it stands on — and `globals` with the cell size, the symmetry and the player count. It is the
right surface for an agent because it is small and because the validator and evaluator answer it with
rule-ids.

The **layout** is the ground: `shapes` (rectangle, circle, polygon, path) with set-algebra operations,
grouped into `islands` that decide what mirrors. A shape carries far more than a footprint — its own `theme`,
its `floor` and `base_height`, per-vertex `anchor_heights`, a `height_mode` of `level`/`raise`/`sink` with a
`skirt`, and a `relief_scope` of `hold`/`exclude` deciding whether its ground joins the island's solved
relief. The **relief** rides beside the shapes rather than inside them, keyed by island id, because a plan
recompile replaces every shape it produced and a relief is hand work a plan cannot express.

The **intent** is what the map is *for*: teams, spawns with yaw and protection, wools with rooms and
monuments, destroyables, cores, the build region and its holes, water lanes, and `structures` — room floors,
entrance redstone, iron cubes and approach walls. It is the only layer that knows the objective.

## A worked example

`tools/seeds/ruediger.layout.json` and `ruediger.intent.json` are a hand-drawn capture map, kept because it
is the only authored document in the repository that uses the layout format to its width. Everything below
is in those two files and none of it was reachable through mapgen's spec.

**Elevation is built from shapes, not from relief.** The layout carries no `relief` block at all. Its ground
steps because twenty-six shapes sit at `base_height` 7, 8, 9, 10, 11, 12, 13, 14, 15 and 16 — a staircase of
tiers stacked as set algebra, one of them a `subtract` cutting a hole through the rest, and one at
`base_height` 100 standing as a tall face. That is the second way to make terrain, beside the relief solver,
and it is the one that gives deliberate steps rather than a solved surface.

**Three themes over one map, chosen per shape.** `ruediger` (sand and sandstone over stone) paints the body,
`ruediger-steps` (grass and stone brick) picks out the treads and the border, and `theme` (stone brick and
stained clay) carries the rest; seventeen of the twenty-six shapes name one. The stepped area therefore reads
as built and the ground around it reads as ground — the distinction MG2 says a single blanket theme throws
away.

**Curves and a hole.** Five shapes carry Bézier `controls` on their polygon edges, so their outlines are
drawn rather than rectilinear, and one shape is `subtract`.

**Two islands that mirror into four.** Both are `mirrors: true` under `rot_180`, so the authored half becomes
the whole 140×260 board.

**Hand-authored defence walls.** The intent carries two `structures.walls` entries — 21×3 at `topY` 13 and
its orbit image — the bedrock approach walls MG21 says nothing generated has ever asked for. It also carries
the two room floors and the two entrance redstone lines.

What it has none of is houses, dressing or iron, which is exactly the gap the review's dressing entries
describe. One clarification worth keeping, because the map is easy to misread: it holds a **single** sketch
layer named "Ground". The layering that reads as depth is the shapes' own `base_height` tiers, not
`layers[]`.

## Where the generation lives

| Stage | Lives in | Takes → gives |
|---|---|---|
| compose a board | `Pgm/Compose/Composer.cs` | `ComposeRequest` → `PlanModel` |
| validate / score | `Pgm/Plan/PlanValidator.cs`, `Evaluate/` | `PlanModel` → findings with rule ids |
| compile | `Pgm/Plan/PlanCompiler.cs` | `PlanModel` → `(SketchLayout, MapIntent)` |
| rasterize | `Pgm/Sketch/SketchRasterizer.cs` | layout JSON → columns `(x, z, yFloor, yTop)` |
| solve relief | `Geom/Relief/` | `ReliefSpec` → a surface per island |
| build the world | `Api/Services/SketchWorldBuilder.cs` | layout + intent → `VoxelWorld` + resolved intent |
| paint | `Minecraft/TerrainPainter.cs` | raw stone → rim, wall, surface, fill |
| dress | `Minecraft/Dressing/Decorator.cs` | props → trees, houses, boulders, paths, water |
| stamp buildings | `Minecraft/HouseStamper.cs` | `Footprint` + `HouseStyle` → walls, roof, openings |
| stamp furniture | `Minecraft/StructureStamper.cs` | `StructureIntent` → floors, redstone, iron, walls |
| write the goal | `Pgm/Authoring/IntentGenerator.cs` | resolved intent → the map document |
| write the XML | `Api/Services/MapXmlComposer.cs` → `Pgm/XmlWriter.cs` | document → `map.xml` |

`SketchWorldBuilder` is the one to read before changing anything downstream, because **order is the
contract**: floors, then wool cages, then spawn cubes and monuments, then plan-derived structures, then the
build-region outline, then destroyables and cores, then the terrain finish, then the dressing, then the
observer platform. Painting happens *after* every stamp so it can skip a column whose top is not terrain, and
dressing happens after painting so it can read the finished surface rather than re-derive it. A pass inserted
in the wrong place is not a small mistake — it is a house painted as ground, or a tree planted through a
monument.

`MapXmlComposer` is the export path and not an optional wrapper: it applies `CtwStandards` (the keep, repair
and remove rules derived from the spawn kit, hunger off, the kill-reward include), the ore and structure
renewables, and the reordering that puts the `not-build-area` rule last. Calling `XmlWriter` directly skips
all of it (MG14).

## What to look at, and when

The system renders itself at every stage, and a generated map should be read at each rather than judged from
one picture at the end (MG13, MG30). Three families exist.

**The API previews** answer a document without building a world — thirteen endpoints, none of which the first
fifteen maps used. `POST /terrain/theme-preview` and `/terrain/theme-map-preview` show a theme as it will
paint, the second over a compiled plan; `/terrain/material-preview` shows one material; `/terrain/prop-preview`
shows a tree, boulder or path before it is placed; `/room-styles/preview` and its `-snapshot`,
`/roof-styles/preview`, `/porch-styles/preview` and `/storey-styles/preview` show a building from four sides;
`/themes/preview` shows a library row. `GET /plans/{id}/svg` draws the plan itself, and `GET /shapes/probe`
emits a canonical family through the real emitters and answers with the shape or a directed rejection.

**The corpus and world harnesses** in `tools/PgmStudio.RoundTrip` read a built world back: `--topdown` for
the plan view, `--heightmap` and `--contour` for the third dimension, `--surface` for what the paint did,
`--traversability` for what a player can walk, `--buildings` and `--structures` for what was stamped,
`--island-study` and `--skeleton-study` for footprint shape and centrelines, `--water`, `--flora`, `--ores`
and `--underground` for the rest.

**The prototypes** render the model rather than a map. `tools/relief` emits ten figures plus a topographic
view, a blocks-from-an-angle view, a section and a step map, and `--corpus` measures real worlds into the
same terms. `tools/compose` holds twenty-two galleries — boards, bodies, boxes, edges, mids, seeds, hubs —
each rendering the real emitters.

The rule that follows: **a stage that produced something should be looked at before the next stage consumes
it.** A theme is checkable before a world is built, a plan is checkable before it compiles, a shape is
checkable before it is drawn, and a forest is checkable in a heightmap rather than guessed from a leaf count.
